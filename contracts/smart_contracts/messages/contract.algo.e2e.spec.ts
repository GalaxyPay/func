import { Config } from '@algorandfoundation/algokit-utils'
import { algorandFixture } from '@algorandfoundation/algokit-utils/testing'
import { AlgoAmount } from '@algorandfoundation/algokit-utils/types/amount'
import { Address } from 'algosdk'
import { beforeAll, beforeEach, describe, expect, test } from 'vitest'
import { MessagesClient, MessagesFactory } from '../artifacts/messages/MessagesClient'

const MESSAGE = { title: 'Hello', body: 'This is a notification body.' }
// Covers the app account base min balance so per-box MBR is the only variable.
const APP_BASE_FUNDING = AlgoAmount.Algo(1)
// Extra fee on calls that emit an inner refund payment (fee pooling).
const INNER_FEE = AlgoAmount.MicroAlgo(1_000)

describe('Messages MBR mechanism (LocalNet e2e)', () => {
  const localnet = algorandFixture()

  beforeAll(() => {
    Config.configure({ debug: true, populateAppCallResources: true })
  })
  beforeEach(localnet.newScope, 10_000)

  /** Deploy a fresh app, fund its base min balance, and return owner + factory. */
  const deploy = async (owner: Address) => {
    const factory = localnet.algorand.client.getTypedAppFactory(MessagesFactory, {
      defaultSender: owner,
    })
    const { appClient } = await factory.deploy({
      onUpdate: 'append',
      onSchemaBreak: 'append',
      suppressLog: true,
    })
    await localnet.algorand.send.payment({
      sender: owner,
      receiver: appClient.appAddress,
      amount: APP_BASE_FUNDING,
    })
    return { client: appClient, factory }
  }

  /** Create + fund a fresh sender account and add it to the allowlist (owner pays its MBR). */
  const allowFreshSender = async (client: MessagesClient, owner: Address) => {
    const sender = localnet.algorand.account.random()
    await localnet.algorand.send.payment({
      sender: owner,
      receiver: sender.addr,
      amount: AlgoAmount.Algo(5),
    })

    const senderMbr = (await client.send.requiredMbrForSender({ args: { address: sender.addr.toString() } })).return!
    const payment = await localnet.algorand.createTransaction.payment({
      sender: owner,
      receiver: client.appAddress,
      amount: AlgoAmount.MicroAlgo(senderMbr),
    })
    await client.send.allowSender({ args: { mbrPayment: payment, address: sender.addr.toString() } })

    return { sender, senderMbr }
  }

  test('exact MBR payment lets an allowed sender post a message', async () => {
    const owner = localnet.context.testAccount
    const { client, factory } = await deploy(owner)
    const { sender } = await allowFreshSender(client, owner)
    const senderClient = factory.getAppClientById({ appId: client.appId, defaultSender: sender.addr })

    const messageMbr = (await senderClient.send.requiredMbrForMessage({ args: { message: MESSAGE } })).return!
    expect(messageMbr).toBeGreaterThan(0n)

    const payment = await localnet.algorand.createTransaction.payment({
      sender: sender.addr,
      receiver: client.appAddress,
      amount: AlgoAmount.MicroAlgo(messageMbr),
    })
    const result = await senderClient.send.addMessage({ args: { mbrPayment: payment, message: MESSAGE } })

    // addMessage returns the round the message was stored under.
    expect(result.return).toBeGreaterThan(0n)
  })

  test('requiredMbrForMessage matches the on-chain charge exactly (under- and over-payment rejected)', async () => {
    const owner = localnet.context.testAccount
    const { client, factory } = await deploy(owner)
    const { sender } = await allowFreshSender(client, owner)
    const senderClient = factory.getAppClientById({ appId: client.appId, defaultSender: sender.addr })

    const messageMbr = (await senderClient.send.requiredMbrForMessage({ args: { message: MESSAGE } })).return!

    const underpay = await localnet.algorand.createTransaction.payment({
      sender: sender.addr,
      receiver: client.appAddress,
      amount: AlgoAmount.MicroAlgo(messageMbr - 1n),
    })
    await expect(
      senderClient.send.addMessage({ args: { mbrPayment: underpay, message: MESSAGE } }),
    ).rejects.toThrow(/INSUFFICIENT_MBR_PAYMENT/)

    const overpay = await localnet.algorand.createTransaction.payment({
      sender: sender.addr,
      receiver: client.appAddress,
      amount: AlgoAmount.MicroAlgo(messageMbr + 1n),
    })
    await expect(
      senderClient.send.addMessage({ args: { mbrPayment: overpay, message: MESSAGE } }),
    ).rejects.toThrow(/INSUFFICIENT_MBR_PAYMENT/)
  })

  test('deleting a message refunds exactly the freed MBR to the caller', async () => {
    const owner = localnet.context.testAccount
    const { client, factory } = await deploy(owner)
    const { sender } = await allowFreshSender(client, owner)
    const senderClient = factory.getAppClientById({ appId: client.appId, defaultSender: sender.addr })

    const messageMbr = (await senderClient.send.requiredMbrForMessage({ args: { message: MESSAGE } })).return!
    const payment = await localnet.algorand.createTransaction.payment({
      sender: sender.addr,
      receiver: client.appAddress,
      amount: AlgoAmount.MicroAlgo(messageMbr),
    })
    const id = (await senderClient.send.addMessage({ args: { mbrPayment: payment, message: MESSAGE } })).return!

    const appBalanceBefore = (await localnet.algorand.account.getInformation(client.appAddress)).balance.microAlgos
    // deleteMessage is owner-only and emits an inner refund payment.
    await client.send.deleteMessage({ args: { id }, extraFee: INNER_FEE })
    const appBalanceAfter = (await localnet.algorand.account.getInformation(client.appAddress)).balance.microAlgos

    expect(appBalanceBefore - appBalanceAfter).toBe(messageMbr)
  })

  test('allowSender charges exact MBR and revokeSender refunds it', async () => {
    const owner = localnet.context.testAccount
    const { client } = await deploy(owner)

    const target = localnet.algorand.account.random()
    const senderMbr = (await client.send.requiredMbrForSender({ args: { address: target.addr.toString() } })).return!
    expect(senderMbr).toBeGreaterThan(0n)

    // Underpaying the sender MBR is rejected.
    const underpay = await localnet.algorand.createTransaction.payment({
      sender: owner,
      receiver: client.appAddress,
      amount: AlgoAmount.MicroAlgo(senderMbr - 1n),
    })
    await expect(
      client.send.allowSender({ args: { mbrPayment: underpay, address: target.addr.toString() } }),
    ).rejects.toThrow(/INSUFFICIENT_MBR_PAYMENT/)

    // Exact payment succeeds.
    const payment = await localnet.algorand.createTransaction.payment({
      sender: owner,
      receiver: client.appAddress,
      amount: AlgoAmount.MicroAlgo(senderMbr),
    })
    await client.send.allowSender({ args: { mbrPayment: payment, address: target.addr.toString() } })

    // Revoking frees exactly the same MBR back out of the app account.
    const appBalanceBefore = (await localnet.algorand.account.getInformation(client.appAddress)).balance.microAlgos
    await client.send.revokeSender({ args: { address: target.addr.toString() }, extraFee: INNER_FEE })
    const appBalanceAfter = (await localnet.algorand.account.getInformation(client.appAddress)).balance.microAlgos

    expect(appBalanceBefore - appBalanceAfter).toBe(senderMbr)
  })
})
