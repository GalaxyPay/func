# Install

The easiest way to install FUNC is with the one-line script for your computer. It works out which version you need, downloads the latest release and installs it.

## Windows

Open **PowerShell** (search for it in the Start menu) and run:

```powershell
irm https://raw.githubusercontent.com/GalaxyPay/func/main/install.ps1 | iex
```

## Mac / Linux

Open **Terminal** and run:

```sh
curl -fsSL https://raw.githubusercontent.com/GalaxyPay/func/main/install.sh | sudo sh
```

You'll be asked for your computer's password, because FUNC installs a background service.

## Open FUNC

That's it. Once installed, open [http://localhost:3536](http://localhost:3536) in your browser and bookmark it. The first time you open it, FUNC asks you to choose a password, so do that right away. Then continue with [Getting Started](/getting-started).

::: info Where does the node software come from?
The installer doesn't include the node software. FUNC downloads it the first time you open the app: from [GalaxyPay/go-algo-win](https://github.com/GalaxyPay/go-algo-win) on Windows, or from [the official Algorand repo](https://github.com/algorand/go-algorand) on Mac and Linux. Keeping them separate means the node can be updated without updating FUNC.
:::

::: tip Review before you run
The install scripts are open source ([install.ps1](https://github.com/GalaxyPay/func/blob/main/install.ps1), [install.sh](https://github.com/GalaxyPay/func/blob/main/install.sh)), so you can read them yourself or have a trusted friend do so.
:::

## Manual Installation

If you'd rather not use the script, download the install file for your computer from the [releases page](https://github.com/GalaxyPay/func/releases).

### Pick the right file for your processor

Each release has two files for every operating system: one ending in **`amd64`** and one ending in **`arm64`**. The Mac files are labelled **`darwin`** (the name of macOS's core), for example `func_5.2.1_darwin-arm64.pkg`. The endings are named for the processor type, so you need the one that matches your computer:

- **`amd64`** is for **Intel and AMD** processors. Despite the name, Intel computers use the `amd64` file too.
- **`arm64`** is for **ARM** processors, such as Apple M-series Macs, Snapdragon Windows laptops and Raspberry Pi.

To check which one you have:

| Computer | Where to look | `amd64` if it says | `arm64` if it says |
| --- | --- | --- | --- |
| Windows | **Settings > System > About**, then **System type** | x64-based processor | ARM-based processor |
| Mac | Apple menu > **About This Mac** | Processor: Intel | Chip: Apple M1, M2, M3... |
| Linux | Run `dpkg --print-architecture` in Terminal | amd64 | arm64 |

::: tip Not sure? Use the script
The one-line install script above works this out for you.
:::

### Manual install on Windows

Run the downloaded installer. Windows will show a **"Windows protected your PC"** dialog: click **More info**, then **Run anyway**.

When updating an earlier installation, the installer offers to close applications automatically and restart them after the install. Let it.

### Manual install on Mac

Download the `.pkg` file, then run this in Terminal:

```sh
sudo installer -target / -pkg <path to .pkg file>
```

If you'd rather double-click the `.pkg` file, macOS will block it because it isn't signed. To allow it:

1. Choose Apple menu > **System Settings**, then click **Privacy & Security** in the sidebar. (You may need to scroll down.)
2. Under **Security**, click **Open**.
3. Click **Open Anyway**. This button is available for about an hour after you try to open the package.
4. Enter your login password, then click **OK**.

### Manual install on Linux

Download the `.deb` file, then run:

```sh
sudo dpkg -i <path to .deb file>
```

Some distributions, such as modern Ubuntu, also let you install it by double-clicking the `.deb` file.

Then open [http://localhost:3536](http://localhost:3536). To open FUNC from another device, see [Remote Access](/remote-access).
