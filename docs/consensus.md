# Participating in Consensus

To participate in consensus, your node needs a **participation key** for your account, and that key must be registered on the network to bring the account **online**.

[Read more about how participation keys work in Algorand's consensus protocol.](https://dev.algorand.co/concepts/protocol/overview/#participation-keys)

The **Participation Keys** section appears on the dashboard once your node has synced.

## Self-custody

Use this if the account is in a wallet you control, such as Pera, Defly or Lute.

1. Wait for your node to sync.
2. Click **Generate Key...** (in Getting Started or the Participation Keys section) and enter your account's address.
3. Click **Generate** and wait. It takes a few minutes for your node to generate the key.
4. Click **Connect Wallet** at the top right and connect the wallet that holds the account.
5. Click the key's status button in the Participation Keys section and choose **Register**, then approve the transaction in your wallet.
6. Your account is now online, and the **Consensus** light on the dashboard turns green.

## Escrow account or staking pool

Use this if a service holds the account for you.

::: tip
To run staking for other people instead, see [Reti](/reti) and [Valar](/valar).
:::

1. Wait for your node to sync.
2. Click **Generate Key...** and enter the escrow account's address.
3. Click **Generate** and wait. It takes a few minutes for your node to generate the key.
4. Expand the key's row and use the copy buttons to paste the key details into the service's site, then follow its instructions.
5. Once the service registers the key, the account is online and the **Consensus** light turns green.

## Managing keys

The status button on each key also lets you:

- **Re-Register** a key, when that's needed to become eligible for rewards
- **Go Offline**, which takes the account out of consensus
- **Delete Key**, for keys that aren't online
- **View Rewards** for the account

Options that send a transaction only appear when the wallet holding that account is connected.

Participation keys expire. The **Approx. Expire** column shows roughly when; generate and register a new key before then to stay online.
