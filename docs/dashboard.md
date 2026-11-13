# The Dashboard

## Status

The **Status** panel shows three lights. Green means all is well. When a light needs attention, a short explanation of what to do appears under it.

- **Node**: whether the node is running and synced with the network. If the node's log has recent errors, a link to view them shows here.
- **Consensus**: how many of your accounts are online, and whether the node is voting for them.
- **Network**: whether the node is connected to enough peers.

If you run [Reti or Valar](/manage#reti-valar), they get a light here too.

Below the lights, the **Manage** button opens the [Manage menu](/manage).

## Overview

Once the node is running, the **Overview** shows:

- **Current Block**: the latest block your node knows about
- **Blocks Created**: blocks proposed by your online accounts. Use the button beside it to count from a chosen date: all time, this year, this month, today or a date you pick.
- **Online Accounts** and **Online Stake**: your accounts that are online, and how much they hold

On Algorand, a calendar at the bottom of the page shows the days your accounts created blocks.

Turn on **Show Advanced Details** in [Settings](/settings) to also see charts of the node's health over time, algod's version and uptime, and a list of peers. Click the **Network** row to show the peers.

## Fast Catchup

While a new node syncs, a **Fast Catchup** section shows its progress through each stage.

## Networks

FUNC shows Algorand by default. To run a Voi node too, turn on **Show Alternative Networks** in [Settings](/settings), and a tab appears for each network.

::: tip
The dashboard doesn't need to be open for the node to run. It's more efficient to close it when you're not using it.
:::
