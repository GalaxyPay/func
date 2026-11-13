using System.Security.Cryptography;

namespace FUNC.Health
{
    public delegate void LineHandler(ReadOnlySpan<byte> line);

    // Reads the lines added to a log file since the last call. The file is opened for
    // each read and closed again, sharing everything, so algod can keep writing to it
    // and rotate it (rename it away and start a new one) at any time.
    public sealed class LogTailer
    {
        private const int FingerprintBytes = 256;
        private const int MaxLineBytes = 16 * 1024 * 1024;

        private byte[] _buffer = new byte[1024 * 1024];

        // Where the next read starts, and which file that position belongs to. Both
        // can be saved and restored to resume after a restart.
        public long Offset { get; set; }
        public string? Fingerprint { get; set; }

        public static FileStream Open(string path) =>
            new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.SequentialScan);

        // Passes each complete new line to onLine and returns the bytes consumed. Stops
        // at the end of the file, or once maxBytes have been consumed.
        public long Read(string path, LineHandler onLine, long maxBytes)
        {
            using FileStream file = Open(path);
            string? fingerprint = ReadFingerprint(file);
            if (fingerprint == null) return 0;
            // A different first line, or a shorter file, means the log was rotated.
            if (fingerprint != Fingerprint || file.Length < Offset)
            {
                Fingerprint = fingerprint;
                Offset = 0;
            }

            file.Seek(Offset, SeekOrigin.Begin);
            long consumed = 0;
            int filled = 0;
            bool skipping = false;
            while (consumed < maxBytes)
            {
                int read = file.Read(_buffer, filled, _buffer.Length - filled);
                if (read == 0) break;
                filled += read;

                int start = 0;
                while (true)
                {
                    int newline = _buffer.AsSpan(start, filled - start).IndexOf((byte)'\n');
                    if (newline < 0) break;
                    ReadOnlySpan<byte> line = _buffer.AsSpan(start, newline);
                    if (line.Length > 0 && line[^1] == (byte)'\r') line = line[..^1];
                    if (!skipping && line.Length > 0) onLine(line);
                    skipping = false;
                    start += newline + 1;
                }

                if (start > 0)
                {
                    // Keep the unfinished last line for the next chunk.
                    consumed += start;
                    Offset += start;
                    filled -= start;
                    Buffer.BlockCopy(_buffer, start, _buffer, 0, filled);
                }
                else if (filled == _buffer.Length)
                {
                    if (_buffer.Length < MaxLineBytes)
                    {
                        Array.Resize(ref _buffer, _buffer.Length * 2);
                    }
                    else
                    {
                        // No sane line is this long; drop it up to its newline.
                        consumed += filled;
                        Offset += filled;
                        filled = 0;
                        skipping = true;
                    }
                }
            }
            return consumed;
        }

        // Identifies a log file by its first line, which carries a microsecond timestamp.
        // Null while that line is still being written. Creation times can't be used:
        // Windows hands a new file the creation time of the one it replaces.
        private static string? ReadFingerprint(FileStream file)
        {
            Span<byte> head = stackalloc byte[FingerprintBytes];
            file.Seek(0, SeekOrigin.Begin);
            int read = file.ReadAtLeast(head, FingerprintBytes, throwOnEndOfStream: false);
            head = head[..read];
            int newline = head.IndexOf((byte)'\n');
            if (newline >= 0) head = head[..newline];
            else if (read < FingerprintBytes) return null;
            return Convert.ToHexString(SHA256.HashData(head))[..32];
        }
    }
}
