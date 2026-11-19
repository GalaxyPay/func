namespace FUNC.Tests;

public class NodeUpdaterTests
{
    [Theory]
    [InlineData("5.0.5", "5.0.4", true)]
    [InlineData("5.1.0", "5.0.12", true)]
    [InlineData("5.0.5", "5.0.5", false)]
    [InlineData("5.0.4", "5.0.5", false)]   // never moves a node backwards
    [InlineData("4.10.0", "4.9.1", true)]   // numeric, not string, comparison
    [InlineData("", "5.0.5", false)]        // latest unknown
    [InlineData("5.0.5", "", false)]        // nothing installed
    public void IsNewer(string latest, string installed, bool expected) =>
        Assert.Equal(expected, NodeUpdater.IsNewer(latest, installed));
}
