using System.Text;

namespace FUNC.Tests;

public class AlgoAddressTests
{
    [Theory]
    [InlineData("", "c672b8d1ef56ed28ab87c3622c5114069bdd3ad7b8f9737498d0c01ecef0967a")]
    [InlineData("abc", "53048e2681941ef99b2e29b76b4c7dabe4c2d0c634fc6d46e0e2f13107e7af23")]
    [InlineData("abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu",
        "3928e184fb8690f840da3988121d31be65cb9d3ef83ee6146feac861e19b563a")]
    public void Sha512_256_MatchesFipsVectors(string input, string hex) =>
        Assert.Equal(hex, Convert.ToHexStringLower(AlgoAddress.Sha512_256(Encoding.ASCII.GetBytes(input))));

    // Reference from algosdk: getApplicationAddress(2714516089).
    [Fact]
    public void AppKeyHex_MatchesAlgosdk() =>
        Assert.Equal("ee356c69b3ddece8ac626fef79710e55ed93e744a5a9f1d01ee8f028b171eb55", AlgoAddress.AppKeyHex(2714516089));

    [Fact]
    public void KeyHex_DecodesAnAddress() =>
        Assert.Equal(AlgoAddress.AppKeyHex(2714516089), AlgoAddress.KeyHex("5Y2WY2NT3XWORLDCN7XXS4IOKXWZHZ2EUWU7DUA65DYCRMLR5NK4KADA6Y"));

    [Theory]
    [InlineData("5Y2WY2NT3XWORLDCN7XXS4IOKXWZHZ2EUWU7DUA65DYCRMLR5NK4KADA6A")] // bad checksum
    [InlineData("5Y2WY2NT3XWORLDCN7XXS4IOKXWZHZ2EUWU7DUA65DYCRMLR5NK4KADA")]
    [InlineData("not an address")]
    [InlineData(null)]
    public void KeyHex_RejectsMalformed(string? address) => Assert.Null(AlgoAddress.KeyHex(address));
}
