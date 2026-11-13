# Reti

[Reti](https://github.com/algorandfoundation/reti) is an open protocol for Algorand staking pools. You run the node as a **validator**, and other people stake their ALGO in your pools. Your node participates in consensus for the pools, and the rewards are shared between the stakers and you, according to the commission you set.

FUNC can run the Reti node daemon for you as a background service. The daemon generates and registers participation keys for your pools and pays out rewards.

## Before you start

Register your validator and create your pool(s) on the [Reti site](https://reti.nodely.io/). There you'll get a **Validator ID** and set your **Node Number** and **Manager Address**.

The manager account is a hot wallet: its mnemonic is stored on your computer so the daemon can sign transactions, such as key registrations and reward payouts. Use an account just for this, and keep only enough ALGO in it to cover fees.

## Add the Reti service

1. Make sure your node is running and synced.
2. Open the [Manage menu](/manage) and choose **Add Reti Service**.
3. Enter your Validator ID, Node Number and the manager account's mnemonic.
4. Click **Start Reti Service**.

The Status panel on the [dashboard](/dashboard) now shows a light for Reti. When a newer version is out, an **Update** button appears beside it.

## Start, stop or remove

The Manage menu's Reti section has **Start Reti** and **Stop Reti**. To remove the service, stop it first, then choose **Remove Reti**.

## Learn more

- [Reti site](https://reti.nodely.io/)
- [Reti on GitHub](https://github.com/algorandfoundation/reti)
