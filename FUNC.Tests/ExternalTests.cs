namespace FUNC.Tests;

public class ExternalTests
{
    private const string ExecStartTail = "; ignore_errors=no ; start_time=[n/a] ; stop_time=[n/a] ; pid=0 ; code=(null) ; status=0/0 }";

    private static string Show(string loadState, string? execStart, string user = "", string environment = "") =>
        $"LoadState={loadState}\n" +
        (execStart == null ? "" : $"ExecStart={{ path={execStart.Split(' ')[0]} ; argv[]={execStart} {ExecStartTail}\n") +
        $"User={user}\n" +
        $"Environment={environment}\n";

    [Fact]
    public void ParseSystemdShow_ReadsThePackagedUnit()
    {
        var unit = External.ParseSystemdShow(Show("loaded", "/usr/bin/algod -d /var/lib/algorand", "algorand"));

        Assert.Equal(("/usr/bin/algod", "/var/lib/algorand", "algorand"), unit);
    }

    [Fact]
    public void ParseSystemdShow_FallsBackToTheEnvironment()
    {
        var unit = External.ParseSystemdShow(Show("loaded", "/home/u/node/algod", environment: "FOO=bar ALGORAND_DATA=/home/u/node/data"));

        Assert.Equal(("/home/u/node/algod", "/home/u/node/data", null), unit);
    }

    [Fact]
    public void ParseSystemdShow_PrefersTheDataDirArgument()
    {
        var unit = External.ParseSystemdShow(Show("loaded", "/home/u/node/algod -d /mnt/data", "u", "ALGORAND_DATA=/home/u/node/data"));

        Assert.Equal("/mnt/data", unit?.DataDir);
    }

    [Fact]
    public void ParseSystemdShow_AllowsAMissingDataDir()
    {
        var unit = External.ParseSystemdShow(Show("loaded", "/usr/bin/algod"));

        Assert.Equal(("/usr/bin/algod", null, null), unit);
    }

    [Fact]
    public void ParseSystemdShow_DoesNotMistakeOtherFlagsForTheDataDir()
    {
        var unit = External.ParseSystemdShow(Show("loaded", "/usr/bin/algod -debug x"));

        Assert.Null(unit?.DataDir);
    }

    [Theory]
    [InlineData("not-found")]
    [InlineData("masked")]
    public void ParseSystemdShow_IgnoresUnitsThatAreNotLoaded(string loadState)
    {
        Assert.Null(External.ParseSystemdShow(Show(loadState, "/usr/bin/algod -d /var/lib/algorand")));
    }

    [Fact]
    public void ParseSystemdShow_IgnoresUnitsWithoutACommand()
    {
        Assert.Null(External.ParseSystemdShow(Show("loaded", null)));
        Assert.Null(External.ParseSystemdShow(string.Empty));
    }
}
