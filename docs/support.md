# Getting Help

The best place to ask for help is the **FUNC channel** of the [Lute Discord](https://discord.gg/fFbxxNr9XT). You can also [open an issue](https://github.com/GalaxyPay/func/issues) on GitHub.

When you ask, share your node's **diagnostics**. They give a snapshot of FUNC and your node: versions, whether the node is running and in sync, disk space, and recent warnings and errors from the node log.

::: danger Beware of scammers
Scammers watch support channels and send direct messages pretending to help. Only ever share the diagnostics described on this page, nothing more. Nobody genuinely helping you will ask for:

- your FUNC password
- a wallet mnemonic or seed phrase
- files from your computer
- remote access to your computer
- that you connect your wallet to a "validation" or "sync" website
  :::

## If the app opens

Open [Settings](/settings) with the gear icon and find **Diagnostics**:

- **Copy** puts the diagnostics on your clipboard, ready to paste.
- **Download** saves them as a file you can attach.

::: tip Copy not working?
Browsers only allow copying over HTTPS. If you opened the app from another computer using `http://`, use **Download** instead.
:::

## If the app won't open or you can't sign in

On the computer FUNC is installed on, open this address in a browser:

```text
http://localhost:3536/func/diagnostics
```

Select everything on the page and copy it, or save the page.

::: info Using FUNC from another device?
This address only works on the computer FUNC is installed on. From anywhere else, use **Copy** or **Download** in Settings instead.
:::

If that page doesn't load either, the FUNC service itself isn't running. Restart the computer and try again, and mention this when you ask for help.

## What's included

Diagnostics are made to be safe to share. They include:

- **FUNC:** the version, operating system, and when the service started.
- **Each node:**
  - whether its service is running and starts at boot
  - the node software version
  - sync status and current round
  - peer counts
  - a few settings from `config.json`
  - free disk space
  - Reti and Valar status
- **Recent problems:** distinct errors and warnings from the node log, with how many times each appeared. Errors cover the node's last 24 hours of logging, which for a stopped node is the day before it stopped. Warnings cover as much of that as could be read quickly, which on a busy node may be only the last hour or two.

They leave out or hide anything private:

- **Never included:**
  - your password and sign-in sessions
  - the node's API token
  - participation keys
  - Reti and Valar settings
  - your computer's name
  - the full node log
- **Hidden:**
  - IP addresses are replaced with `<ip>`.
  - Account addresses are shortened, for example `ABCD…WXYZ`.
  - Your user name in folder paths is replaced with `~`.
  - Your Nodely telemetry ID is replaced with `<guid>`.

The diagnostics are plain text, so you can read them before you share them.
