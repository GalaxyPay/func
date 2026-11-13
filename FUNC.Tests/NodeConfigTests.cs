using FUNC.Models;
using Newtonsoft.Json.Linq;

namespace FUNC.Tests;

[Collection("AppData")]
public class NodeConfigTests
{
    private const string Name = "algorand";
    private readonly string _dir = TestEnv.ResetNodeDir(Name);

    private string ConfigPath => Path.Combine(_dir, "config.json");

    private void Existing(string json) => File.WriteAllText(ConfigPath, json);

    private static Task Set(string json) => Node.SetConfig(Name, new Config { Json = json });

    private JObject Saved() => JObject.Parse(File.ReadAllText(ConfigPath));

    [Fact]
    public async Task SetConfig_WritesEditableKeys()
    {
        Existing("""{"DNSBootstrapID":"mainnet.algonode.org","EndpointAddress":"0.0.0.0:8081"}""");

        await Set("""
            {
              "DNSBootstrapID": "example.org",
              "EndpointAddress": "127.0.0.1:9000",
              "BaseLoggerDebugLevel": 5,
              "EnableP2P": true,
              "EnableP2PHybridMode": false
            }
            """);

        JObject saved = Saved();
        Assert.Equal("example.org", saved.Value<string>("DNSBootstrapID"));
        Assert.Equal("127.0.0.1:9000", saved.Value<string>("EndpointAddress"));
        Assert.Equal(5, saved.Value<int>("BaseLoggerDebugLevel"));
        Assert.True(saved.Value<bool>("EnableP2P"));
        Assert.False(saved.Value<bool>("EnableP2PHybridMode"));
    }

    [Fact]
    public async Task SetConfig_KeepsUnchangedOtherKeys()
    {
        Existing("""{"Archival":true,"GossipFanout":8,"EndpointAddress":"0.0.0.0:8081"}""");

        await Set("""{"Archival":true,"GossipFanout":8,"EndpointAddress":"0.0.0.0:8082"}""");

        JObject saved = Saved();
        Assert.True(saved.Value<bool>("Archival"));
        Assert.Equal(8, saved.Value<int>("GossipFanout"));
        Assert.Equal("0.0.0.0:8082", saved.Value<string>("EndpointAddress"));
    }

    [Fact]
    public async Task SetConfig_CreatesFileWhenMissing()
    {
        await Set("""{"EndpointAddress":"localhost:8081"}""");

        Assert.Equal("localhost:8081", Saved().Value<string>("EndpointAddress"));
    }

    [Fact]
    public async Task SetConfig_AllowsRemovingAnEditableKey()
    {
        Existing("""{"BaseLoggerDebugLevel":5,"EndpointAddress":"0.0.0.0:8081"}""");

        await Set("""{"EndpointAddress":"0.0.0.0:8081"}""");

        Assert.Null(Saved()["BaseLoggerDebugLevel"]);
    }

    [Theory]
    [InlineData("""{"Archival":true}""", """{"Archival":false}""")] // changed
    [InlineData("""{}""", """{"EnableMetricReporting":true}""")] // added
    [InlineData("""{"Archival":true}""", """{}""")] // removed
    [InlineData("""{"Nested":{"a":1}}""", """{"Nested":{"a":2}}""")] // changed inside an object
    public async Task SetConfig_RejectsChangesToOtherKeys(string existing, string submitted)
    {
        Existing(existing);

        var ex = await Assert.ThrowsAsync<Exception>(() => Set(submitted));

        Assert.Contains("is not supported", ex.Message);
        Assert.Equal(existing, File.ReadAllText(ConfigPath));
    }

    [Theory]
    [InlineData("""{"EndpointAddress":"example.com:8081"}""")]
    [InlineData("""{"EndpointAddress":"0.0.0.0"}""")]
    [InlineData("""{"EndpointAddress":"0.0.0.0:0"}""")]
    [InlineData("""{"EndpointAddress":"0.0.0.0:70000"}""")]
    [InlineData("""{"EndpointAddress":"0.0.0.0:8081 -x"}""")]
    [InlineData("""{"EndpointAddress":8081}""")]
    [InlineData("""{"BaseLoggerDebugLevel":7}""")]
    [InlineData("""{"BaseLoggerDebugLevel":-1}""")]
    [InlineData("""{"BaseLoggerDebugLevel":"4"}""")]
    [InlineData("""{"EnableP2P":"true"}""")]
    [InlineData("""{"EnableP2PHybridMode":1}""")]
    [InlineData("""{"DNSBootstrapID":5}""")]
    [InlineData("""{"DNSBootstrapID":"a\nb"}""")]
    public async Task SetConfig_RejectsInvalidValues(string submitted)
    {
        Existing("{}");

        await Assert.ThrowsAsync<Exception>(() => Set(submitted));

        Assert.Equal("{}", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public async Task SetConfig_RejectsOverlongBootstrapId()
    {
        string id = new('a', 513);

        await Assert.ThrowsAsync<Exception>(() => Set($$"""{"DNSBootstrapID":"{{id}}"}"""));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    public async Task SetConfig_RejectsNonObjects(string submitted)
    {
        var ex = await Assert.ThrowsAsync<Exception>(() => Set(submitted));

        Assert.Equal("Config must be a JSON object", ex.Message);
    }

    [Fact]
    public async Task SetConfig_RejectsDuplicateKeys()
    {
        // Otherwise a second copy of a key could slip past the comparison with the file.
        await Assert.ThrowsAnyAsync<Exception>(() => Set("""{"EnableP2P":true,"EnableP2P":false}"""));

        Assert.False(File.Exists(ConfigPath));
    }

    [Theory]
    [InlineData("""{"EndpointAddress":"0.0.0.0:8081"}""", null, 8081)]
    [InlineData("""{"EndpointAddress":"localhost:4190"}""", "127.0.0.1:9999", 4190)]
    [InlineData("""{"EndpointAddress":"127.0.0.1:0"}""", "127.0.0.1:54321\n", 54321)]
    [InlineData("""{}""", "[::]:8080", 8080)]
    [InlineData(null, "127.0.0.1:8080", 8080)]
    [InlineData("not json", "127.0.0.1:8080", 8080)]
    [InlineData("""{"EndpointAddress":"127.0.0.1:0"}""", null, 0)]
    [InlineData(null, null, 0)]
    public void AlgodPort_ReadsConfigThenAlgodNet(string? config, string? algodNet, int expected)
    {
        if (config != null) Existing(config);
        if (algodNet != null) File.WriteAllText(Path.Combine(_dir, "algod.net"), algodNet);

        Assert.Equal(expected, Node.AlgodPort(Name));
    }

    [Fact]
    public void AlgodToken_IsTrimmed()
    {
        File.WriteAllText(Path.Combine(_dir, "algod.admin.token"), "abc123\n");

        Assert.Equal("abc123", Node.AlgodToken(Name));
    }

    [Fact]
    public void AlgodToken_IsEmptyWithoutAFile()
    {
        Assert.Equal(string.Empty, Node.AlgodToken(Name));
    }
}
