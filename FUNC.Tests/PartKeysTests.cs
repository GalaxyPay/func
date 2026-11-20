using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

namespace FUNC.Tests;

public class PartKeysTests
{
    // A Reti validator box with the given node's pool app ids filled in.
    private static byte[] RetiBox(int nodeNum, params ulong[] apps)
    {
        byte[] box = new byte[ManagedAccounts.RetiBoxLength];
        for (int k = 0; k < apps.Length; k++)
            BinaryPrimitives.WriteUInt64BigEndian(box.AsSpan(900 + ((nodeNum - 1) * 3 + k) * 8), apps[k]);
        return box;
    }

    [Fact]
    public void RetiPoolApps_ReadsThisNodesPools()
    {
        byte[] box = RetiBox(2, 2714622967);
        BinaryPrimitives.WriteUInt64BigEndian(box.AsSpan(900 + 3 * 3 * 8), 2726614654); // node 4
        Assert.Equal([2714622967UL], ManagedAccounts.RetiPoolApps(box, 2));
        Assert.Equal([2726614654UL], ManagedAccounts.RetiPoolApps(box, 4));
        Assert.Empty(ManagedAccounts.RetiPoolApps(box, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void RetiPoolApps_RejectsNodeOutOfRange(int nodeNum) =>
        Assert.Empty(ManagedAccounts.RetiPoolApps(RetiBox(1, 5), nodeNum));

    [Fact]
    public void RetiPoolApps_RejectsUnknownLayout() =>
        Assert.Empty(ManagedAccounts.RetiPoolApps(new byte[1000], 1));

    [Fact]
    public void GlobalBytes_FindsKeyByName()
    {
        string B64(string s) => Convert.ToBase64String(Encoding.ASCII.GetBytes(s));
        JsonNode app = new JsonObject
        {
            ["params"] = new JsonObject
            {
                ["global-state"] = new JsonArray(
                    new JsonObject { ["key"] = B64("round_end"), ["value"] = new JsonObject { ["type"] = 2, ["uint"] = 5 } },
                    new JsonObject { ["key"] = B64("state"), ["value"] = new JsonObject { ["type"] = 1, ["bytes"] = "BQ==" } }),
            },
        };
        Assert.Equal([0x05], ManagedAccounts.GlobalBytes(app, "state"));
        Assert.Null(ManagedAccounts.GlobalBytes(app, "del_beneficiary"));
        Assert.Null(ManagedAccounts.GlobalBytes(null, "state"));
    }

    private const string Key = """
        {"vote-participation-key":"dm90ZQ==","selection-participation-key":"c2Vs","state-proof-key":"c3A=",
         "vote-first-valid":10,"vote-last-valid":3000010,"vote-key-dilution":1733}
        """;

    [Fact]
    public void IsRegistered_MatchesTheOnlineKey()
    {
        JsonObject key = JsonNode.Parse(Key)!.AsObject();
        Assert.True(PartKeys.IsRegistered(key, JsonNode.Parse(Key)!.AsObject()));
        Assert.False(PartKeys.IsRegistered(key, null));
        Assert.False(PartKeys.IsRegistered(key, JsonNode.Parse(Key.Replace("3000010", "3000011"))!.AsObject()));
        Assert.False(PartKeys.IsRegistered(key, JsonNode.Parse(Key.Replace("c3A=", "eHg="))!.AsObject()));
    }
}
