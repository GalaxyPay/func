// Takes the dashboard screenshot used by the README and the docs site, with
// fake but realistic data. See screenshot.md beside this file.
//
// usage (from webui/): pnpm screenshot [version]
//
// Starts this branch's UI on a Vite dev server showing [version] (asked for
// when not given), answers every API call it makes with mock data, and writes
// assets/screenshot.png and docs/public/screenshot.png.
import algosdk from "algosdk";
import fs from "node:fs";
import path from "node:path";
import readline from "node:readline/promises";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright-core";

const webui = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repo = path.resolve(webui, "..");
const outputs = [path.join(repo, "assets/screenshot.png"), path.join(repo, "docs/public/screenshot.png")];
const pkgVersion = JSON.parse(fs.readFileSync(path.join(webui, "package.json"), "utf8")).version;

// ---- Version shown in the app bar ----------------------------------------
async function askVersion() {
  const arg = process.argv[2];
  if (arg) return arg.replace(/^v/, "");
  if (!process.stdin.isTTY) {
    console.log(`No version given; using package.json's ${pkgVersion}.`);
    return pkgVersion;
  }
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  const answer = (await rl.question(`Version to show in the screenshot [${pkgVersion}]: `)).trim();
  rl.close();
  return answer.replace(/^v/, "") || pkgVersion;
}
const VERSION = await askVersion();

// ---- Mock data -----------------------------------------------------------
// One online account (~96k ALGO), healthy node voting, telemetry on, no
// Reti/Valar, no unread messages, FUNC and the node up to date.
const FUNC_HOST = "func.mock"; // the UI's API origin, so every call is ours
const LAST = 67214583;
const STAKE = 96415208000; // microAlgos
const BLOCK_SEC = 2.8;
const NODE_VERSION = "v4.3.1-stable";

// Deterministic randomness so reruns give the same picture.
let seed = 42;
const rnd = () => (seed = (seed * 1103515245 + 12345) % 2 ** 31) / 2 ** 31;
const bytes = (n) => Uint8Array.from({ length: n }, () => Math.floor(rnd() * 256));
const b64 = (n) => Buffer.from(bytes(n)).toString("base64");

const addr = new algosdk.Address(bytes(32)).toString();
const key = {
  "selection-participation-key": b64(32),
  "vote-participation-key": b64(32),
  "state-proof-key": b64(64),
  "vote-first-valid": LAST - 412000,
  "vote-last-valid": LAST - 412000 + 3000000,
  "vote-key-dilution": 1733,
};
const status = () => ({
  "catchup-time": 0,
  "last-round": LAST,
  "last-version": "https://github.com/algorandfoundation/specs/tree/v1",
  "next-version": "https://github.com/algorandfoundation/specs/tree/v1",
  "next-version-round": LAST + 1,
  "next-version-supported": true,
  "stopped-at-unsupported-round": false,
  "time-since-last-round": 1200000000,
});
const account = {
  address: addr,
  amount: STAKE,
  "amount-without-pending-rewards": STAKE,
  "min-balance": 100000,
  "pending-rewards": 0,
  rewards: 0,
  round: LAST,
  status: "Online",
  "incentive-eligible": true,
  "total-apps-opted-in": 0,
  "total-assets-opted-in": 0,
  "total-created-apps": 0,
  "total-created-assets": 0,
  participation: key,
};

// Poisson arrivals over the past year at ~2.3 proposals a day, so only ~10%
// of the block calendar's days are empty.
const PER_DAY = 2.3;
const now = Math.floor(Date.now() / 1000);
const ts = [];
for (let t = now - 365 * 86400; ; ) {
  t += Math.floor((-Math.log(1 - rnd()) * 86400) / PER_DAY);
  if (t >= now) break;
  ts.push(t);
}

// A node up for nine days, well connected and voting.
const iso = (secAgo) => new Date((now - secAgo) * 1000).toISOString();
const health = {
  source: "ok",
  metrics: true,
  version: NODE_VERSION.replace(/^v|-stable$/g, ""),
  startedAt: iso(9 * 86400 + 4 * 3600),
  lastRound: LAST,
  lastRoundAt: iso(1),
  lastVoteAt: iso(8),
  peersIn: 0,
  peersOut: 6,
  bucketSeconds: 300,
  points: [],
  peers: [],
  peersAt: iso(5),
  notes: [],
};
const supply = { current_round: LAST, "online-money": 1953000000000000, "total-money": 10000000000000000 };

// Browser storage the page starts with: the block calendar's cache, the
// Blocks Created period, and Lute connected with the participating account
// (Lute resumes a stored session without contacting anything).
const walletAccount = { name: "Lute Account 1", address: addr };
const storage = {
  partProposalsCache: JSON.stringify({ Algorand: { [addr]: { hwm: LAST, ts } } }),
  resetDates: JSON.stringify([{ name: "Algorand", date: "snapshot", mode: "month" }]),
  "@txnlab/use-wallet:v5": JSON.stringify({
    wallets: { lute: { accounts: [walletAccount], activeAccount: walletAccount } },
    activeWallet: "lute",
    activeNetwork: "mainnet",
    customNetworkConfigs: {},
  }),
};

const json = (route, body) =>
  route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(body) });

async function mock(route) {
  const url = new URL(route.request().url());
  const p = url.pathname;
  if (url.hostname === FUNC_HOST) {
    if (url.port === "3536") {
      if (p === "/auth") return json(route, { hasPassword: true, signedIn: true });
      if (p === "/goal/version") return json(route, { installed: NODE_VERSION, latest: NODE_VERSION, manager: "func" });
      if (/^\/algorand\/health$/i.test(p)) return json(route, health);
      if (/^\/algorand$/i.test(p))
        return json(route, {
          machineName: "NODE-01",
          isWindows: true,
          serviceStatus: "Running",
          startsAtBoot: true,
          port: 8080,
          token: "a".repeat(64),
          telemetryStatus: "Telemetry enabled: name = NODE-01, GUID = 1234",
        });
      return json(route, {});
    }
    // algod, on the node's port
    if (p === "/v2/status") return json(route, status());
    if (p.startsWith("/v2/status/wait-for-block-after")) {
      await new Promise((r) => setTimeout(r, 60000));
      return json(route, status());
    }
    if (p === "/v2/ledger/supply") return json(route, supply);
    if (p === "/v2/participation") return json(route, [{ id: "abc123", address: addr, key }]);
    if (p.startsWith("/v2/accounts/")) return json(route, account);
    if (p.includes("/boxes")) return json(route, { boxes: [] });
    if (p.startsWith("/v2/blocks/")) {
      const r = Number(p.split("/").pop());
      const body = algosdk.msgpackRawEncode({ block: { rnd: r, ts: Math.floor(now - (LAST - r) * BLOCK_SEC) } });
      return route.fulfill({ status: 200, contentType: "application/msgpack", body: Buffer.from(body) });
    }
    return json(route, {});
  }
  if (url.hostname.includes("idx")) {
    if (p === "/health") return json(route, {});
    return json(route, { blocks: [], "current-round": LAST });
  }
  // The latest FUNC release, so Settings shows the version as up to date.
  if (url.hostname === "api.github.com") return json(route, { name: `v${VERSION}`, tag_name: `v${VERSION}` });
  return route.continue();
}

// ---- Dev server ----------------------------------------------------------
// vite.config reads the app version from npm_package_version; variables
// already set win over webui/.env files.
process.env.npm_package_version = VERSION;
process.env.VITE_ORIGIN = `http://${FUNC_HOST}:3536`;
const { createServer } = await import("vite");
const server = await createServer({ root: webui, configFile: path.join(webui, "vite.config.mts"), logLevel: "warn" });
await server.listen();
const appUrl = server.resolvedUrls.local[0];

// ---- Capture -------------------------------------------------------------
// Installed Chrome, else Edge; CHROME_PATH picks another Chromium.
async function launch() {
  if (process.env.CHROME_PATH) return chromium.launch({ executablePath: process.env.CHROME_PATH });
  try {
    return await chromium.launch({ channel: "chrome" });
  } catch {
    return chromium.launch({ channel: "msedge" });
  }
}

const browser = await launch();
try {
  const ctx = await browser.newContext({ viewport: { width: 1000, height: 600 }, deviceScaleFactor: 2 });
  await ctx.addInitScript((items) => {
    for (const [k, v] of Object.entries(items)) localStorage.setItem(k, v);
  }, storage);
  await ctx.route("**/*", mock);

  const page = await ctx.newPage();
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  // A cold dev server optimizes dependencies on the first load and reloads
  // the page, so load once to warm it up, then load for real.
  await page.goto(appUrl);
  await page.waitForLoadState("networkidle").catch(() => {});
  await page.goto(appUrl);
  await page.getByText("Participation Keys", { exact: true }).waitFor({ timeout: 60000 });
  await page.locator(".v-data-table__td .font-mono").first().waitFor();
  await page.waitForTimeout(3000);
  await page.mouse.move(0, 0);

  const wallet = (await page.locator(".wallet-btn").innerText()).trim();
  const shown = (await page.locator(".brand-version").innerText()).trim();
  const png = await page.screenshot({ fullPage: true });
  for (const out of outputs) fs.writeFileSync(out, png);

  console.log(`Version shown: ${shown}`);
  console.log(`Wallet button: ${wallet}`);
  console.log(`Block calendar: ${ts.length} blocks in the last year`);
  if (errors.length) console.log("Page errors:", errors);
  console.log("Wrote:\n  " + outputs.map((o) => path.relative(repo, o)).join("\n  "));
} finally {
  await browser.close();
  await server.close();
}
