using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FUNC.Health
{
    public sealed class LogLine
    {
        // Where the line starts in the file; unique within one log file.
        public long Offset { get; set; }
        public DateTimeOffset? Time { get; set; }
        public string Level { get; set; } = "info";
        public string Msg { get; set; } = "";
        // The whole line as the node wrote it (very long lines are cut short).
        public string Raw { get; set; } = "";
    }

    public sealed class LogPage
    {
        public bool Found { get; set; }
        // Matching lines, newest first.
        public List<LogLine> Lines { get; set; } = [];
        // Pass as "before" to continue further back; null once the start is reached.
        public long? Next { get; set; }
        // End of the last complete line. Pass as "after" to get only what is newer.
        public long End { get; set; }
        // The "after" position no longer exists: the log was rotated, start over.
        public bool Reset { get; set; }
    }

    // Finds lines in a node.log, newest first. A busy node's log is hundreds of
    // megabytes, so a search reads backwards from the end in chunks and gives up
    // after a fixed amount, handing back a cursor to continue from.
    public static class LogSearch
    {
        public const int MaxLimit = 1000;
        public const int MaxQueryLength = 200;
        internal const int ChunkBytes = 4 * 1024 * 1024;
        internal const long ScanBytes = 64 * 1024 * 1024;
        private const int MaxLineBytes = 16 * 1024 * 1024;
        private const int MaxRawChars = 20_000;
        private const int MaxMsgChars = 500;

        private static readonly string[] Levels = ["debug", "info", "warning", "error", "fatal", "panic"];

        // level: the least severe level to include (info, warning or error).
        public static LogPage Search(string path, string? query, string? level, long? before, long? after, int limit,
            int chunkBytes = ChunkBytes, long scanBytes = ScanBytes)
        {
            int minRank = level switch
            {
                null or "" or "warning" => 2,
                "info" => 0,
                "error" => 3,
                _ => throw new Exception("Level must be info, warning or error"),
            };
            if (query != null && query.Length > MaxQueryLength) throw new Exception("Search text is too long");
            if (string.IsNullOrWhiteSpace(query)) query = null;
            limit = Math.Clamp(limit, 1, MaxLimit);

            LogPage page = new();
            if (!File.Exists(path)) return page;
            page.Found = true;
            try
            {
                using FileStream file = LogTailer.Open(path);
                Scan(file, page, query, minRank, before, after, limit, chunkBytes, scanBytes);
            }
            catch (EndOfStreamException)
            {
                // The file shrank mid-read: the node rotated its log.
                return new LogPage { Found = true, Reset = true };
            }
            return page;
        }

        private static void Scan(FileStream file, LogPage page, string? query, int minRank, long? before, long? after, int limit, int chunkBytes, long scanBytes)
        {
            long length = file.Length;
            long floor = 0;
            if (after is long a)
            {
                if (a > length)
                {
                    page.Reset = true;
                    return;
                }
                floor = Math.Max(0, a);
            }
            long end = Math.Clamp(before ?? length, floor, length);
            page.End = floor;

            byte[] buffer = new byte[(int)Math.Min(chunkBytes, Math.Max(1, end - floor))];
            // Pieces of the line being assembled that lie after the current chunk,
            // nearest the end of the file first.
            List<byte[]> tail = [];
            int tailBytes = 0;
            // Whether a newline has been seen yet. Whatever follows the last one is a
            // line still being written, and is left for a later request.
            bool anchored = false;
            // False while inside a line too long to return.
            bool keep = true;
            // Start of the oldest line dealt with so far.
            long boundary = end;
            long pos = end;
            long scanned = 0;

            while (pos > floor && scanned < scanBytes)
            {
                int n = (int)Math.Min(buffer.Length, pos - floor);
                pos -= n;
                file.Seek(pos, SeekOrigin.Begin);
                file.ReadExactly(buffer, 0, n);
                scanned += n;

                int segmentEnd = n;
                while (true)
                {
                    int newline = buffer.AsSpan(0, segmentEnd).LastIndexOf((byte)'\n');
                    if (newline < 0) break;
                    long lineStart = pos + newline + 1;
                    if (!anchored)
                    {
                        anchored = true;
                        page.End = lineStart;
                    }
                    else if (keep)
                    {
                        Consider(page, lineStart, buffer.AsSpan(newline + 1, segmentEnd - newline - 1), tail, tailBytes, query, minRank);
                    }
                    boundary = lineStart;
                    tail.Clear();
                    tailBytes = 0;
                    keep = true;
                    segmentEnd = newline;
                    if (page.Lines.Count >= limit)
                    {
                        page.Next = boundary > floor ? boundary : null;
                        return;
                    }
                }

                // The rest of the chunk belongs to a line that starts further back.
                if (anchored && keep && segmentEnd > 0)
                {
                    if (tailBytes + segmentEnd > MaxLineBytes)
                    {
                        keep = false;
                        tail.Clear();
                        tailBytes = 0;
                    }
                    else
                    {
                        tail.Add(buffer.AsSpan(0, segmentEnd).ToArray());
                        tailBytes += segmentEnd;
                    }
                }
            }

            if (pos > floor)
            {
                // Out of budget. Continue from the oldest line dealt with, or simply
                // from here if that made no progress.
                page.Next = anchored && boundary < end ? boundary : pos;
                return;
            }
            // Reached the start: what is left over is the first line itself.
            if (anchored && keep && tailBytes > 0) Consider(page, floor, [], tail, tailBytes, query, minRank);
        }

        private static void Consider(LogPage page, long offset, ReadOnlySpan<byte> head, List<byte[]> tail, int tailBytes, string? query, int minRank)
        {
            ReadOnlySpan<byte> line = head;
            if (tailBytes > 0)
            {
                byte[] whole = new byte[head.Length + tailBytes];
                head.CopyTo(whole);
                int at = head.Length;
                for (int i = tail.Count - 1; i >= 0; i--)
                {
                    tail[i].CopyTo(whole, at);
                    at += tail[i].Length;
                }
                line = whole;
            }
            if (line.Length > 0 && line[^1] == (byte)'\r') line = line[..^1];
            if (line.Length == 0) return;

            string level = Level(line);
            if (Array.IndexOf(Levels, level) < minRank) return;

            string raw = Encoding.UTF8.GetString(line);
            if (query != null && !raw.Contains(query, StringComparison.OrdinalIgnoreCase)) return;

            LogLine result = new() { Offset = offset, Level = level, Msg = Clip(raw, MaxMsgChars), Raw = Clip(raw, MaxRawChars) };
            try
            {
                using JsonDocument doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("msg", out JsonElement msg) && msg.ValueKind == JsonValueKind.String)
                        result.Msg = Clip(msg.GetString()!, MaxMsgChars);
                    if (doc.RootElement.TryGetProperty("time", out JsonElement time) && time.ValueKind == JsonValueKind.String
                        && DateTimeOffset.TryParse(time.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed))
                        result.Time = parsed;
                }
            }
            catch (JsonException)
            {
                // Not JSON: show the line as it is.
            }
            page.Lines.Add(result);
        }

        // The line's level without parsing it. Keys are in alphabetical order, so the
        // last "level" is the line's own; quotes inside values are escaped and can't match.
        private static string Level(ReadOnlySpan<byte> line)
        {
            ReadOnlySpan<byte> key = "\"level\":\""u8;
            int at = line.LastIndexOf(key);
            if (at < 0) return "info";
            ReadOnlySpan<byte> value = line[(at + key.Length)..];
            int close = value.IndexOf((byte)'"');
            if (close is < 0 or > 16) return "info";
            string level = Encoding.ASCII.GetString(value[..close]);
            return Array.IndexOf(Levels, level) >= 0 ? level : "info";
        }

        private static string Clip(string text, int max) => text.Length <= max ? text : text[..max] + "…";
    }
}
