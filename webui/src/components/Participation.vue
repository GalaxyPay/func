<template>
  <v-container>
    <v-card-title class="d-flex">
      Participation Keys <v-spacer />
      <v-btn
        :icon="mdiPlus"
        variant="plain"
        color="primary"
        :disabled="status !== 'Running'"
        @click="generateDialog"
      />
    </v-card-title>
    <v-container>
      <v-data-table
        :headers="headers"
        :items="keys"
        density="comfortable"
        items-per-page="-1"
        hover
      >
        <template #no-data>
          <i>No participation keys on this node</i>
        </template>
        <template #bottom />
        <template #[`item.active`]="{ item }">
          <v-icon
            v-show="isKeyActive(item)"
            size="small"
            color="success"
            :icon="mdiCheck"
          />
        </template>
        <template #[`item.address`]="{ value }">
          <span @click="copyAddrToClipboard(value)" class="pointer">
            {{ formatAddr(value) }}
            <v-tooltip activator="parent" location="top" :text="value" />
          </span>
        </template>
        <template #[`item.expire`]="{ item }">
          {{ expireDt(Number(item.key.voteLastValid)) }}
        </template>
        <template #[`item.actions`]="{ item }">
          <span>
            <v-btn
              variant="plain"
              :icon="mdiContentCopy"
              color="currentColor"
              @click="copyKeyToClipboard(item)"
            />
            <v-tooltip
              activator="parent"
              location="top"
              text="Copy Key Details"
            />
          </span>
          <span>
            <v-btn
              variant="plain"
              :icon="mdiHandshake"
              color="currentColor"
              :disabled="activeAccount?.address != item.address"
              @click="registerKey(item)"
            />
            <v-tooltip
              activator="parent"
              location="top"
              :text="isKeyActive(item) ? 'Unregister' : 'Register'"
            />
          </span>
          <span>
            <v-btn
              variant="plain"
              :icon="mdiDelete"
              :disabled="isKeyActive(item)"
              color="error"
              @click="deleteKey(item.id)"
            />
            <v-tooltip activator="parent" location="top" text="Delete" />
          </span>
        </template>
      </v-data-table>
    </v-container>
    <v-dialog v-model="showGenerate" max-width="600" persistent>
      <v-card :disabled="loading">
        <v-card-title class="d-flex">
          Generate Participation Key
          <v-spacer />
          <v-icon color="currentColor" :icon="mdiClose" @click="resetAll()" />
        </v-card-title>
        <v-form ref="form" @submit.prevent="generateKey()">
          <v-container>
            <v-row>
              <v-col class="pb-0">
                <v-text-field
                  v-model="addr"
                  label="Address"
                  :rules="[required, validAddress]"
                  style="font-family: monospace"
                />
              </v-col>
            </v-row>
            <v-row>
              <v-col cols="6" class="pb-0">
                <v-text-field
                  v-model="gen.first"
                  type="number"
                  label="First Valid"
                  :rules="[required]"
                />
              </v-col>
              <v-col cols="6" class="pb-0">
                <v-text-field
                  v-model="gen.last"
                  type="number"
                  label="Last Valid"
                  :rules="[required]"
                />
              </v-col>
            </v-row>
          </v-container>
          <v-card-actions>
            <v-spacer />
            <v-btn
              text="Generate"
              type="submit"
              color="primary"
              variant="tonal"
              :loading="loading"
            />
          </v-card-actions>
        </v-form>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script lang="ts" setup>
import { Participation } from "@/types";
import { delay, formatAddr } from "@/utils";
import {
  mdiCheck,
  mdiClose,
  mdiContentCopy,
  mdiDelete,
  mdiHandshake,
  mdiPlus,
} from "@mdi/js";
import { useWallet } from "@txnlab/use-wallet-vue";
import algosdk, { Algodv2, modelsv2 } from "algosdk";

const props = defineProps({
  port: { type: Number, required: true },
  token: { type: String, required: true },
  algodClient: { type: Algodv2, required: true },
  status: { type: String, required: true },
});

const store = useAppStore();
const { activeAccount, transactionSigner } = useWallet();

const loading = ref();
const keys = ref<Participation[]>();
const showGenerate = ref(false);
const form = ref();
const addr = ref<string>();
const gen = ref<{ first?: number; last?: number }>({});
const lastRound = ref();
const acctInfos = ref<modelsv2.Account[]>([]);

const validAddress = (v: string) =>
  algosdk.isValidAddress(v) || "Invalid Address";

const headers = computed<any[]>(() => {
  const val = [
    { title: "Active", key: "active", sortable: false, align: "center" },
    { title: "Address", key: "address", sortable: false, align: "center" },
    {
      title: "First Valid",
      key: "key.voteFirstValid",
      sortable: false,
      align: "center",
    },
    {
      title: "Last Valid",
      key: "key.voteLastValid",
      sortable: false,
      align: "center",
    },
    {
      title: "Approx. Expire",
      key: "expire",
      sortable: false,
      align: "center",
    },
    { title: "Actions", key: "actions", sortable: false, align: "center" },
  ];
  return val;
});

const required = (v: number) => !!v || v === 0 || "Required";

const baseUrl = computed(
  () => `http://localhost:${props.port}/v2/participation`
);

async function getKeys() {
  const response = await fetch(baseUrl.value, {
    headers: { "X-Algo-Api-Token": props.token },
  });
  if (response.ok) {
    const data = await response.json();
    keys.value = data
      ?.map((p: Participation) => ({
        ...p,
        key: modelsv2.AccountParticipation.from_obj_for_encoding(p.key),
      }))
      .sort(
        (a: Participation, b: Participation) =>
          Number(b.key.voteLastValid) - Number(a.key.voteLastValid)
      );
    // get account details
    acctInfos.value = [];
    const addrs = [...new Set(keys.value?.map((k) => k.address))];
    await Promise.all(
      addrs.map(async (addr) => {
        const account = await props.algodClient.accountInformation(addr).do();
        acctInfos.value.push(modelsv2.Account.from_obj_for_encoding(account));
      })
    );
  } else {
    keys.value = undefined;
  }
}

onMounted(() => {
  getKeys();
  calcAvgBlockTime();
});

function loadDefaults() {
  addr.value = activeAccount.value?.address;
  gen.value.first = lastRound.value;
  gen.value.last = lastRound.value + 3 * 10 ** 6;
}

function isKeyActive(item: Participation) {
  const acctInfo = acctInfos.value.find((ai) => ai.address === item.address);
  if (!acctInfo) return false;
  return (
    item.key.voteParticipationKey.toString() ==
    acctInfo.participation?.voteParticipationKey.toString()
  );
}

async function deleteKey(id: string) {
  if (confirm("Are you sure you want to delete this key?")) {
    await fetch(`${baseUrl.value}/${id}`, {
      method: "DELETE",
      headers: { "X-Algo-Api-Token": props.token },
    });
    await getKeys();
  }
}

async function generateDialog() {
  await getLastRound();
  loadDefaults();
  showGenerate.value = true;
}

async function getLastRound() {
  const status = await props.algodClient.status().do();
  lastRound.value = status["last-round"];
}

async function generateKey() {
  const { valid } = await form.value.validate();
  if (!valid) return;
  try {
    loading.value = true;
    await fetch(
      `${baseUrl.value}/generate/${addr.value}` +
        `?first=${gen.value.first}&last=${gen.value.last}`,
      {
        method: "POST",
        headers: { "X-Algo-Api-Token": props.token },
      }
    ).then(async (response) => {
      if (response.ok) {
        let generating = true;
        while (generating) {
          await delay(2000);
          await getKeys();
          if (
            keys.value?.some(
              (k) =>
                k.key.voteFirstValid == gen.value.first &&
                k.key.voteLastValid == gen.value.last
            )
          )
            generating = false;
        }
        store.setSnackbar("New key generated", "success");
      } else {
        throw new Error(response.statusText);
      }
    });
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err.message, "error");
  }
  resetAll();
}

async function execAtc(
  atc: algosdk.AtomicTransactionComposer,
  success: string
) {
  const store = useAppStore();
  store.setSnackbar("Awaiting Signatures...", "info", -1);
  await atc.gatherSignatures();
  store.setSnackbar("Processing...", "info", -1);
  await atc.execute(props.algodClient, 4);
  store.setSnackbar(success, "success");
  store.refresh++;
}

async function registerKey(item: Participation) {
  try {
    if (isKeyActive(item)) {
      await offline();
      return;
    }
    store.overlay = true;
    const atc = new algosdk.AtomicTransactionComposer();
    const suggestedParams = await props.algodClient.getTransactionParams().do();
    const txn = algosdk.makeKeyRegistrationTxnWithSuggestedParamsFromObject({
      from: item.address,
      suggestedParams,
      voteFirst: Number(item.key.voteFirstValid),
      voteLast: Number(item.key.voteLastValid),
      voteKeyDilution: Number(item.key.voteKeyDilution),
      selectionKey: item.key.selectionParticipationKey,
      voteKey: item.key.voteParticipationKey,
      stateProofKey: item.key.stateProofKey!,
    });
    atc.addTransaction({ txn, signer: transactionSigner });
    await execAtc(atc, "Participation Key Registered");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err.message, "error");
  }
  store.overlay = false;
}

async function offline() {
  if (confirm("Are you sure you want to take this account offline?")) {
    try {
      store.overlay = true;
      const suggestedParams = await props.algodClient
        .getTransactionParams()
        .do();
      const atc = new algosdk.AtomicTransactionComposer();
      const txn = algosdk.makeKeyRegistrationTxnWithSuggestedParamsFromObject({
        from: activeAccount.value!.address,
        suggestedParams,
        nonParticipation: false,
      });
      atc.addTransaction({ txn, signer: transactionSigner });
      await execAtc(atc, "Account Offline");
    } catch (err: any) {
      console.error(err);
      store.setSnackbar(err.message, "error");
    }
    store.overlay = false;
  }
}

const avgBlockTime = ref();

async function calcAvgBlockTime() {
  await getLastRound();
  const currentRound = await props.algodClient.block(lastRound.value).do();
  const oldRound = await props.algodClient.block(lastRound.value - 100).do();
  avgBlockTime.value =
    Math.floor(currentRound.block.ts - oldRound.block.ts) * 10;
}

function expireDt(lastValid: number) {
  const expireMs = (lastValid - lastRound.value) * avgBlockTime.value;
  return new Date(Date.now() + expireMs).toLocaleString();
}

function resetAll() {
  form.value.reset();
  showGenerate.value = false;
  loading.value = false;
}

watch(
  () => store.refresh,
  async () => await getKeys()
);

function copyKeyToClipboard(item: Participation) {
  const formatted = { ...item, key: item.key.get_obj_for_encoding() };
  navigator.clipboard.writeText(JSON.stringify(formatted));
  store.setSnackbar("Key Copied", "info", 1000);
}

function copyAddrToClipboard(addr: string) {
  navigator.clipboard.writeText(addr);
  store.setSnackbar("Address Copied", "info", 1000);
}
</script>
