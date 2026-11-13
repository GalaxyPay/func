# Dashboard screenshot

The README and the docs home page show the same dashboard screenshot:

- `assets/screenshot.png`
- `docs/public/screenshot.png`

It shows **fake but realistic data**, never a real node or a real person's address. [screenshot.mjs](screenshot.mjs) produces both files.

## Before you start: which version?

**Ask which FUNC version the screenshot should show.** It's usually the version about to be released, which can be ahead of `webui/package.json`. The version appears in the app bar, and Settings reports it as up to date.

If you're running the script yourself, it asks for the version when you don't pass one. Press Enter to use the version in `package.json`.

## Run it

From `webui/`:

```sh
pnpm install
pnpm screenshot           # asks for the version
pnpm screenshot 5.3.0     # or pass it
```

The script:

1. starts this branch's UI on a Vite dev server, set to show that version
2. opens it in your installed Chrome, or Edge if Chrome is missing, without a visible window. Set `CHROME_PATH` to use another Chromium.
3. answers every request the UI makes with mock data, so no FUNC service, node, password or network is needed
4. writes both PNGs: 1000px wide at 2x, full page, dark theme

It prints the version shown and the wallet button's text, so you can confirm both without opening the image. Look at the image before committing it anyway.

## What the picture shows

- **Status:** Node "Running, synced", Consensus "1 account voting", Network "6 peers"
- **Overview:** block 67,214,583, Blocks Created counted for this month, 1 online account with ~96,415 ALGO
- **Participation Keys:** one online key, for an address generated from a fixed seed (`SWC6VMW...PGGFNVM`)
- **Wallet:** Lute connected with that same account
- **Block calendar:** ~842 blocks over the past year
- **Settings:** FUNC and the node up to date; no unread messages, no Reti or Valar

Everything comes from the deterministic random generator, so reruns give the same picture apart from dates. Change the constants at the top of the mock-data section to vary it.

## When the dashboard changes

The mocks only cover the calls the dashboard made when the script was last updated. If a new call isn't mocked, it gets an empty `{}`, and the affected panel shows "Checking..." or a dash instead of data. To fix that:

- **Find the call:** look for it in the browser's network tab, or add `page.on("request", ...)` logging.
- **Mock it:** add a branch for it in `mock()`. The response shape is in `src/types.d.ts` for FUNC's API, or in algosdk's models for algod.
- **Browser storage:** if a new feature reads it, such as a dismissed banner or a remembered setting, add the key to `storage`.

Two things to know:

- The wallet is restored from use-wallet's `@txnlab/use-wallet:v5` key. If use-wallet changes that key or its format, the wallet button goes back to "Connect Wallet".
- The UI's API origin is set to the made-up host `func.mock`, so the script ignores `webui/.env.development.local` and can't reach a real FUNC service.
