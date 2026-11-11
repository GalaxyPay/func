using static System.OperatingSystem;

namespace FUNC.Tests;

[Collection("AppData")]
public class UtilsTests
{
    // An absolute path that needs no escaping and doesn't have to exist.
    private static readonly string Abs = IsWindows() ? @"C:\data\nodes" : "/data/nodes";
    private static readonly char Sep = Path.DirectorySeparatorChar;

    [Fact]
    public void AppDataDir_HonorsTheOverride()
    {
        Assert.Equal(TestEnv.DataDir, Utils.appDataDir);
    }

    [Fact]
    public void NodeDataParent_DefaultsToAppDataDir()
    {
        Assert.Equal(TestEnv.DataDir, Utils.NodeDataParent("nodata"));
    }

    [Fact]
    public void NodeDataParent_ReadsTheDataFile()
    {
        string file = Path.Combine(TestEnv.DataDir, "moved.data");
        File.WriteAllText(file, Abs + "\n");
        try
        {
            Assert.Equal(Abs, Utils.NodeDataParent("moved"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ValidateDataParent_AcceptsAnAbsolutePath()
    {
        Assert.Equal(Abs, Utils.ValidateDataParent(Abs));
        Assert.Equal(Abs, Utils.ValidateDataParent($"  {Abs}{Sep} "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateDataParent_RequiresAPath(string? path)
    {
        Assert.Equal("Path is required", Assert.Throws<Exception>(() => Utils.ValidateDataParent(path)).Message);
    }

    [Fact]
    public void ValidateDataParent_RejectsRelativePaths()
    {
        Assert.Equal("Path must be absolute", Assert.Throws<Exception>(() => Utils.ValidateDataParent("data/nodes")).Message);
    }

    [Fact]
    public void ValidateDataParent_RejectsDotSegments()
    {
        var ex = Assert.Throws<Exception>(() => Utils.ValidateDataParent($"{Abs}{Sep}..{Sep}other"));

        Assert.StartsWith("Path must be normalized", ex.Message);
    }

    [Theory]
    [InlineData("$(id)")]
    [InlineData("a;b")]
    [InlineData("a\"b")]
    [InlineData("a'b")]
    [InlineData("a\nb")]
    [InlineData("%TEMP%")]
    public void ValidateDataParent_RejectsShellCharacters(string segment)
    {
        var ex = Assert.Throws<Exception>(() => Utils.ValidateDataParent($"{Abs}{Sep}{segment}"));

        Assert.Equal("Path contains unsupported characters", ex.Message);
    }

    [Fact]
    public void ValidateDataParent_RejectsTheUnixRoot()
    {
        // Unix only: on Windows a drive root such as C:\ is not rejected today.
        if (IsWindows()) return;

        Assert.Equal("Path must not be the root directory", Assert.Throws<Exception>(() => Utils.ValidateDataParent("/")).Message);
    }

    [Fact]
    public void ValidateMnemonic_NormalizesWhitespace()
    {
        string words = string.Join("  \t\n", Enumerable.Repeat("abandon", 25));

        Assert.Equal(string.Join(' ', Enumerable.Repeat("abandon", 25)), Utils.ValidateMnemonic($" {words} "));
    }

    [Theory]
    [InlineData(24)]
    [InlineData(26)]
    public void ValidateMnemonic_RequiresTwentyFiveWords(int count)
    {
        string mnemonic = string.Join(' ', Enumerable.Repeat("abandon", count));

        Assert.Equal("Mnemonic must be 25 words", Assert.Throws<Exception>(() => Utils.ValidateMnemonic(mnemonic)).Message);
    }

    [Theory]
    [InlineData("Abandon")]
    [InlineData("aband0n")]
    [InlineData("a\"b")]
    [InlineData("abandonabandonabandon")]
    public void ValidateMnemonic_RejectsAnythingButLowercaseWords(string word)
    {
        string mnemonic = string.Join(' ', Enumerable.Repeat("abandon", 24).Append(word));

        Assert.Equal("Mnemonic must contain only lowercase words", Assert.Throws<Exception>(() => Utils.ValidateMnemonic(mnemonic)).Message);
    }

    [Fact]
    public void ValidateMnemonic_RequiresAValue()
    {
        Assert.Equal("Mnemonic is required", Assert.Throws<Exception>(() => Utils.ValidateMnemonic(" ")).Message);
    }

    [Fact]
    public void ParseServiceStatus_ReadsTheServiceManagersOutput()
    {
        if (IsWindows())
        {
            Assert.Equal("Running", Utils.ParseServiceStatus("        STATE              : 4  RUNNING "));
            Assert.Equal("Stopped", Utils.ParseServiceStatus("        STATE              : 1  STOPPED "));
            Assert.Equal("Not Found", Utils.ParseServiceStatus("[SC] EnumQueryServicesStatus:OpenService FAILED 1060:"));
            Assert.Equal("Unknown", Utils.ParseServiceStatus("        STATE              : 2  START_PENDING "));
        }
        else if (IsLinux())
        {
            Assert.Equal("Running", Utils.ParseServiceStatus("LoadState=loaded\nActiveState=active\n"));
            Assert.Equal("Stopped", Utils.ParseServiceStatus("LoadState=loaded\nActiveState=inactive\n"));
            Assert.Equal("Not Found", Utils.ParseServiceStatus("LoadState=not-found\nActiveState=inactive\n"));
            Assert.Equal("Unknown", Utils.ParseServiceStatus("LoadState=loaded\nActiveState=failed\n"));
        }
        Assert.Equal("Unknown", Utils.ParseServiceStatus(string.Empty));
    }

    [Fact]
    public void Cap_CapitalizesTheNodeName()
    {
        Assert.Equal("Algorand", Utils.Cap("algorand"));
        Assert.Equal("Voi", Utils.Cap("voi"));
    }
}
