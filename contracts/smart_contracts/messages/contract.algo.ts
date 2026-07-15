import {
  Account,
  arc4,
  assert,
  BoxMap,
  clone,
  Contract,
  Global,
  GlobalState,
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
  public id = GlobalState<uint64>();
  public messages = BoxMap<uint64, Message>({ keyPrefix: "" });
  public allowedSenders = BoxMap<Account, boolean>({ keyPrefix: "" });

  private isOwner(): boolean {
    return Txn.sender === Global.creatorAddress;
  }

  private onlyOwner(): void {
    assert(this.isOwner(), "SENDER_NOT_ALLOWED");
  }

  updateApplication() {
    this.onlyOwner();
  }

  // MBR the caller must attach to `addMessage(_, message)` for this exact message.
  @arc4.abimethod({ readonly: true })
  requiredMbrForMessage(message: Message): uint64 {
    const nextId: uint64 = this.id.hasValue ? this.id.value : 0 + 1;
    const before = Global.currentApplicationAddress.minBalance;
    this.messages(nextId).value = clone(message);
    const mbr: uint64 = Global.currentApplicationAddress.minBalance - before;
    this.messages(nextId).delete();
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
    assert(
      this.isOwner() || this.allowedSenders(Txn.sender).exists,
      "SENDER_NOT_ALLOWED",
    );
    this.id.value = this.id.hasValue ? this.id.value : 0 + 1;
    const mbrBefore = Global.currentApplicationAddress.minBalance;
    this.messages(this.id.value).value = clone(message);
    this.requireMbrCoverage(mbrBefore, mbrPayment);
    return this.id.value;
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
