using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace FUNC
{
    // FUNC proxies algod (routes under "ReverseProxy" in appsettings.json) so the web UI
    // can reach a node through FUNC's own origin. The destination host is always
    // localhost; only the port changes, and it always comes from FUNC's own state.
    public static class AlgodProxy
    {
        public const int LegacyVoiPort = 3538;

        // network: the {network} route value, set only by the /algod/{network}/... routes
        // (constrained to algorand|voi). The legacy /v2 and /debug routes have none and
        // pick the node from the port the request was addressed to, as before.
        public static int DestinationPort(string? network, int? hostPort) => network switch
        {
            "algorand" => Shared.AlgoPort,
            "voi" => Shared.VoiPort,
            _ => hostPort == LegacyVoiPort ? Shared.VoiPort : Shared.AlgoPort,
        };

        public static void AddTransform(TransformBuilderContext builderContext)
        {
            builderContext.AddRequestTransform(transformContext =>
            {
                var request = transformContext.HttpContext.Request;
                var network = request.RouteValues["network"] as string;
                var destPort = DestinationPort(network?.ToLowerInvariant(), request.Host.Port);
                var newUri = new UriBuilder(transformContext.DestinationPrefix) { Port = destPort }.Uri;
                transformContext.DestinationPrefix = newUri.ToString();
                return ValueTask.CompletedTask;
            });
        }
    }
}
