# Valar

[Valar](https://stake.valar.solutions) is a marketplace for Algorand staking. As a node runner, you post a **Validator Ad** with your terms, and ALGO holders (delegators) pick an ad to stake with. Your node participates in consensus for their accounts, and you earn the fees set in your ad. The delegators keep their ALGO in their own accounts the whole time.

FUNC can run the [Valar daemon](https://github.com/ValarStaking/valar/tree/master/projects/valar-daemon) for you as a background service. The daemon generates participation keys for your delegators and claims your fees.

## Before you start

Create a Validator Ad on the [Valar site](https://stake.valar.solutions). There you'll get a **Validator Ad ID** and set your **Manager Address**.

The manager account is a hot wallet: its mnemonic is stored on your computer so the daemon can sign transactions, such as key registrations and fee claims. Use an account just for this, and keep only enough ALGO in it to cover fees.

## Add the Valar service

1. Make sure your node is running and synced.
2. Open the [Manage menu](/manage) and choose **Add Valar Service**.
3. Enter your Validator Ad ID. If you have more than one ad, separate the IDs with commas.
4. Enter the manager account's mnemonic.
5. Click **Start Valar Service**.

FUNC downloads a self-contained Python runtime and the Valar daemon, which can take a few minutes. You don't need to install Python yourself.

The Status panel on the [dashboard](/dashboard) now shows a light for Valar. When a newer version is out, an **Update** button appears beside it.

## Start, stop or remove

The Manage menu's Valar section has **Start Valar** and **Stop Valar**. To remove the service, stop it first, then choose **Remove Valar**.

## Learn more

- [Valar site](https://stake.valar.solutions)
- [Valar on GitHub](https://github.com/ValarStaking/valar)
