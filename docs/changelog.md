---
description: What's new in each version of FUNC.
outline: [2, 2]
---

# Changelog

The changes you'll notice in each version of FUNC. Releases with only behind-the-scenes changes aren't listed. For every release and its downloads, see [GitHub Releases](https://github.com/GalaxyPay/func/releases).

## 5.5.0

_2026-11-20_

- **Tray icon (Windows and Mac):** shows that FUNC is running and notifies you about new messages, updates and expiring keys. Click it to open the dashboard. See [Keeping Your Node Running](/running).
- **Automatic node updates:** the new **Node Updates** setting can install new node releases for you (**Auto**), alert you (**Notify**, the default) or stay on a version you pick (**Manual**). See [Node updates](/settings#node-updates).
- **Required updates:** FUNC warns you when the network is about to upgrade and your node needs a newer version to keep syncing. Versions that can't follow the network are greyed out.
- **Block alerts:** the dashboard plays a chime and shows the round and reward when one of your accounts creates a block. Turn on **Notify on Blocks Created** to get a desktop notification with the dashboard closed.
- **Expiring keys:** online keys with a week or less left get an **Expiring** badge. Keys looked after by Reti or Valar are badged as such instead.
- **Password reset:** reset a forgotten password from the tray menu or with `--reset-password`. See [Password](/password#forgot-your-password).
- Node downloads on Mac and Linux are now checked against Algorand's signed release hashes before installing.

## 5.4.0

_2026-11-13_

- **Diagnostics** in Settings: copy or download troubleshooting info, including recent warnings and errors, to share when asking for help. See [Getting Help](/support).
- Fixed the network light sometimes showing the wrong status.

## 5.3.0

_2026-11-12_

- **Getting Started** guide in the app for new nodes.
- **Node health charts** over time, under **Show Advanced Details**.
- **Log viewer** with search and live follow.
- New layout for advanced details.
- This documentation site.

## 5.2.1

_2026-11-11_

- Rewards eligibility is now shown on every network.
- Updated Pera Wallet connection.

## 5.2.0

_2026-11-02_

- **Existing nodes (Mac and Linux):** FUNC can manage an Algorand node you installed yourself. See [Existing Nodes](/existing-nodes).
- A hint explains why some features need a secure connection when you open FUNC from another device over HTTP.

## 5.1.0

_2026-10-30_

- **Redesigned dashboard.**

## 5.0.3

_2026-10-17_

- Windows services recover more reliably after a crash or restart.

## 5.0.2

_2026-10-01_

- Defly Wallet is back.

## 5.0.0

_2026-09-25_

- **Valar:** run a Valar service to take on delegated stake. See [Valar](/valar).
- **Password:** FUNC now asks for a password before anyone can use it. See [Password](/password).

## 4.1.7

_2026-09-07_

- Clearer error messages.

## 4.1.6

_2026-09-02_

- Account stats keep loading when an indexer is down, and no longer hold up the rest of the dashboard.

## 4.1.4

_2026-08-25_

- Fixed updating FUNC from the app on Windows, which Microsoft Defender blocked in 4.1.2 and 4.1.3.

## 4.1.2

_2026-07-23_

- Smoother FUNC updates from the app, which now notice when the service has restarted.

## 4.1.0

_2026-07-15_

- **Messages:** announcements about FUNC appear under the bell icon.
- **One-line install and uninstall** scripts for every platform. See [Install](/install).

## 4.0.0

_2026-06-29_

- **Blocks calendar** showing the days your accounts created blocks.
- **Block count presets:** count blocks for all time, this year, this month or today.
- **Block sound:** a chime when one of your accounts creates a block.
- Node services now run under their own service account instead of an administrator account.

## 3.6.0

_2026-05-09_

- Node updates now offer the latest release. Turn on **Manual Node Version Selection** to pick any release instead.
- Removed some wallets that are no longer maintained.

## 3.5.7

_2025-12-08_

- **P2P setting:** choose relays only, P2P only or hybrid networking.
- Going offline no longer asks for confirmation first.

## 3.5.3

_2025-05-27_

- Fixed "failed to fetch" errors when updating the node software.

## 3.5.2

_2025-05-21_

- **Windows on ARM** support.

## 3.5.0

_2025-05-06_

- Count blocks created from a date you choose, instead of all time.

## 3.4.0

_2025-03-07_

- Link from each account to its rewards history on algonoderewards.com.

## 3.3.0

_2025-01-30_

- **Show Machine Name** setting, to tell your nodes apart. 3.3.1 also adds it to the browser tab title.

## 3.2.2

_2025-01-17_

- Dashboard works on phones and small screens.
- Shows why an account isn't eligible for rewards.

## 3.2.0

_2025-01-14_

- Alerts you when a new version of FUNC is available.

## 3.0.3

_2025-01-07_

- **Show Alternative Networks** setting. FUNC shows only Algorand unless you turn it on.

## 3.0.0

_2024-12-31_

- **Node Data Directory:** keep node data wherever you like.
- **Nodely Telemetry:** link your node to Nodely's monitoring dashboard. See [Nodely Telemetry](/telemetry).
- Register a key from its status dot.
- FNet support removed.

## 2.2.0

_2024-12-16_

- **Mac** support.

## 2.1.0

_2024-12-14_

- FUNC can be served over HTTPS.

## 2.0.6

_2024-12-09_

- **Linux on ARM** support.

## 2.0.1

_2024-12-06_

- Open FUNC from other devices on your network. See [Remote Access](/remote-access).

## 2.0.0

_2024-12-06_

- **AvmWinNode is now FUNC**, and runs on **Linux** too.

## 1.3.0

_2024-11-08_

- **Configuration:** change the node's port, DNS bootstrap and P2P settings.
- Choose which node release to install.
- See the node's peers.

## 1.2.1

_2024-11-01_

- Fast catchup progress.

## 1.2.0

_2024-11-01_

- **New interface.**

## 1.1.3

_2024-10-29_

- Generate a participation key without connecting a wallet.

## 1.1.0

_2024-10-24_

- **Reti:** run a Reti pool service. See [Reti](/reti).
- FNet network support.

## 1.0.2

_2024-10-21_

- Kibisis wallet support.
- Copy a participation key's details to the clipboard.
- The app opens in your browser after installing.

## 1.0.0

_2024-10-17_

- First release, as **AvmWinNode**: a Windows service for running an Algorand or Voi node and participating in consensus.
