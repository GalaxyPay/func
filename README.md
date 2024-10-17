# AvmWinNode

AvmWinNode is a Windows Service that makes it easy to spin up a node for Algorand or Voi and start participating in consensus.

![screenshot](https://github.com/user-attachments/assets/746beacf-0e24-40c6-a403-06d856287a06)

## Installation

Head over to the [releases page](https://github.com/GalaxyPay/avm-win-node/releases) and download the Setup file.

In order to run it, you'll need to click "More info" on the "Windows protected your PC" dialog.
Then click the "Run Anyway" button.
The code is open-source so you can review it yourself or have a trusted friend do so.

The installer does not include the node software. It is automatically downloaded the first time you open the app from [this open-source repo](https://github.com/GalaxyPay/algowin), which compiles the official Go code into Windows binaries. This separation allows the node software to be updated without needing to update this app.
