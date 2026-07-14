import {
  Account,
  arc4,
  assert,
  BoxMap,
  clone,
  Contract,
  Global,
  gtxn,
  itxn,
  Txn,
  uint64,
  Uint64,
} from "@algorandfoundation/algorand-typescript";

interface Message {
  title: string;
  body: string;
}

export class Messages extends Contract {
  public messages = BoxMap<uint64, Message>({ keyPrefix: "" });
  public allowedSenders = BoxMap<Account, boolean>({ keyPrefix: "" });

  private onlyOwner(): void {
    assert(Txn.sender === Global.creatorAddress, "SENDER_NOT_ALLOWED");
  }

  // MBR the caller must attach to `addMessage(_, message)` for this exact message.
  @arc4.abimethod({ readonly: true })
  requiredMbrForMessage(message: Message): uint64 {
    assert(!this.messages(Global.round).exists, "TRY_AGAIN");
    const before = Global.currentApplicationAddress.minBalance;
    this.messages(Global.round).value = clone(message);
    const mbr: uint64 = Global.currentApplicationAddress.minBalance - before;
    this.messages(Global.round).delete();
    return mbr;
  }

  // MBR the caller must attach to `allowSender(_, address)` for this address.
  @arc4.abimethod({ readonly: true })
  requiredMbrForSender(address: Account): uint64 {
    const before = Global.currentApplicationAddress.minBalance;
    this.allowedSenders(address).create();
    const mbr: uint64 = Global.currentApplicationAddress.minBalance - before;
    this.allowedSenders(address).delete();
    return mbr;
  }

  // Enforce that `mbrPayment` covers the MBR increase caused by a box write.
  // Call with the app min balance captured *before* the box was created.
  private requireMbrCoverage(
    mbrBefore: uint64,
    mbrPayment: gtxn.PaymentTxn,
  ): void {
    const mbrIncrease: uint64 =
      Global.currentApplicationAddress.minBalance - mbrBefore;
    assert(
      mbrPayment.receiver === Global.currentApplicationAddress,
      "MBR_PAYMENT_WRONG_RECEIVER",
    );
    assert(mbrPayment.amount === mbrIncrease, "INSUFFICIENT_MBR_PAYMENT");
  }

  // Refund the MBR freed by a box deletion back to `recipient`.
  // Call with the app min balance captured *before* the box was deleted.
  private refundMbr(mbrBefore: uint64, recipient: Account): void {
    const mbrRefund: uint64 =
      mbrBefore - Global.currentApplicationAddress.minBalance;
    if (mbrRefund > Uint64(0)) {
      itxn
        .payment({
          receiver: recipient,
          amount: mbrRefund,
          fee: 0,
        })
        .submit();
    }
  }

  addMessage(mbrPayment: gtxn.PaymentTxn, message: Message): uint64 {
    assert(this.allowedSenders(Txn.sender).exists, "SENDER_NOT_ALLOWED");
    assert(!this.messages(Global.round).exists, "TRY_AGAIN");

    const mbrBefore = Global.currentApplicationAddress.minBalance;
    this.messages(Global.round).value = clone(message);
    this.requireMbrCoverage(mbrBefore, mbrPayment);

    return Global.round;
  }

  deleteMessage(id: uint64): void {
    this.onlyOwner();
    assert(this.messages(id).exists, "INVALID_ID");
    const mbrBefore = Global.currentApplicationAddress.minBalance;
    this.messages(id).delete();
    this.refundMbr(mbrBefore, Txn.sender);
  }

  allowSender(mbrPayment: gtxn.PaymentTxn, address: Account): void {
    this.onlyOwner();
    assert(!this.allowedSenders(address).exists, "SENDER_ALREADY_ALLOWED");
    const mbrBefore = Global.currentApplicationAddress.minBalance;
    this.allowedSenders(address).create();
    this.requireMbrCoverage(mbrBefore, mbrPayment);
  }

  revokeSender(address: Account): void {
    this.onlyOwner();
    assert(this.allowedSenders(address).exists, "INVALID_ADDRESS");
    const mbrBefore = Global.currentApplicationAddress.minBalance;
    this.allowedSenders(address).delete();
    this.refundMbr(mbrBefore, Txn.sender);
  }
}
