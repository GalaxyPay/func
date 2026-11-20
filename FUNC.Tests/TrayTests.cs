using System.Buffers.Binary;
using System.Text;

namespace FUNC.Tests;

public class TrayTests
{
    // ABI-encodes (string,string): two uint16 head offsets, then length-prefixed UTF-8.
    private static byte[] EncodeTuple(string a, string b)
    {
        byte[] ab = Encoding.UTF8.GetBytes(a), bb = Encoding.UTF8.GetBytes(b);
        byte[] result = new byte[4 + 2 + ab.Length + 2 + bb.Length];
        int offA = 4, offB = 4 + 2 + ab.Length;
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(0), (ushort)offA);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(2), (ushort)offB);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(offA), (ushort)ab.Length);
        ab.CopyTo(result, offA + 2);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(offB), (ushort)bb.Length);
        bb.CopyTo(result, offB + 2);
        return result;
    }

    private static byte[] Key(ulong id)
    {
        byte[] key = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(key, id);
        return key;
    }

    [Fact]
    public void DecodeMessage_ReadsIdAndTitle()
    {
        var message = Tray.DecodeMessage(Key(42), EncodeTuple("Upgrade soon ✓", "Body text"));
        Assert.NotNull(message);
        Assert.Equal(42UL, message.Id);
        Assert.Equal("Upgrade soon ✓", message.Title);
        Assert.Equal("Body text", message.Body);
    }

    [Fact]
    public void DecodeMessage_SkipsAddressKeys() =>
        Assert.Null(Tray.DecodeMessage(new byte[32], EncodeTuple("a", "b")));

    [Fact]
    public void TaskXml_IsWellFormedWithEscapedCommand()
    {
        string exe = @"C:\Program Files\R&D\FUNC\FUNC.Tray.exe";
        var doc = System.Xml.Linq.XDocument.Parse(TrayLauncher.TaskXml(exe));
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        Assert.Equal(exe, doc.Descendants(ns + "Command").Single().Value);
        Assert.Equal("S-1-5-32-545", doc.Descendants(ns + "GroupId").Single().Value);
    }

    [Theory]
    [InlineData("{\"blocks\":true}", true)]
    [InlineData("{\"blocks\":false}", false)]
    [InlineData("{}", false)]
    public void Notifications_ParsesTheSavedSetting(string json, bool blocks) =>
        Assert.Equal(blocks, Notifications.Parse(json).Blocks);

    [Fact]
    public void DecodeMessage_SkipsMalformedValues() =>
        Assert.Null(Tray.DecodeMessage(Key(1), [0, 200, 0, 0]));
}
