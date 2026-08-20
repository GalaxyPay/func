<template>
  <v-dialog v-model="show" max-width="1000" scrollable>
    <v-card>
      <v-card-title class="d-flex align-center">
        Messages Admin
        <v-spacer />
        <v-icon color="currentColor" :icon="mdiClose" @click="show = false" />
      </v-card-title>
      <v-card-text>
        <v-alert v-if="loadError" type="error" class="mb-4" density="compact">
          {{ loadError }}
        </v-alert>
        <v-row dense class="mb-2">
          <v-col cols="12" sm="6" md="3">
            <div class="text-caption text-grey">App ID</div>
            <div>{{ appId }}</div>
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <div class="text-caption text-grey">Network / Message Counter</div>
            <div>{{ activeNetwork }} / {{ globalId ?? "?" }}</div>
          </v-col>
        </v-row>

        <v-card variant="outlined" class="mb-4">
          <v-card-title class="text-subtitle-1">Messages</v-card-title>
          <v-data-table
            :headers="messageHeaders"
            :items="messages"
            :items-per-page="-1"
            density="comfortable"
            hide-default-footer
            no-data-text="No messages"
          >
            <template #item.actions="{ item }">
              <span>
                <v-icon
                  :icon="mdiDelete"
                  :disabled="!isCreator"
                  @click="deleteMessage(item)"
                />
                <v-tooltip
                  text="Delete message (refunds MBR)"
                  location="bottom"
                  activator="parent"
                />
              </span>
            </template>
          </v-data-table>
          <v-card-text>
            <v-text-field
              v-model="title"
              label="Title"
              density="compact"
              :disabled="!canPost"
            />
            <v-textarea
              v-model="body"
              label="Message"
              rows="2"
              density="compact"
              :disabled="!canPost"
            />
            <v-btn
              color="primary"
              :disabled="!canPost || !title || !body"
              @click="addMessage()"
            >
              Add Message
            </v-btn>
          </v-card-text>
        </v-card>

        <v-card variant="outlined" class="mb-4">
          <v-card-title class="text-subtitle-1">Allowed Senders</v-card-title>
          <v-card-text>
            <div v-if="!senders.length" class="text-grey mb-2">
              No allowed senders (creator can always post)
            </div>
            <div
              v-for="sender in senders"
              :key="sender"
              class="d-flex align-center mb-1"
            >
              <span style="font-family: monospace">{{ sender }}</span>
              <span>
                <v-icon
                  class="ml-2"
                  :icon="mdiDelete"
                  :disabled="!isCreator"
                  @click="revokeSender(sender)"
                />
                <v-tooltip
                  text="Revoke sender (refunds MBR)"
                  location="bottom"
                  activator="parent"
                />
              </span>
            </div>
            <v-text-field
              v-model="newSender"
              label="Address to allow"
              density="compact"
              class="mt-2"
              :rules="[
                (v: string) =>
                  !v || algosdk.isValidAddress(v) || 'Invalid address',
              ]"
              :disabled="!isCreator"
            />
            <v-btn
              color="primary"
              :disabled="!isCreator || !algosdk.isValidAddress(newSender)"
              @click="allowSender()"
            >
              Allow Sender
            </v-btn>
          </v-card-text>
        </v-card>

        <v-card variant="outlined">
          <v-card-title class="text-subtitle-1">Update Contract</v-card-title>
          <v-card-text>
            <v-file-input
              v-model="specModel"
              label="Messages.arc56.json"
              accept=".json"
              density="compact"
              :disabled="!isCreator"
            />
            <v-alert
              v-if="specError"
              type="error"
              density="compact"
              class="mb-2"
            >
              {{ specError }}
            </v-alert>
            <div v-if="parsedSpec" class="mb-2">
              Contract: {{ parsedSpec.name }} — compiler
              {{ parsedSpec.compiler }} — approval
              {{ parsedSpec.approval.length }} bytes, clear
              {{ parsedSpec.clear.length }} bytes
            </div>
            <v-btn
              color="error"
              class="mt-2"
              :disabled="!isCreator || !parsedSpec"
              @click="updateContract()"
            >
              Update Contract
            </v-btn>
          </v-card-text>
        </v-card>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { Message } from "@/types";
import { execAtc } from "@/utils";
import { mdiClose, mdiDelete } from "@mdi/js";
import { useNetwork, useWallet } from "@txnlab/use-wallet-vue";
import algosdk, { modelsv2 } from "algosdk";

const props = defineProps({ visible: { type: Boolean, required: true } });
const emit = defineEmits(["close"]);

const store = useAppStore();
const { activeAccount, transactionSigner, algodClient } = useWallet();
const { activeNetwork } = useNetwork();

// The dialog only ever renders for an account that can act on the
// contract (creator or allowed sender); anyone else sees nothing.
const show = computed({
  get() {
    return props.visible && canPost.value;
  },
  set(val) {
    if (!val) {
      emit("close");
    }
  },
});

const appId = BigInt(import.meta.env.VITE_MESSAGES_APP_ID);
const appAddress = algosdk.getApplicationAddress(appId);

const abi = {
  update: algosdk.ABIMethod.fromSignature("updateApplication()void"),
  mbrForMessage: algosdk.ABIMethod.fromSignature(
    "requiredMbrForMessage((string,string))uint64"
  ),
  mbrForSender: algosdk.ABIMethod.fromSignature(
    "requiredMbrForSender(address)uint64"
  ),
  addMessage: algosdk.ABIMethod.fromSignature(
    "addMessage(pay,(string,string))uint64"
  ),
  deleteMessage: algosdk.ABIMethod.fromSignature("deleteMessage(uint64)void"),
  allowSender: algosdk.ABIMethod.fromSignature("allowSender(pay,address)void"),
  revokeSender: algosdk.ABIMethod.fromSignature("revokeSender(address)void"),
};

const creator = ref<string>();
const globalId = ref<bigint>();
const messages = ref<Message[]>([]);
const senders = ref<string[]>([]);
const loadError = ref<string>();

const title = ref("");
const body = ref("");
const newSender = ref("");

const messageHeaders = [
  { title: "ID", key: "id", width: 60 },
  { title: "Title", key: "title" },
  { title: "Message", key: "body", sortable: false },
  { title: "", key: "actions", sortable: false, width: 40 },
] as const;

const isCreator = computed(
  () => !!activeAccount.value && activeAccount.value.address === creator.value
);
const canPost = computed(
  () =>
    isCreator.value ||
    (!!activeAccount.value &&
      senders.value.includes(activeAccount.value.address))
);

async function loadState() {
  try {
    loadError.value = undefined;
    const app = await algodClient.value.getApplicationByID(appId).do();
    creator.value = app.params?.creator?.toString();
    const idKv = app.params?.globalState?.find(
      (kv) => Buffer.from(kv.key).toString() === "id"
    );
    globalId.value = idKv?.value.uint ?? 0n;
    const resp = await algodClient.value
      .getApplicationBoxes(appId)
      .include("values")
      .do();
    const tupleType = algosdk.ABIType.from("(string,string)");
    const msgs: Message[] = [];
    const snds: string[] = [];
    for (const box of resp.boxes ?? []) {
      if (box.name.length === 8) {
        const [msgTitle, msgBody] = tupleType.decode(box.value!) as string[];
        msgs.push({
          id: algosdk.decodeUint64(box.name),
          title: msgTitle,
          body: msgBody,
        });
      } else if (box.name.length === 32) {
        snds.push(algosdk.encodeAddress(box.name));
      }
    }
    messages.value = msgs.sort((a, b) => a.id - b.id);
    senders.value = snds;
  } catch (err: any) {
    console.error(err);
    loadError.value = err?.response?.data || err.message;
  }
}

watch(
  () => [props.visible, activeNetwork.value],
  async () => {
    if (!props.visible) return;
    await loadState();
    if (!canPost.value) {
      if (loadError.value) {
        store.setSnackbar(loadError.value, "error");
      }
      emit("close");
    }
  }
);

// Keep the parent's visible flag in sync if authorization is lost while
// open (wallet disconnected or account switched).
watch(canPost, (val) => {
  if (!val && props.visible) emit("close");
});

// Quote the exact MBR a method requires via a signer-less simulate. The
// readonly methods create and delete a box internally, hence
// allowUnnamedResources.
async function quoteMbr(
  method: algosdk.ABIMethod,
  args: algosdk.ABIValue[]
): Promise<bigint> {
  const atc = new algosdk.AtomicTransactionComposer();
  const suggestedParams = await algodClient.value.getTransactionParams().do();
  atc.addMethodCall({
    appID: appId,
    method,
    methodArgs: args,
    sender: activeAccount.value!.address,
    suggestedParams,
    signer: algosdk.makeEmptyTransactionSigner(),
  });
  const { methodResults, simulateResponse } = await atc.simulate(
    algodClient.value,
    new modelsv2.SimulateRequest({
      txnGroups: [],
      allowEmptySignatures: true,
      allowUnnamedResources: true,
    })
  );
  const failure = simulateResponse.txnGroups[0]?.failureMessage;
  if (failure) throw new Error(failure);
  if (methodResults[0].decodeError) throw methodResults[0].decodeError;
  return methodResults[0].returnValue as bigint;
}

async function addMessage() {
  try {
    store.overlay = true;
    const msgTuple: algosdk.ABIValue = [title.value, body.value];
    const mbr = await quoteMbr(abi.mbrForMessage, [msgTuple]);
    // Refresh the counter right before sending; the new message's box key
    // (id + 1) must be referenced explicitly.
    await loadState();
    const sender = activeAccount.value!.address;
    const suggestedParams = await algodClient.value.getTransactionParams().do();
    const pay = algosdk.makePaymentTxnWithSuggestedParamsFromObject({
      sender,
      receiver: appAddress,
      amount: mbr,
      suggestedParams,
    });
    const atc = new algosdk.AtomicTransactionComposer();
    atc.addMethodCall({
      appID: appId,
      method: abi.addMessage,
      methodArgs: [{ txn: pay, signer: transactionSigner }, msgTuple],
      sender,
      suggestedParams,
      boxes: [
        { appIndex: 0, name: algosdk.encodeUint64(globalId.value! + 1n) },
        { appIndex: 0, name: algosdk.decodeAddress(sender).publicKey },
      ],
      signer: transactionSigner,
    });
    await execAtc(atc, algodClient.value, `Message added (MBR ${mbr} µA)`);
    title.value = "";
    body.value = "";
    await loadState();
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  store.overlay = false;
}

async function deleteMessage(item: Message) {
  try {
    store.overlay = true;
    const suggestedParams = await algodClient.value.getTransactionParams().do();
    // Cover the fee-0 inner refund payment.
    suggestedParams.flatFee = true;
    suggestedParams.fee = 2n * BigInt(suggestedParams.minFee);
    const atc = new algosdk.AtomicTransactionComposer();
    atc.addMethodCall({
      appID: appId,
      method: abi.deleteMessage,
      methodArgs: [BigInt(item.id)],
      sender: activeAccount.value!.address,
      suggestedParams,
      boxes: [{ appIndex: 0, name: algosdk.encodeUint64(item.id) }],
      signer: transactionSigner,
    });
    await execAtc(atc, algodClient.value, "Message deleted");
    await loadState();
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  store.overlay = false;
}

async function allowSender() {
  try {
    store.overlay = true;
    const addr = newSender.value;
    const mbr = await quoteMbr(abi.mbrForSender, [addr]);
    const sender = activeAccount.value!.address;
    const suggestedParams = await algodClient.value.getTransactionParams().do();
    const pay = algosdk.makePaymentTxnWithSuggestedParamsFromObject({
      sender,
      receiver: appAddress,
      amount: mbr,
      suggestedParams,
    });
    const atc = new algosdk.AtomicTransactionComposer();
    atc.addMethodCall({
      appID: appId,
      method: abi.allowSender,
      methodArgs: [{ txn: pay, signer: transactionSigner }, addr],
      sender,
      suggestedParams,
      boxes: [{ appIndex: 0, name: algosdk.decodeAddress(addr).publicKey }],
      signer: transactionSigner,
    });
    await execAtc(atc, algodClient.value, "Sender allowed");
    newSender.value = "";
    await loadState();
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  store.overlay = false;
}

async function revokeSender(addr: string) {
  try {
    store.overlay = true;
    const suggestedParams = await algodClient.value.getTransactionParams().do();
    // Cover the fee-0 inner refund payment.
    suggestedParams.flatFee = true;
    suggestedParams.fee = 2n * BigInt(suggestedParams.minFee);
    const atc = new algosdk.AtomicTransactionComposer();
    atc.addMethodCall({
      appID: appId,
      method: abi.revokeSender,
      methodArgs: [addr],
      sender: activeAccount.value!.address,
      suggestedParams,
      boxes: [{ appIndex: 0, name: algosdk.decodeAddress(addr).publicKey }],
      signer: transactionSigner,
    });
    await execAtc(atc, algodClient.value, "Sender revoked");
    await loadState();
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  store.overlay = false;
}

// ARC-56 spec upload for contract updates
const specModel = ref<File | File[] | null>(null);
const parsedSpec = ref<{
  name: string;
  compiler: string;
  approval: Uint8Array;
  clear: Uint8Array;
}>();
const specError = ref<string>();

watch(specModel, async (model) => {
  parsedSpec.value = undefined;
  specError.value = undefined;
  const file = Array.isArray(model) ? model[0] : model;
  if (!file) return;
  try {
    const spec = JSON.parse(await file.text());
    if (spec?.name !== "Messages") {
      throw new Error("Not a Messages ARC-56 file");
    }
    if (!spec.byteCode?.approval || !spec.byteCode?.clear) {
      throw new Error("ARC-56 file missing byteCode");
    }
    if (Object.keys(spec.templateVariables ?? {}).length) {
      throw new Error("Template variables not supported");
    }
    const v = spec.compilerInfo?.compilerVersion;
    parsedSpec.value = {
      name: spec.name,
      compiler: v
        ? `${spec.compilerInfo.compiler} ${v.major}.${v.minor}.${v.patch}`
        : "unknown",
      approval: algosdk.base64ToBytes(spec.byteCode.approval),
      clear: algosdk.base64ToBytes(spec.byteCode.clear),
    };
  } catch (err: any) {
    console.error(err);
    specError.value = err.message;
  }
});

async function updateContract() {
  try {
    store.overlay = true;
    const suggestedParams = await algodClient.value.getTransactionParams().do();
    const atc = new algosdk.AtomicTransactionComposer();
    atc.addMethodCall({
      appID: appId,
      method: abi.update,
      sender: activeAccount.value!.address,
      suggestedParams,
      onComplete: algosdk.OnApplicationComplete.UpdateApplicationOC,
      approvalProgram: parsedSpec.value!.approval,
      clearProgram: parsedSpec.value!.clear,
      signer: transactionSigner,
    });
    await execAtc(atc, algodClient.value, "Contract updated");
    specModel.value = null;
    await loadState();
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  store.overlay = false;
}
</script>
