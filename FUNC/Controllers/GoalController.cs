using System.Diagnostics;
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

                Task<string?> unsupported = Consensus.InstalledBlocker();
                string latest = string.Empty;
                try { latest = await LatestVersion(); } catch { }

                return new GoalVersion()
                {
                    Installed = installed,
                    Latest = latest,
                    Manager = ext?.Manager ?? "func",
                    Updates = NodeUpdater.Status(),
                    Unsupported = await unsupported,
                };
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // The version of the algod in binDir ("5.0.5"), or empty if there's none.
        internal static async Task<string> InstalledVersion(string binDir)
        {
            // FUNC's own algod is being replaced; running it would lock the file (callers treat
            // an empty version as unknown).
            if (Installing && Path.GetFullPath(binDir) == Path.GetFullPath(Path.Combine(Utils.appDataDir, "bin")))
                return string.Empty;
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

        // PUT: goal/updates
        [HttpPut("updates")]
        public ActionResult<NodeUpdates> SetUpdates(NodeUpdatesSettings model)
        {
            try
            {
                if (model.Mode != NodeUpdateMode.Notify && External.Load() != null)
                    throw new Exception("An existing node is updated by its own installer");
                NodeUpdater.SetMode(model.Mode);
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
                var missing = await Task.WhenAll(tags.Select(t => Consensus.Missing(t, req)));
                return ReleaseOptions(tags, missing);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Newest version first, ending at the newest release that can't follow the network:
        // protocol support only grows from release to release, so every older one can't
        // either, and listing them all would bury the ones that can be installed.
        internal static List<ReleaseOption> ReleaseOptions(List<string> tags, string?[] missing)
        {
            List<ReleaseOption> options = [];
            foreach (var (tag, lacks) in tags.Zip(missing).OrderByDescending(r => TagVersion(r.First)))
            {
                if (lacks != null)
                {
                    options.Add(new ReleaseOption { Name = tag, Unsupported = $"This and earlier versions don't support {lacks}" });
                    break;
                }
                options.Add(new ReleaseOption { Name = tag });
            }
            return options;
        }

        // "v4.0.1-stable" → 4.0.1. Unparseable tags sort last.
        private static Version TagVersion(string tag)
        {
            int dash = tag.IndexOf('-');
            return Version.TryParse(tag[(tag.StartsWith('v') ? 1 : 0)..(dash < 0 ? tag.Length : dash)], out var v) ? v : new Version();
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
                    // Picking an older version means staying on it: no auto-update replacing it,
                    // no alerts nagging about it.
                    if (model.Name != "latest" && NodeUpdater.Mode != NodeUpdateMode.Manual)
                    {
                        string installed = await InstalledVersion(Path.Combine(Utils.appDataDir, "bin"));
                        string latest = string.Empty;
                        try { latest = await LatestVersion(); } catch { }
                        if (latest.Length == 0 || NodeUpdater.IsNewer(latest, installed)) NodeUpdater.SetMode(NodeUpdateMode.Manual);
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

        // Set at startup; Install is static and runs from requests, the auto-updater and first-run setup.
        internal static ILogger? Log { get; set; }

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
            string staging = Path.Combine(Utils.appDataDir, "node-staging");
            var total = Stopwatch.StartNew();
            var step = Stopwatch.StartNew();
            void Done(string what)
            {
                Log?.LogInformation("Node install {Tag}: {Step} took {Ms} ms", release.TagName, what, step.ElapsedMilliseconds);
                step.Restart();
            }

            try
            {
                using var httpClient = new HttpClient();
                using (var s = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl))
                using (FileStream fs = new(filePath, System.IO.FileMode.Create))
                {
                    await s.CopyToAsync(fs);
                }
                Done("download");

                await VerifyNodeDownload(httpClient, release, asset, filePath);
                Done("verify");

                // Unpack beside the live install first, so the nodes are only down for the swap.
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                using (FileStream rfs = new(filePath, System.IO.FileMode.Open, FileAccess.Read))
                using (GZipStream gz = new(rfs, CompressionMode.Decompress))
                {
                    await TarFile.ExtractToDirectoryAsync(gz, staging, true);
                }
                Done("extract");

                var running = new List<string>();
                foreach (string node in new[] { "algorand", "voi" })
                {
                    if (await Node.ServiceStatus(node) == "Running") running.Add(node);
                }
                foreach (string node in running) await Node.ControlService(node, "stop");
                Done("stop nodes");

                try
                {
                    await ReplaceFiles(staging, Utils.appDataDir);
                    Done("replace files");
                }
                finally
                {
                    // Restart even when the swap failed: ReplaceFiles has put the old files back.
                    foreach (string node in running)
                    {
                        try { await Node.ControlService(node, "start"); }
                        catch (Exception ex) { Log?.LogWarning("Node install: restarting {Node} failed: {Message}", node, ex.Message); }
                    }
                    Done("start nodes");
                }

                Log?.LogInformation("Node install {Tag} finished in {Ms} ms", release.TagName, total.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Log?.LogWarning("Node install {Tag} failed after {Ms} ms: {Message}", release.TagName, total.ElapsedMilliseconds, ex.Message);
                throw;
            }
            finally
            {
                TryDelete(filePath);
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
            }
        }

        // Moves every file under `staging` to the same relative path under `target`.
        //
        // Windows won't overwrite a running .exe (e.g. a goal or diagcfg a status check just
        // started), but it will rename one, so each existing file is first moved aside to
        // "<name>.func-old". If any step fails, the files already replaced are put back, so
        // the node is never left with a mix of two versions' binaries.
        internal static async Task ReplaceFiles(string staging, string target, int attempts = 10)
        {
            var replaced = new List<(string Dest, string? Aside)>();
            try
            {
                foreach (string src in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
                {
                    string dest = Path.Combine(target, Path.GetRelativePath(staging, src));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    string? aside = null;
                    if (System.IO.File.Exists(dest))
                    {
                        aside = AsideName(dest);
                        await Retry(() => System.IO.File.Move(dest, aside), attempts);
                    }
                    replaced.Add((dest, aside));
                    await Retry(() => System.IO.File.Move(src, dest), attempts);
                }
            }
            catch
            {
                foreach (var (dest, aside) in Enumerable.Reverse(replaced))
                {
                    try
                    {
                        if (aside != null) System.IO.File.Move(aside, dest, overwrite: true);
                        else TryDelete(dest);
                    }
                    catch { }
                }
                throw;
            }

            // Old binaries still running can't be deleted yet; the next install clears them.
            foreach (string dir in replaced.Select(r => Path.GetDirectoryName(r.Dest)!).Distinct())
            {
                foreach (string old in Directory.EnumerateFiles(dir, "*.func-old*")) TryDelete(old);
            }
        }

        // "<dest>.func-old", or a unique variant when a leftover one is still in use.
        private static string AsideName(string dest)
        {
            string aside = dest + ".func-old";
            if (System.IO.File.Exists(aside)) TryDelete(aside);
            return System.IO.File.Exists(aside) ? $"{aside}-{Guid.NewGuid():N}" : aside;
        }

        // File operations can briefly fail while antivirus scans a new file or a process exits.
        private static async Task Retry(Action action, int attempts)
        {
            for (int i = 1; ; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex) when (i < attempts && ex is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(300);
                }
            }
        }

        private static void TryDelete(string path)
        {
            try { System.IO.File.Delete(path); } catch { }
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
