// Preview of the participation key badges (Reti, Valar, Expiring), adapted
// from screenshot.mjs. Not for the README.
//
// usage (from webui/): node scripts/mock-badges.mjs <outDir> [--live]
//
// Writes dashboard.png and keys.png to <outDir>; with --live, opens a browser
// window on the mocked dashboard instead and runs until it is closed.
import algosdk from "algosdk";
import fs from "node:fs";
import path from "node:path";
import readline from "node:readline/promises";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright-core";

const webui = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repo = path.resolve(webui, "..");
const outDir = process.argv[2];
const LIVE = process.argv.includes("--live");
const pkgVersion = JSON.parse(
  fs.readFileSync(path.join(webui, "package.json"), "utf8")
).version;

// ---- Version shown in the app bar ----------------------------------------
async function askVersion() {
  return pkgVersion;
  if (!process.stdin.isTTY) {
    console.log(`No version given; using package.json's ${pkgVersion}.`);
    return pkgVersion;
  }
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
  });
  const answer = (
    await rl.question(`Version to show in the screenshot [${pkgVersion}]: `)
  ).trim();
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
const bytes = (n) =>
  Uint8Array.from({ length: n }, () => Math.floor(rnd() * 256));
const b64 = (n) => Buffer.from(bytes(n)).toString("base64");

const DAY_ROUNDS = Math.round(86400 / BLOCK_SEC);
// [manager, days left, label]
const PLAN = [
  [null, 72, "personal, fine"],
  ["reti", 5, "Reti pool (5 days left, not warned)"],
  ["valar", 3, "Valar delegator (3 days left, not warned)"],
  [null, 4, "personal, expiring"],
];
const accts = PLAN.map(([manager, days], i) => {
  const address = new algosdk.Address(bytes(32)).toString();
  const lastValid = LAST + days * DAY_ROUNDS;
  return {
    id: `key${i}`,
    address,
    manager,
    days,
    key: {
      "selection-participation-key": b64(32),
      "vote-participation-key": b64(32),
      "state-proof-key": b64(64),
      "vote-first-valid": lastValid - 30 * DAY_ROUNDS,
      "vote-last-valid": lastValid,
      "vote-key-dilution": 1733,
    },
  };
});
const addr = accts[0].address;
const key = accts[0].key;
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
const accountFor = (a) => ({
  address: a.address,
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
  participation: a.key,
});

// Poisson arrivals over the past year at ~2.3 proposals a day, so only ~10%
// of the block calendar's days are empty.
const PER_DAY = 2.3;
const now = Math.floor(Date.now() / 1000);
const ts = [];
for (let t = now - 365 * 86400; ;) {
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
const supply = {
  current_round: LAST,
  "online-money": 1953000000000000,
  "total-money": 10000000000000000,
};

// Browser storage the page starts with: the block calendar's cache, the
// Blocks Created period, and Lute connected with the participating account
// (Lute resumes a stored session without contacting anything).
const walletAccount = { name: "Lute Account 1", address: addr };
const storage = {
  partProposalsCache: JSON.stringify({
    Algorand: Object.fromEntries(
      accts.map((a) => [a.address, { hwm: LAST, ts }])
    ),
  }),
  resetDates: JSON.stringify([
    { name: "Algorand", date: "snapshot", mode: "month" },
  ]),
  "@txnlab/use-wallet:v5": JSON.stringify({
    wallets: {
      lute: { accounts: [walletAccount], activeAccount: walletAccount },
    },
    activeWallet: "lute",
    activeNetwork: "mainnet",
    customNetworkConfigs: {},
  }),
};

const json = (route, body) =>
  route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify(body),
  });

async function mock(route) {
  const url = new URL(route.request().url());
  const p = url.pathname;
  if (url.hostname === FUNC_HOST) {
    if (url.port === "3536") {
      if (p === "/auth")
        return json(route, { hasPassword: true, signedIn: true });
      if (p === "/goal/version")
        return json(route, {
          installed: NODE_VERSION,
          latest: NODE_VERSION,
          manager: "func",
          updates: {
            mode: "notify",
            lastCheck: null,
            lastError: null,
            lastUpdated: null,
          },
          unsupported: null,
        });
      if (p === "/func/messages") return json(route, []);
      if (p === "/func/version")
        return json(route, {
          installed: VERSION,
          latest: VERSION,
          available: false,
        });
      if (/^\/algorand\/keys$/i.test(p))
        return json(
          route,
          accts.map((a) => ({
            id: a.id,
            address: a.address,
            lastValid: a.key["vote-last-valid"],
            expires: new Date(Date.now() + a.days * 86400 * 1000).toISOString(),
            registered: true,
            manager: a.manager,
            expiringSoon: !a.manager && a.days <= 7,
          }))
        );
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
    if (p === "/v2/participation")
      return json(
        route,
        accts.map((a) => ({ id: a.id, address: a.address, key: a.key }))
      );
    if (p.startsWith("/v2/accounts/")) {
      const a = accts.find((x) => p.includes(x.address)) ?? accts[0];
      return json(route, accountFor(a));
    }
    if (p.includes("/boxes")) return json(route, { boxes: [] });
    if (p.startsWith("/v2/blocks/")) {
      const r = Number(p.split("/").pop());
      const body = algosdk.msgpackRawEncode({
        block: { rnd: r, ts: Math.floor(now - (LAST - r) * BLOCK_SEC) },
      });
      return route.fulfill({
        status: 200,
        contentType: "application/msgpack",
        body: Buffer.from(body),
      });
    }
    return json(route, {});
  }
  if (url.hostname.includes("idx")) {
    if (p === "/health") return json(route, {});
    return json(route, { blocks: [], "current-round": LAST });
  }
  // The latest FUNC release, so Settings shows the version as up to date.
  if (url.hostname === "api.github.com")
    return json(route, { name: `v${VERSION}`, tag_name: `v${VERSION}` });
  return route.continue();
}

// ---- Dev server ----------------------------------------------------------
// vite.config reads the app version from npm_package_version; variables
// already set win over webui/.env files.
process.env.npm_package_version = VERSION;
process.env.VITE_ORIGIN = `http://${FUNC_HOST}:3536`;
const { createServer } = await import("vite");
const server = await createServer({
  root: webui,
  configFile: path.join(webui, "vite.config.mts"),
  logLevel: "warn",
});
await server.listen();
const appUrl = server.resolvedUrls.local[0];

// ---- Capture -------------------------------------------------------------
// Installed Chrome, else Edge; CHROME_PATH picks another Chromium.
async function launch() {
  if (process.env.CHROME_PATH)
    return chromium.launch({ executablePath: process.env.CHROME_PATH });
  try {
    return await chromium.launch({ channel: "chrome" });
  } catch {
    return chromium.launch({ channel: "msedge" });
  }
}

if (LIVE) {
  const browser = await chromium
    .launch({ channel: "chrome", headless: false })
    .catch(() => chromium.launch({ channel: "msedge", headless: false }));
  const ctx = await browser.newContext({ viewport: null });
  await ctx.addInitScript((items) => {
    for (const [k, v] of Object.entries(items)) localStorage.setItem(k, v);
  }, storage);
  await ctx.route("**/*", mock);
  const page = await ctx.newPage();
  await page.goto(appUrl);
  console.log(
    `Live mock at ${appUrl} (in the opened window). Close the window to stop.`
  );
  await new Promise((resolve) => browser.on("disconnected", resolve));
  await server.close();
  process.exit(0);
}

const browser = await launch();
try {
  const ctx = await browser.newContext({
    viewport: { width: 1000, height: 900 },
    deviceScaleFactor: 2,
  });
  await ctx.addInitScript((items) => {
    for (const [k, v] of Object.entries(items)) localStorage.setItem(k, v);
  }, storage);
  await ctx.route("**/*", mock);

  const page = await ctx.newPage();
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto(appUrl);
  await page.waitForLoadState("networkidle").catch(() => {});
  await page.goto(appUrl);
  await page
    .getByText("Participation Keys", { exact: true })
    .waitFor({ timeout: 60000 });
  await page.locator(".v-data-table__td .v-chip").first().waitFor();
  await page.waitForTimeout(3000);
  await page.mouse.move(0, 0);

  fs.mkdirSync(outDir, { recursive: true });
  fs.writeFileSync(
    path.join(outDir, "dashboard.png"),
    await page.screenshot({ fullPage: true })
  );
  const section = page
    .locator("section.node-section")
    .filter({ hasText: "Participation Keys" });
  fs.writeFileSync(path.join(outDir, "keys.png"), await section.screenshot());

  // Hover the Expiring badge for its tooltip.
  await page.locator(".v-chip", { hasText: "Expiring" }).first().hover();
  await page.waitForTimeout(800);
  const box = await section.boundingBox();
  fs.writeFileSync(
    path.join(outDir, "keys-tooltip.png"),
    await page.screenshot({
      clip: {
        x: box.x,
        y: Math.max(0, box.y - 40),
        width: box.width,
        height: box.height + 40,
      },
    })
  );

  // The first key's details, expanded, at phone, laptop and wide widths.
  await page.mouse.move(0, 0);
  await page
    .locator(".v-data-table__tr")
    .first()
    .locator("td")
    .last()
    .locator("button")
    .click();
  await page.waitForTimeout(500);
  for (const width of [420, 1000, 1600]) {
    await page.setViewportSize({ width, height: 900 });
    await page.waitForTimeout(500);
    fs.writeFileSync(
      path.join(outDir, `keys-expanded-${width}.png`),
      await section.screenshot()
    );
  }

  if (errors.length) console.log("Page errors:", errors);
  console.log("Wrote dashboard.png, keys*.png to " + outDir);
} finally {
  await browser.close();
  await server.close();
}
