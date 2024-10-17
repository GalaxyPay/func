# ![icon](assets/icon.png) AvmWinNode

AvmWinNode is a Windows Service that makes it easy to spin up a node for Algorand or Voi and start participating in consensus.

![screenshot](assets/screenshot.png)

## Installation

Head over to the [releases page](https://github.com/GalaxyPay/avm-win-node/releases) and download the Setup file.

In order to run it, you'll need to click "More info" on the "Windows protected your PC" dialog.
Then click the "Run Anyway" button.
The code is open-source so you can review it yourself or have a trusted friend do so.

The installer does not include the node software. It is automatically downloaded the first time you open the app from [this open-source repo](https://github.com/GalaxyPay/algowin), which compiles the official Go code into Windows binaries. This separation allows the node software to be updated without needing to update this app.

## Manage Node Menu Options

### Create Service

- Creates and starts a new Windows Service to run the Node
- Only available when Service does not exist

### Start Node

- Starts Windows Service that runs the Node
- Only available when Service is stopped

### Stop Node

- Stops Windows Service that runs the Node
- Only available when Service is running

### Remove Service

- Removes Windows Service that runs the Node
- Only available when Service is stopped

### Delete Node Data

- Deletes all node data, including any participation and KMD keys
- Only available when Service does not exist

## Notes

- The app is a [locally hosted webpage](http://localhost:3536). After install, bookmark it for easy access.

- The node will restart automatically if your computer reboots, but you will need to configure Windows to **_not_** go into Sleep mode in order to keep the node running 24/7.

- If you Stop a node and restart your computer, the node will restart automatically. You must remove the service if you want the node to not restart. Removing the service preserves the node data; deleting the data is a separate step.

## Build (for Developers)

You can fork the repo and let Github Actions do the build for you, or you can run the commands from [the actions yml](.github\workflows\go.yml) locally.

Dependencies include .NET Core 8, Node.js, pnpm, and Inno Setup.
