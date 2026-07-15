<template>
  <v-container fluid>
    <v-divider />
    <v-card-title class="d-flex">
      Participation Keys
      <span>
        <v-progress-circular
          v-show="loading"
          indeterminate
          :size="20"
          :width="2"
          class="ml-2"
        />
        <v-tooltip activator="parent" text="Loading Stats..." />
      </span>
      <v-spacer />
      <v-btn
        :text="!xs && 'Generate Key...'"
        :icon="xs && mdiPlus"
        variant="tonal"
        color="primary"
        :disabled="status !== 'Running'"
        @click="showGenerateDialog"
      />
    </v-card-title>
    <v-container fluid>
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
        <template #[`item.status`]="{ item }">
          <v-btn variant="flat" size="small">
            <span>
              <v-badge
                floating
                dot
                :color="keyStatus(item).color"
                class="mr-2"
              />
              <v-icon :icon="mdiChevronDown" size="large" />
            </span>
            <v-tooltip
              activator="parent"
              location="top"
              :text="keyStatus(item).text"
            />
            <v-menu activator="parent" bottom scrim>
              <v-list density="compact">
                <div
                  class="mx-4 text-caption text-grey"
                  v-show="!isConnected(item)"
                >
                  Connect wallet for more options
                </div>
                <v-list-item
                  :title="(isKeyActive(item) ? 'Re-' : '') + 'Register'"
                  @click="registerKey(item)"
                  v-show="
                    isConnected(item) &&
                    (!isKeyActive(item) ||
                      (incentiveIneligible(item.address).val &&
                        !incentiveIneligible(item.address).reason))
                  "
                />
                <v-list-item
                  title="Go Offline"
                  @click="offline()"
                  v-show="isConnected(item) && isKeyActive(item)"
                />
                <v-list-item
                  title="Delete Key"
                  @click="deleteKey(item.id)"
                  v-show="!isKeyActive(item)"
                />
                <v-list-item
                  title="View Rewards"
                  :append-icon="mdiOpenInNew"
                  @click="viewRewards(item)"
                />
              </v-list>
            </v-menu>
          </v-btn>
        </template>
        <template #[`item.address`]="{ value }">
          <span @click="copyVal(value)" class="pointer">
            {{ formatAddr(value, 7) }}
            <v-tooltip activator="parent" location="top" :text="value" />
          </span>
        </template>
        <template #[`item.expire`]="{ item }">
          {{ expireDt(item.key.voteLastValid) }}
        </template>
        <template #expanded-row="{ columns, item }">
          <tr style="background-color: #1acbf712">
            <td :colspan="columns.length">
              <v-row class="text-center">
                <v-col class="text-subtitle-1">
                  Stake:
                  <span class="font-weight-bold">
                    {{
                      (
                        Number(
                          acctInfos.find((a) => a.address === item.address)
                            ?.amount
                        ) /
                        10 ** 6
                      ).toLocaleString()
                    }}
                  </span>
                </v-col>
                <v-col class="text-subtitle-1">
                  Blocks Created:
                  <span class="font-weight-bold">
                    {{ partStats[item.address]?.proposals.toLocaleString() }}
                  </span>
                </v-col>
              </v-row>
            </td>
          </tr>
          <tr style="background-color: #1acbf70d" class="text-caption">
            <td :colspan="1">
              <div class="pa-1">
                <div class="pa-1">
                  <v-icon
                    :icon="mdiContentCopy"
                    size="small"
                    @click="copyVal(item.key.voteFirstValid)"
                  />
                  First Valid: {{ item.key.voteFirstValid.toLocaleString() }}
                </div>
                <div class="pa-1">
                  <v-icon
                    :icon="mdiContentCopy"
                    size="small"
                    @click="copyVal(item.key.voteLastValid)"
                  />
                  Last Valid: {{ item.key.voteLastValid.toLocaleString() }}
                </div>
                <div class="pa-1">
                  <v-icon
                    :icon="mdiContentCopy"
                    size="small"
                    @click="copyVal(item.key.voteKeyDilution)"
                  />
                  Key Dilution: {{ item.key.voteKeyDilution.toLocaleString() }}
                </div>
              </div>
            </td>
            <td :colspan="columns.length - 1" style="max-width: 0">
              <div class="pa-1">
                <div class="pa-1 ellipsis">
                  <v-icon
                    :icon="mdiContentCopy"
                    size="small"
                    @click="copyVal(b64(item.key.voteParticipationKey))"
                  />
                  Vote Key: {{ b64(item.key.voteParticipationKey) }}
                </div>
                <div class="pa-1 ellipsis">
                  <v-icon
                    :icon="mdiContentCopy"
                    size="small"
                    @click="copyVal(b64(item.key.selectionParticipationKey))"
                  />
                  Selection Key: {{ b64(item.key.selectionParticipationKey) }}
                </div>
                <div class="pa-1 ellipsis">
                  <v-icon
                    :icon="mdiContentCopy"
                    size="small"
                    @click="copyVal(b64(item.key.stateProofKey))"
                  />
                  State Proof Key: {{ b64(item.key.stateProofKey) }}
                </div>
              </div>
            </td>
          </tr>
        </template>
      </v-data-table>
    </v-container>
    <v-dialog v-model="showGenerate" max-width="600" persistent>
      <v-card :disabled="generating">
        <v-card-title class="d-flex">
          Generate Participation Key
          <v-spacer />
          <v-icon :icon="mdiClose" @click="resetAll()" />
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
              :loading="generating"
            />
          </v-card-actions>
        </v-form>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script lang="ts" setup>
import { DEFAULT_NETWORK, networks } from "@/data";
import { PartDetails, Participation } from "@/types";
import { b64, delay, effectiveResetDate, execAtc, formatAddr } from "@/utils";
import {
  mdiChevronDown,
  mdiClose,
  mdiContentCopy,
  mdiOpenInNew,
  mdiPlus,
} from "@mdi/js";
import { useNetwork, useWallet } from "@txnlab/use-wallet-vue";
import algosdk, { Algodv2, modelsv2 } from "algosdk";
import { useDisplay } from "vuetify";
import type { PropType } from "vue";

const props = defineProps({
  name: { type: String, required: true },
  port: { type: Number, required: true },
  token: { type: String, required: true },
  algodClient: { type: Algodv2, required: true },
  status: { type: String, required: true },
  currentRound: {
    type: BigInt as unknown as PropType<bigint>,
    required: false,
  },
});

const emit = defineEmits(["partDetails", "generatingKey", "blockTimestamps"]);

const store = useAppStore();
const { activeAccount, transactionSigner } = useWallet();
const { activeNetwork } = useNetwork();
const { xs } = useDisplay();

const loading = ref();
const generating = ref();
const keys = ref<Participation[]>();
const showGenerate = ref(false);
const form = ref();
const addr = ref<string>();
const gen = ref<{ first?: bigint | string; last?: bigint | string }>({});
const lastRound = ref<bigint>();
const acctInfos = ref<modelsv2.Account[]>([]);
const emittedPart = ref<PartDetails>();

const validAddress = (v: string) =>
  algosdk.isValidAddress(v) || "Invalid Address";

const headers = computed<any[]>(() => {
  const val = [
    { title: "Status", key: "status", sortable: false, align: "center" },
    { title: "Address", key: "address", sortable: false, align: "center" },
    {
      title: "Approx. Expire",
      key: "expire",
      sortable: false,
      align: "center",
    },
    { key: "data-table-expand" },
  ];
  return val;
});

const required = (v: number) => !!v || v === 0 || "Required";

const port =
  location.protocol === "https:"
    ? networks.find((n) => n.title === props.name)?.yarpAlgodPort
    : props.port;

const partClient = axios.create({
  baseURL: `${location.protocol}//${import.meta.env.VITE_HOSTNAME}:${port}/v2/participation`,
  headers: { "X-Algo-Api-Token": props.token },
});

const statsClient = axios.create({
  baseURL:
    props.name === "Voi" ? "https://api.voirewards.com/proposers" : undefined,
});

const partStats = ref<any>({});

// Block timestamps (seconds) emitted to the calendar: the cached history from
// the last stats refresh plus any blocks observed live this session.
let cachedTimestamps: number[] = [];
let liveTimestamps: number[] = [];

type ProposalsCache = {
  [network: string]: { [address: string]: { hwm: number; ts: number[] } };
};

function loadProposalsCache(): ProposalsCache {
  try {
    return JSON.parse(localStorage.getItem("partProposalsCache") || "{}");
  } catch {
    return {};
  }
}

function saveProposalsCache(cache: ProposalsCache) {
  localStorage.setItem("partProposalsCache", JSON.stringify(cache));
}

function emitBlockTimestamps(addrs: string[]) {
  const cache = loadProposalsCache();
  const netCache = cache[props.name] || {};
  const ts: number[] = [];
  for (const addr of addrs) {
    const entry = netCache[addr];
    if (entry?.ts) ts.push(...entry.ts);
  }
  cachedTimestamps = ts;
  liveTimestamps = [];
  emit("blockTimestamps", [...cachedTimestamps]);
}

async function getKeys(): Promise<Participation[]> {
  const { data }: { data: Participation[] } = await partClient.get("");
  return data
    ?.map((p) => ({
      ...p,
      key: modelsv2.AccountParticipation.fromEncodingData(
        new Map(Object.entries(p.key))
      ),
    }))
    .sort((a, b) => Number(b.key.voteLastValid) - Number(a.key.voteLastValid));
}

async function refreshPartData() {
  try {
    const tempKeys = await getKeys();
    acctInfos.value = [];
    const addrs = [...new Set(tempKeys?.map((k) => k.address))];
    await Promise.all(
      addrs.map(async (addr) => {
        const account = await props.algodClient.accountInformation(addr).do();
        acctInfos.value.push(account);
      })
    );
    keys.value = tempKeys;
    const activeKeys = tempKeys?.filter((k) => isKeyActive(k));
    let proposals = 0;
    if (activeKeys?.length) {
      loading.value = true;
      partStats.value =
        (await getStats(activeKeys.map((k) => k.address))) || {};
      for (const value of Object.values(partStats.value) as any[]) {
        proposals += value?.proposals || 0;
      }
    }
    loading.value = false;
    emitBlockTimestamps(activeKeys?.map((k) => k.address) || []);
    const activeStake = acctInfos.value
      .filter((a) => activeKeys?.some((k) => k.address === a.address))
      .reduce((a, c) => a + Number(c.amount), 0);
    const partDetails: PartDetails = {
      activeKeys: activeKeys?.length || 0,
      activeStake,
      proposals: Object.keys(partStats.value).length ? proposals : undefined,
    };
    emittedPart.value = partDetails;
    emit("partDetails", partDetails);
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function checkNewBlock(round: bigint) {
  if (props.status !== "Running") return;
  try {
    const online = keys.value
      ?.filter((k) => isKeyActive(k))
      .map((k) => k.address);
    if (!online?.length) return;
    const resp = await props.algodClient.block(round).headerOnly(true).do();
    const proposer = resp.block.header.proposer?.toString();
    if (!proposer || !online.includes(proposer)) return;
    playBlockSound();
    if (partStats.value[proposer]) {
      partStats.value[proposer].proposals =
        (partStats.value[proposer].proposals || 0) + 1;
    } else {
      partStats.value[proposer] = { proposals: 1 };
    }
    const account = await props.algodClient.accountInformation(proposer).do();
    const idx = acctInfos.value.findIndex((a) => a.address === proposer);
    if (idx >= 0) acctInfos.value.splice(idx, 1);
    acctInfos.value.push(account);
    if (emittedPart.value) {
      emittedPart.value.proposals = (emittedPart.value.proposals || 0) + 1;
      emittedPart.value.activeStake += Number(resp.block.header.proposerPayout);
      emit("partDetails", emittedPart.value);
    }
    liveTimestamps.push(Number(resp.block.header.timestamp));
    emit("blockTimestamps", [...cachedTimestamps, ...liveTimestamps]);
  } catch (err: any) {
    console.error(err);
  }
}

let audioCtx: AudioContext | undefined;

function playBlockSound() {
  try {
    audioCtx ??= new (
      window.AudioContext || (window as any).webkitAudioContext
    )();
    const ctx = audioCtx;
    if (ctx.state === "suspended") ctx.resume();
    const now = ctx.currentTime;

    const out = ctx.createGain();
    out.gain.value = 0.9;
    out.connect(ctx.destination);

    // Solid wooden "tok": short, dense tone with a fast pitch drop and
    // a second partial just above to add body instead of a hollow ring.
    const partials = [
      { type: "sine" as OscillatorType, f0: 720, f1: 560, level: 0.7 },
      { type: "sine" as OscillatorType, f0: 1180, f1: 940, level: 0.35 },
    ];
    for (const p of partials) {
      const osc = ctx.createOscillator();
      const g = ctx.createGain();
      osc.type = p.type;
      osc.frequency.setValueAtTime(p.f0, now);
      osc.frequency.exponentialRampToValueAtTime(p.f1, now + 0.012);
      g.gain.setValueAtTime(0.0001, now);
      g.gain.exponentialRampToValueAtTime(p.level, now + 0.002);
      g.gain.exponentialRampToValueAtTime(0.0001, now + 0.06);
      osc.connect(g).connect(out);
      osc.start(now);
      osc.stop(now + 0.08);
    }

    // Sharp percussive attack (the "t") for a solid, dry strike.
    const noise = ctx.createBufferSource();
    const buf = ctx.createBuffer(
      1,
      Math.ceil(ctx.sampleRate * 0.02),
      ctx.sampleRate
    );
    const data = buf.getChannelData(0);
    for (let i = 0; i < data.length; i++) {
      data[i] = (Math.random() * 2 - 1) * (1 - i / data.length);
    }
    noise.buffer = buf;
    const bp = ctx.createBiquadFilter();
    bp.type = "bandpass";
    bp.frequency.value = 2400;
    bp.Q.value = 1.2;
    const nGain = ctx.createGain();
    nGain.gain.setValueAtTime(0.5, now);
    nGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.02);
    noise.connect(bp).connect(nGain).connect(out);
    noise.start(now);
    noise.stop(now + 0.03);
  } catch (err) {
    console.error(err);
  }
}

watch(
  () => props.currentRound,
  (val, old) => {
    if (!val || !old || val <= old) return;
    checkNewBlock(val);
  }
);

onMounted(() => {
  refreshPartData();
  calcAvgBlockTime();
});

function loadDefaults() {
  if (!lastRound.value) throw Error("Invalid Round");
  addr.value = activeAccount.value?.address;
  gen.value.first = lastRound.value;
  gen.value.last = lastRound.value + 3n * 10n ** 6n;
}

function viewRewards(item: Participation) {
  const url =
    props.name === "Algorand"
      ? `https://algonoderewards.com/${item.address}`
      : props.name === "Voi"
        ? `https://voirewards.com/wallet/${item.address}#epochs`
        : undefined;
  if (url) window.open(url, "_blank");
}

function isConnected(item: Participation) {
  return activeAccount.value?.address === item.address;
}

function isKeyActive(item: Participation) {
  const acctInfo = acctInfos.value.find((ai) => ai.address === item.address);
  if (!acctInfo) return false;
  return (
    acctInfo.participation?.stateProofKey &&
    item.key.selectionParticipationKey.toString() ==
      acctInfo.participation.selectionParticipationKey.toString() &&
    item.key.stateProofKey?.toString() ==
      acctInfo.participation.stateProofKey.toString() &&
    item.key.voteFirstValid == acctInfo.participation.voteFirstValid &&
    item.key.voteKeyDilution == acctInfo.participation.voteKeyDilution &&
    item.key.voteLastValid == acctInfo.participation.voteLastValid &&
    item.key.voteParticipationKey.toString() ==
      acctInfo.participation.voteParticipationKey.toString()
  );
}

function incentiveIneligible(addr: string) {
  if (activeNetwork.value !== DEFAULT_NETWORK)
    return { val: false, reason: "Not Supported" };
  const acctInfo = acctInfos.value.find((ai) => ai.address === addr);
  if ((acctInfo?.amount || 0) < 3 * 10 ** 10)
    return { val: true, reason: "Balance Too Low" };
  if ((acctInfo?.amount || 0) >= 7 * 10 ** 13)
    return { val: true, reason: "Balance Too High" };
  return { val: !acctInfo?.incentiveEligible, reason: "" };
}

function keyStatus(item: Participation) {
  const ii = incentiveIneligible(item.address);
  return !isKeyActive(item)
    ? { text: "Unregistered", color: "red" }
    : ii.val
      ? {
          text: `Ineligible For Incentives${ii.reason ? ": " + ii.reason : ""}`,
          color: "warning",
        }
      : { text: "Online", color: "success" };
}

async function deleteKey(id: string) {
  if (
    confirm(
      `Are you sure you want to delete this key?

If the key was previously registered, you should wait 320 rounds after unregistering it before deleting the key.`
    )
  ) {
    await partClient.delete(id);
    await refreshPartData();
  }
}

async function showGenerateDialog() {
  await getLastRound();
  loadDefaults();
  showGenerate.value = true;
}

async function getLastRound() {
  const status = await props.algodClient.status().do();
  lastRound.value = status.lastRound;
}

async function generateKey() {
  const { valid } = await form.value.validate();
  if (!valid) return;
  try {
    generating.value = true;
    emit("generatingKey", true);
    await partClient
      .post(
        `generate/${addr.value}?first=${gen.value.first}&last=${gen.value.last}`
      )
      .then(async () => {
        let generating = true;
        while (generating) {
          await delay(500);
          const keys = await getKeys();
          if (
            keys?.some(
              (k) =>
                k.address === addr.value &&
                k.key.voteFirstValid == gen.value.first &&
                k.key.voteLastValid == gen.value.last
            )
          )
            generating = false;
        }
        store.setSnackbar("New key generated", "success");
        await refreshPartData();
      });
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  resetAll();
}

async function registerKey(item: Participation) {
  try {
    store.overlay = true;
    const atc = new algosdk.AtomicTransactionComposer();
    const suggestedParams = await props.algodClient.getTransactionParams().do();
    const ii = incentiveIneligible(item.address);
    if (ii.val && !ii.reason) {
      suggestedParams.flatFee = true;
      suggestedParams.fee = 2n * 10n ** 6n;
    }
    const txn = algosdk.makeKeyRegistrationTxnWithSuggestedParamsFromObject({
      sender: item.address,
      suggestedParams,
      voteFirst: Number(item.key.voteFirstValid),
      voteLast: Number(item.key.voteLastValid),
      voteKeyDilution: Number(item.key.voteKeyDilution),
      selectionKey: item.key.selectionParticipationKey,
      voteKey: item.key.voteParticipationKey,
      stateProofKey: item.key.stateProofKey!,
    });
    atc.addTransaction({ txn, signer: transactionSigner });
    await execAtc(atc, props.algodClient, "Participation Key Registered");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  store.overlay = false;
}

async function offline() {
  try {
    store.overlay = true;
    const suggestedParams = await props.algodClient.getTransactionParams().do();
    const atc = new algosdk.AtomicTransactionComposer();
    const txn = algosdk.makeKeyRegistrationTxnWithSuggestedParamsFromObject({
      sender: activeAccount.value!.address,
      suggestedParams,
      nonParticipation: false,
    });
    atc.addTransaction({ txn, signer: transactionSigner });
    await execAtc(atc, props.algodClient, "Account Offline");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  store.overlay = false;
}

const avgBlockTime = ref<number>();

async function calcAvgBlockTime() {
  await getLastRound();
  if (!lastRound.value) return;
  const currentRound = await props.algodClient.block(lastRound.value).do();
  const oldRound = await props.algodClient.block(lastRound.value - 100n).do();
  avgBlockTime.value =
    Math.floor(
      Number(
        currentRound.block.header.timestamp - oldRound.block.header.timestamp
      )
    ) * 10;
}

function expireDt(lastValid: bigint) {
  if (!lastRound.value || !avgBlockTime.value) return undefined;
  const expireMs = Number(lastValid - lastRound.value) * avgBlockTime.value;
  return new Date(Date.now() + expireMs).toLocaleString();
}

function resetAll() {
  emit("generatingKey", false);
  form.value.reset();
  showGenerate.value = false;
  generating.value = false;
}

watch(
  () => store.refreshPart,
  async () => await refreshPartData()
);

function copyVal(val: string | number | bigint | undefined) {
  if (!val) return;
  navigator.clipboard.writeText(val.toString());
  store.setSnackbar("Copied", "info", 1000);
}

async function getStats(addrs: string[]) {
  try {
    const resetDate = effectiveResetDate(
      store.resetDates.find((rr) => rr.name === props.name)
    );
    const stats: any = {};
    switch (props.name) {
      case "Algorand": {
        const nodely = "https://mainnet-idx.4160.nodely.dev";
        const indexer = new algosdk.Indexer("", nodely, "");
        const cache = loadProposalsCache();
        const netCache = (cache[props.name] ??= {});
        const resetSec = resetDate ? new Date(resetDate).getTime() / 1000 : 0;
        await Promise.all(
          addrs.map(async (addr) => {
            const entry = (netCache[addr] ??= { hwm: 0, ts: [] });
            let resp = await indexer
              .searchForBlockHeaders()
              .minRound(entry.hwm + 1)
              .limit(1000)
              .proposers([addr])
              .do();
            entry.ts.push(...resp.blocks.map((b) => Number(b.timestamp)));
            let currentRound = Number(resp.currentRound);
            while (resp.blocks.length && resp.nextToken) {
              resp = await indexer
                .searchForBlockHeaders()
                .minRound(entry.hwm + 1)
                .limit(1000)
                .proposers([addr])
                .nextToken(resp.nextToken)
                .do();
              entry.ts.push(...resp.blocks.map((b) => Number(b.timestamp)));
              currentRound = Number(resp.currentRound);
            }
            entry.hwm = currentRound;
            stats[addr] = {
              proposals: resetSec
                ? entry.ts.filter((t) => t >= resetSec).length
                : entry.ts.length,
            };
          })
        );
        saveProposalsCache(cache);
        break;
      }
      case "Voi": {
        await Promise.all(
          addrs.map(async (addr) => {
            const { data } = await statsClient.get(
              `index_main_3.php?action=walletDetails&wallet=${addr}`
            );
            stats[addr] = {
              proposals: data.total_blocks,
            };
          })
        );
        if (resetDate) {
          const start = new Date(resetDate).toISOString().split("T")[0];
          await Promise.all(
            addrs.map(async (addr) => {
              const { data } = await statsClient.get(
                `index_main_3.php?action=proposals&wallet=${addr}&start=${start}`
              );
              stats[addr].proposals = Object.values(data).flat().length;
            })
          );
        }
        break;
      }
      default:
        throw Error("Unsupported Network");
    }
    return stats;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}
</script>
