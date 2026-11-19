using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using FUNC.Models;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using static System.OperatingSystem;

namespace FUNC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GoalController(ILogger<GoalController> logger) : ControllerBase
    {
        private readonly ILogger<GoalController> _logger = logger;

        // GET: goal/version
        [HttpGet("version")]
        public async Task<ActionResult<GoalVersion>> GoalVersion()
        {
            try
            {
                // An adopted node's binaries come from its package manager or update.sh.
                await Node.SyncExternal();
                ExternalNode? ext = External.Load();
                string installed = await InstalledVersion(ext?.BinDir ?? Path.Combine(Utils.appDataDir, "bin"));

                string latest = string.Empty;
                try { latest = await LatestVersion(); } catch { }

                return new GoalVersion() { Installed = installed, Latest = latest, Manager = ext?.Manager ?? "func" };
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // The version of the algod in binDir ("5.0.5"), or empty if there's none.
        internal static async Task<string> InstalledVersion(string binDir)
        {
            string version = await Utils.Exec(Path.Combine(binDir, "algod"), "-v");
            if (version == string.Empty) return string.Empty;
            int firstBreak = version.IndexOf("\n") + 1;
            string secondLine = version[firstBreak..version.IndexOf("\n", firstBreak)];
            return secondLine[..secondLine.LastIndexOf(".")];
        }

        // The latest node release's version ("5.0.5").
        internal static async Task<string> LatestVersion()
        {
            var (owner, repo) = NodeRepo();
            var client = new GitHubClient(new ProductHeaderValue(repo));
            var latest = await client.Repository.Release.GetLatest(owner, repo);
            return latest.TagName[1..latest.TagName.IndexOf("-")];
        }

        // GET: goal/autoupdate
        [HttpGet("autoupdate")]
        public ActionResult<AutoUpdate> GetAutoUpdate() => NodeUpdater.Status();

        // PUT: goal/autoupdate
        [HttpPut("autoupdate")]
        public ActionResult<AutoUpdate> SetAutoUpdate(AutoUpdateSettings model)
        {
            try
            {
                if (model.Enabled && External.Load() != null)
                    throw new Exception("An existing node is updated by its own installer");
                NodeUpdater.SetEnabled(model.Enabled);
                return NodeUpdater.Status();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: goal/releases
        [HttpGet("releases")]
        public async Task<ActionResult<List<ReleaseOption>>> Releases()
        {
            try
            {
                var (owner, repo) = NodeRepo();
                var client = new GitHubClient(new ProductHeaderValue(repo));
                var releases = await client.Repository.Release.GetAll(owner, repo, new ApiOptions { PageSize = 30, PageCount = 1 });
                var tags = releases.Select(r => r.TagName)
                    .Where(t => t.Contains("stable", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var req = await Consensus.AlgorandRequirement();
                var blockers = await Task.WhenAll(tags.Select(t => Consensus.Blocker(t, req)));
                return tags.Select((t, i) => new ReleaseOption { Name = t, Unsupported = blockers[i] }).ToList();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: goal/update
        [HttpPost("update")]
        public async Task<ActionResult<string>> GoalUpdate(Models.Release model)
        {
            try
            {
                if (External.Load() is { } ext)
                {
                    if (model.Name != "latest") throw new Exception("An existing node can only be updated to the latest version");
                    await External.Update(ext);
                }
                else
                {
                    await Install(model.Name);
                    // An older version would be replaced by the next auto-update.
                    if (model.Name != "latest")
                    {
                        string installed = await InstalledVersion(Path.Combine(Utils.appDataDir, "bin"));
                        string latest = string.Empty;
                        try { latest = await LatestVersion(); } catch { }
                        if (latest.Length == 0 || NodeUpdater.IsNewer(latest, installed)) NodeUpdater.SetEnabled(false);
                    }
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Algorand's release signing key (dev@algorand.com). Only subkeys certified by this
        // primary key are trusted to sign go-algorand releases.
        internal const string AlgorandPrimaryFingerprint = "611DE94A396F01359C72AF56EDACD29DA10A4EA6";

        // The current public key, fetched to pick up signing subkeys added after this build.
        // The embedded keyring keeps expired subkeys, which older releases are signed with.
        private const string AlgorandKeyUrl = "https://releases.algorand.com/key.pub";

        // One install at a time, between manual updates, auto-updates and first-run setup.
        private static readonly SemaphoreSlim installLock = new(1, 1);
        private static volatile bool installing;
        internal static bool Installing => installing;

        // Download a go-algorand release into FUNC's bin dir, restarting FUNC's nodes around it.
        public static async Task Install(string name)
        {
            await installLock.WaitAsync();
            installing = true;
            try { await InstallUnlocked(name); }
            finally
            {
                installing = false;
                installLock.Release();
            }
        }

        private static async Task InstallUnlocked(string name)
        {
            var (owner, repo) = NodeRepo();
            var client = new GitHubClient(new ProductHeaderValue(repo));
            var release = name == "latest"
                ? await client.Repository.Release.GetLatest(owner, repo)
                : await client.Repository.Release.Get(owner, repo, name);
            if (name != "latest" && await Consensus.Blocker(release.TagName, await Consensus.AlgorandRequirement()) is { } why)
                throw new Exception($"{release.TagName}: {why}, so the node would stall");
            var asset = FindNodeAsset(release)
                ?? throw new Exception($"No node binaries for this platform in release {release.TagName}");

            string filePath = Path.Combine(Utils.appDataDir, "node.tar.gz");
            using var httpClient = new HttpClient();
            using (var s = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl))
            using (FileStream fs = new(filePath, System.IO.FileMode.Create))
            {
                await s.CopyToAsync(fs);
            }

            try
            {
                await VerifyNodeDownload(httpClient, release, asset, filePath);
            }
            catch
            {
                System.IO.File.Delete(filePath);
                throw;
            }

            // Get Node Statuses
            var algorandStatus = await Node.Get("algorand");
            var voiStatus = await Node.Get("voi");

            // Stop Running Nodes
            if (algorandStatus.ServiceStatus == "Running")
            {
                await Node.ControlService("algorand", "stop");
            }
            if (voiStatus.ServiceStatus == "Running")
            {
                await Node.ControlService("voi", "stop");
            }

            using FileStream rfs = new(filePath, System.IO.FileMode.Open, FileAccess.Read);
            using GZipStream gz = new(rfs, CompressionMode.Decompress, leaveOpen: true);
            await TarFile.ExtractToDirectoryAsync(gz, Utils.appDataDir, true);
            rfs.Dispose();

            // Restart Running Nodes
            if (algorandStatus.ServiceStatus == "Running")
            {
                await Node.ControlService("algorand", "start");
            }
            if (voiStatus.ServiceStatus == "Running")
            {
                await Node.ControlService("voi", "start");
            }

            System.IO.File.Delete(filePath);
        }

        private static (string Owner, string Repo) NodeRepo() =>
            IsWindows() ? ("GalaxyPay", "go-algo-win") : ("algorand", "go-algorand");

        private static ReleaseAsset? FindNodeAsset(Octokit.Release release)
        {
            if (IsWindows()) return release.Assets.FirstOrDefault(a => a.Name == "node.tar.gz");

            string? prefix = IsLinux() ? $"node_stable_linux-{(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm" : "amd")}64_"
                : IsMacOS() ? "node_stable_darwin-universal_"
                : null;
            return prefix == null ? null
                : release.Assets.FirstOrDefault(a => a.Name.StartsWith(prefix) && a.Name.EndsWith(".tar.gz"));
        }

        // Checks a downloaded node tarball before it's extracted over FUNC's bin dir.
        private static async Task VerifyNodeDownload(HttpClient http, Octokit.Release release, ReleaseAsset asset, string filePath)
        {
            string actual;
            using (FileStream fs = new(filePath, System.IO.FileMode.Open, FileAccess.Read))
            {
                actual = Convert.ToHexString(await SHA256.HashDataAsync(fs));
            }

            ReleaseAsset? Find(string n) => release.Assets.FirstOrDefault(a => a.Name == n);

            if (IsWindows())
            {
                // go-algo-win publishes "<hex>  node.tar.gz" beside the tarball. Releases built
                // before it did have no sidecar; they're installed unchecked, as they always were.
                var shaAsset = Find(asset.Name + ".sha256");
                if (shaAsset == null) return;
                string expected = (await http.GetStringAsync(shaAsset.BrowserDownloadUrl)).Trim().Split(' ', '\t')[0];
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"{asset.Name} failed checksum verification");
                return;
            }

            // Each go-algorand release has a per-platform hashes file listing every artifact's
            // md5/sha256/sha512, with a detached signature by Algorand's release key:
            // node_stable_linux-amd64_5.0.5.tar.gz -> hashes_stable_linux_amd64_5.0.5(.sig)
            string hashesName = "hashes_" + asset.Name["node_".Length..^".tar.gz".Length].Replace('-', '_');
            var hashesAsset = Find(hashesName) ?? throw new Exception($"Release {release.TagName} has no {hashesName}");
            var sigAsset = Find(hashesName + ".sig") ?? throw new Exception($"Release {release.TagName} has no {hashesName}.sig");
            byte[] hashes = await http.GetByteArrayAsync(hashesAsset.BrowserDownloadUrl);
            byte[] sig = await http.GetByteArrayAsync(sigAsset.BrowserDownloadUrl);

            if (!Pgp.Verify(hashes, sig, await AlgorandSigningKeys(http)))
                throw new Exception($"{hashesName} failed signature verification");

            string expectedSha = FindSha256(Encoding.UTF8.GetString(hashes), asset.Name)
                ?? throw new Exception($"{asset.Name} is not listed in {hashesName}");
            if (!string.Equals(expectedSha, actual, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"{asset.Name} failed checksum verification");
        }

        internal static async Task<List<Pgp.SigningKey>> AlgorandSigningKeys(HttpClient http)
        {
            string embedded = Pgp.ReadEmbedded("algorand-release.asc");
            string live = string.Empty;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                live = await http.GetStringAsync(AlgorandKeyUrl, cts.Token);
            }
            catch { }  // the embedded keyring alone covers every release up to this build
            return Pgp.LoadSigningKeys(AlgorandPrimaryFingerprint, embedded, live);
        }

        // Finds the sha256 for `fileName` in a sha*sum-style listing ("<hex>  <name>" per line).
        internal static string? FindSha256(string listing, string fileName)
        {
            foreach (string line in listing.Split('\n'))
            {
                string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && parts[0].Length == 64 && parts[1].TrimStart('*') == fileName)
                    return parts[0];
            }
            return null;
        }
    }
}
