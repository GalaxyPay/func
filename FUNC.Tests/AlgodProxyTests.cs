using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FUNC.Tests;

[Collection("AppData")]
public class AlgodProxyTests
{
    [Theory]
    [InlineData("algorand", null, 1111)]
    [InlineData("voi", null, 2222)]
    [InlineData("algorand", 3538, 1111)] // the path wins over the listener port
    [InlineData("voi", 3537, 2222)]
    [InlineData(null, 3537, 1111)]       // legacy: chosen by port, as before
    [InlineData(null, 3538, 2222)]
    [InlineData(null, 3536, 1111)]
    [InlineData(null, null, 1111)]
    public void DestinationPort_PicksNode(string? network, int? hostPort, int expected)
    {
        var (algo, voi) = (Shared.AlgoPort, Shared.VoiPort);
        try
        {
            Shared.AlgoPort = 1111;
            Shared.VoiPort = 2222;
            Assert.Equal(expected, AlgodProxy.DestinationPort(network, hostPort));
        }
        finally
        {
            (Shared.AlgoPort, Shared.VoiPort) = (algo, voi);
        }
    }

    // A stand-in algod that echoes which node it is and what it was asked for.
    private static async Task<(WebApplication App, int Port)> StartStub(string name)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.Run(ctx => ctx.Response.WriteAsync($"{name} {ctx.Request.Path}{ctx.Request.QueryString}"));
        await app.StartAsync();
        return (app, new Uri(app.Urls.First()).Port);
    }

    // FUNC's proxy, configured from FUNC's own appsettings.json and transform.
    private static async Task<(WebApplication App, HttpClient Client)> StartProxy()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false);
        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .AddTransforms(AlgodProxy.AddTransform);
        var app = builder.Build();
        app.MapReverseProxy();
        await app.StartAsync();
        return (app, new HttpClient { BaseAddress = new Uri(app.Urls.First()) });
    }

    [Fact]
    public async Task Proxy_RoutesByPathAndKeepsLegacyRoutes()
    {
        var (algo, voi) = (Shared.AlgoPort, Shared.VoiPort);
        var (algoStub, algoPort) = await StartStub("algorand");
        var (voiStub, voiPort) = await StartStub("voi");
        var (proxy, client) = await StartProxy();
        try
        {
            Shared.AlgoPort = algoPort;
            Shared.VoiPort = voiPort;

            Assert.Equal("algorand /v2/status", await client.GetStringAsync("/algod/algorand/v2/status"));
            Assert.Equal("voi /v2/status", await client.GetStringAsync("/algod/voi/v2/status"));
            Assert.Equal("voi /debug/settings/config", await client.GetStringAsync("/algod/voi/debug/settings/config"));
            Assert.Equal(
                "voi /v2/status/wait-for-block-after/123?format=msgpack",
                await client.GetStringAsync("/algod/voi/v2/status/wait-for-block-after/123?format=msgpack"));

            // Only the two known networks match; anything else isn't proxied at all.
            Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync("/algod/foo/v2/status")).StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync("/algod/algorand/metrics")).StatusCode);

            // Legacy routes are unchanged: the node is chosen by the Host header's port.
            Assert.Equal("algorand /v2/status", await client.GetStringAsync("/v2/status"));
            var legacyVoi = new HttpRequestMessage(HttpMethod.Get, "/v2/status");
            legacyVoi.Headers.Host = "localhost:3538";
            Assert.Equal("voi /v2/status", await (await client.SendAsync(legacyVoi)).Content.ReadAsStringAsync());
        }
        finally
        {
            (Shared.AlgoPort, Shared.VoiPort) = (algo, voi);
            client.Dispose();
            await proxy.DisposeAsync();
            await algoStub.DisposeAsync();
            await voiStub.DisposeAsync();
        }
    }
}
