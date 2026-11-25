<template>
  <section class="node-section">
    <div class="section-head">
      <span class="section-label">Participation Keys</span>
      <span>
        <v-progress-circular
          v-show="loading"
          indeterminate
          :size="14"
          :width="2"
        />
        <v-tooltip activator="parent" text="Loading Stats..." />
      </span>
      <v-spacer />
      <v-btn
        :text="!xs && 'Generate Key...'"
        :icon="xs && mdiPlus"
        :prepend-icon="!xs ? mdiPlus : undefined"
        variant="flat"
        color="primary"
        size="small"
        class="generate-btn"
        :aria-label="xs ? 'Generate Key...' : undefined"
        :disabled="status !== 'Running'"
        @click="showGenerateDialog"
      />
    </div>
    <div class="group">
      <v-data-table
        :headers="headers"
        :items="keys"
        density="comfortable"
        items-per-page="-1"
        hover
        class="keys-table"
      >
        <template #no-data>
          <i>No participation keys on this node</i>
        </template>
        <template #bottom />
        <template #[`item.status`]="{ item }">
          <v-btn variant="outlined" size="small" class="status-btn">
            <span class="status-dot" :class="`bg-${keyStatus(item).color}`" />
            <v-icon :icon="mdiChevronDown" size="16" />
            <v-tooltip
              activator="parent"
              location="top"
              :text="keyStatus(item).text"
            />
            <v-menu activator="parent" bottom scrim>
              <v-list density="compact">
                <div class="menu-hint text-dim" v-show="!isConnected(item)">
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
        <template #[`item.address`]="{ item }">
          <span class="address-cell">
            <span @click="copyVal(item.address)" class="pointer font-mono">
              {{ formatAddr(item.address, 7) }}
              <v-tooltip
                activator="parent"
                location="top"
                :text="item.address"
              />
            </span>
            <v-chip
              v-if="keyBadge(item)"
              :color="keyBadge(item)!.color"
              size="x-small"
              variant="tonal"
              label
            >
              {{ keyBadge(item)!.text }}
              <v-tooltip
                activator="parent"
                location="top"
                :text="keyBadge(item)!.hint"
              />
            </v-chip>
          </span>
        </template>
        <template #[`item.expire`]="{ item }">
          <span class="text-body">{{
            expireDate(partKeys[item.id]?.expires)
          }}</span>
        </template>
        <template #expanded-row="{ columns, item }">
          <tr class="expand-row">
            <td :colspan="columns.length">
              <div class="expand-stats">
                <div>
                  <span class="text-muted">Stake</span>
                  <span class="expand-figure">
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
                </div>
                <div>
                  <span class="text-muted">Blocks Created</span>
                  <span class="expand-figure">
                    {{ partStats[item.address]?.proposals.toLocaleString() }}
                  </span>
                </div>
              </div>
            </td>
          </tr>
          <tr class="expand-row expand-row--detail">
            <td :colspan="columns.length" style="max-width: 0">
              <div class="detail-grid">
                <div class="detail-list detail-list--valid">
                  <div>
                    <v-icon
                      :icon="mdiContentCopy"
                      size="14"
                      class="clickable"
                      @click="copyVal(item.key.voteFirstValid)"
                    />
                    <span class="text-muted">First Valid:</span>
                    <span class="font-mono">{{
                      item.key.voteFirstValid.toLocaleString()
                    }}</span>
                  </div>
                  <div>
                    <v-icon
                      :icon="mdiContentCopy"
                      size="14"
                      class="clickable"
                      @click="copyVal(item.key.voteLastValid)"
                    />
                    <span class="text-muted">Last Valid:</span>
                    <span class="font-mono">{{
                      item.key.voteLastValid.toLocaleString()
                    }}</span>
                  </div>
                  <div>
                    <v-icon
                      :icon="mdiContentCopy"
                      size="14"
                      class="clickable"
                      @click="copyVal(item.key.voteKeyDilution)"
                    />
                    <span class="text-muted">Key Dilution:</span>
                    <span class="font-mono">{{
                      item.key.voteKeyDilution.toLocaleString()
                    }}</span>
                  </div>
                </div>
                <div class="detail-list">
                  <div class="ellipsis">
                    <v-icon
                      :icon="mdiContentCopy"
                      size="14"
                      class="clickable"
                      @click="copyVal(b64(item.key.voteParticipationKey))"
                    />
                    <span class="text-muted">Vote Key:</span>
                    <span class="font-mono">{{
                      b64(item.key.voteParticipationKey)
                    }}</span>
                  </div>
                  <div class="ellipsis">
                    <v-icon
                      :icon="mdiContentCopy"
                      size="14"
                      class="clickable"
                      @click="copyVal(b64(item.key.selectionParticipationKey))"
                    />
                    <span class="text-muted">Selection Key:</span>
                    <span class="font-mono">{{
                      b64(item.key.selectionParticipationKey)
                    }}</span>
                  </div>
                  <div class="ellipsis">
                    <v-icon
                      :icon="mdiContentCopy"
                      size="14"
                      class="clickable"
                      @click="copyVal(b64(item.key.stateProofKey))"
                    />
                    <span class="text-muted">State Proof Key:</span>
                    <span class="font-mono">{{
                      b64(item.key.stateProofKey)
                    }}</span>
                  </div>
                </div>
              </div>
            </td>
          </tr>
        </template>
      </v-data-table>
    </div>
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
                  class="font-mono"
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
                  class="font-mono"
                />
              </v-col>
              <v-col cols="6" class="pb-0">
                <v-text-field
                  v-model="gen.last"
                  type="number"
                  label="Last Valid"
                  :rules="[required]"
                  class="font-mono"
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
              variant="flat"
              :loading="generating"
            />
          </v-card-actions>
        </v-form>
      </v-card>
    </v-dialog>
    <BlockToast :block="createdBlock" />
  </section>
</template>

<script lang="ts" setup>
import { algodTarget } from "@/clients";
import { CreatedBlock, PartDetails, PartKey, Participation } from "@/types";
import {
  b64,
  copyToClipboard,
  delay,
  effectiveResetDate,
  errorMessage,
  execAtc,
  formatAddr,
  getSuggestedParams,
  algodBaseUrl,
} from "@/utils";
import { playBlockSound } from "@/utils/blockSound";
import {
  mdiChevronDown,
  mdiClose,
  mdiContentCopy,
  mdiOpenInNew,
  mdiPlus,
} from "@mdi/js";
import { useWallet } from "@txnlab/use-wallet-vue";
import algosdk, { Algodv2, modelsv2 } from "algosdk";
import { useDisplay } from "vuetify";
import type { PropType } from "vue";

const props = defineProps({
  name: { type: String, required: true },
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
const createdBlock = ref<CreatedBlock>();
// What the service knows about each key, by id: registration, expiry, manager.
const partKeys = ref<Record<string, PartKey>>({});

const validAddress = (v: string) =>
  algosdk.isValidAddress(v) || "Invalid Address";

const headers = computed(
  () =>
    [
      { title: "Status", key: "status", sortable: false, align: "center" },
      { title: "Address", key: "address", sortable: false, align: "center" },
      {
        // Phones have no room for "Approx."
        title: xs.value ? "Expire" : "Approx. Expire",
        key: "expire",
        sortable: false,
        align: "center",
      },
      { key: "data-table-expand" },
    ] as const
);

const required = (v: number) => !!v || v === 0 || "Required";

// The participation API is served by algod itself, so both clients fail
// together when the node is down; name that endpoint in errors.
const algodUrl = algodBaseUrl(props.name);
const algodEndpoint = algodTarget(algodUrl);

const partClient = axios.create({
  baseURL: `${algodUrl}/v2/participation`,
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

async function getKeyInfo(): Promise<PartKey[]> {
  try {
    const { data } = await store.api.get<PartKey[]>(`${props.name}/keys`);
    return data;
  } catch (err: any) {
    console.error(errorMessage(err, "Load key expiry"), err);
    return [];
  }
}

let statsRequestId = 0;

async function refreshPartData() {
  try {
    const [tempKeys, info] = await Promise.all([getKeys(), getKeyInfo()]);
    partKeys.value = Object.fromEntries(info.map((k) => [k.id, k]));
    acctInfos.value = [];
    const addrs = [...new Set(tempKeys?.map((k) => k.address))];
    await Promise.all(
      addrs.map(async (addr) => {
        const account = await props.algodClient.accountInformation(addr).do();
        acctInfos.value.push(account);
      })
    );
    keys.value = tempKeys;
    const activeKeys = tempKeys?.filter((k) => isKeyActive(k)) || [];
    const activeAddrs = activeKeys.map((k) => k.address);
    const activeAccts = acctInfos.value.filter((a) =>
      activeAddrs.includes(a.address.toString())
    );
    const activeStake = activeAccts.reduce((a, c) => a + Number(c.amount), 0);
    const heartbeats = activeAccts.map((a) => Number(a.lastHeartbeat ?? 0));
    const activeRound = heartbeats.length ? Math.max(...heartbeats) : 0;

    // Emit what algod alone can tell us right away; the proposal count
    // depends on a (potentially slow) stats fetch and follows separately.
    const details: PartDetails = {
      keys: tempKeys?.length ?? 0,
      activeKeys: activeKeys.length,
      activeStake,
      activeRound: activeRound || undefined,
      proposals: undefined,
    };
    emittedPart.value = details;
    emit("partDetails", details);
    emitBlockTimestamps(activeAddrs);

    if (!activeAddrs.length) {
      partStats.value = {};
      return;
    }

    const token = ++statsRequestId;
    loading.value = true;
    try {
      const stats = (await getStats(activeAddrs)) || {};
      // A newer refresh superseded this one; drop the stale result.
      if (token !== statsRequestId) return;
      partStats.value = stats;
      let proposals = 0;
      for (const value of Object.values(stats) as any[]) {
        proposals += value?.proposals || 0;
      }
      // Spread the live object so any activeStake payout applied by
      // checkNewBlock while the fetch was in flight is preserved.
      emittedPart.value = {
        ...details,
        proposals: Object.keys(stats).length ? proposals : undefined,
      };
      emit("partDetails", emittedPart.value);
      emitBlockTimestamps(activeAddrs);
    } finally {
      if (token === statsRequestId) loading.value = false;
    }
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(
      errorMessage(err, "Load participation keys", algodEndpoint),
      "error"
    );
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
    createdBlock.value = {
      round: resp.block.header.round,
      address: proposer,
      payout: BigInt(resp.block.header.proposerPayout ?? 0),
      unit: props.name === "Voi" ? "VOI" : "ALGO",
    };
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
    console.error(errorMessage(err, "Check new block", algodEndpoint), err);
  }
}

watch(
  () => props.currentRound,
  (val, old) => {
    if (!val || !old || val <= old) return;
    checkNewBlock(val);
  }
);

onMounted(() => refreshPartData());

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

// The service compares the key with the account's online key. When it couldn't say
// (its lookup failed), compare with the account info this page fetched itself.
function isKeyActive(item: Participation) {
  const known = partKeys.value[item.id];
  if (known) return !!known.registered;
  const acctInfo = acctInfos.value.find((ai) => ai.address === item.address);
  if (!acctInfo?.participation?.stateProofKey) return false;
  const p = acctInfo.participation;
  return (
    item.key.selectionParticipationKey.toString() ==
      p.selectionParticipationKey.toString() &&
    item.key.stateProofKey?.toString() == p.stateProofKey!.toString() &&
    item.key.voteFirstValid == p.voteFirstValid &&
    item.key.voteKeyDilution == p.voteKeyDilution &&
    item.key.voteLastValid == p.voteLastValid &&
    item.key.voteParticipationKey.toString() ==
      p.voteParticipationKey.toString()
  );
}

function incentiveIneligible(addr: string) {
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
    ? { text: "Unregistered", color: "error" }
    : ii.val
      ? {
          text: `Ineligible For Incentives${ii.reason ? ": " + ii.reason : ""}`,
          color: "warning",
        }
      : { text: "Online", color: "success" };
}

const managerNames = { reti: "Reti", valar: "Valar" };

// One badge beside the address: who manages the account, or that its key needs renewing.
function keyBadge(item: Participation) {
  const k = partKeys.value[item.id];
  if (!k) return undefined;
  if (k.manager) {
    const name = managerNames[k.manager];
    return {
      text: name,
      color: "primary",
      hint: `${name} manages this account's keys`,
    };
  }
  // Once a key expires the account goes offline and algod deletes the key, so
  // there is no "expired" state to show.
  if (k.expiringSoon)
    return {
      text: "Expiring",
      color: "warning",
      hint: `Expires around ${expireDate(k.expires) ?? "soon"}. Generate and register a new key`,
    };
  return undefined;
}

async function deleteKey(id: string) {
  if (
    confirm(
      `Are you sure you want to delete this key?

If the key was previously registered, you should wait 320 rounds after unregistering it before deleting the key.`
    )
  ) {
    try {
      await partClient.delete(id);
      await refreshPartData();
    } catch (err: any) {
      console.error(err);
      store.setSnackbar(
        errorMessage(err, "Delete participation key", algodEndpoint),
        "error"
      );
    }
  }
}

// Getting Started opens the dialog from its own button.
defineExpose({ showGenerateDialog });

async function showGenerateDialog() {
  try {
    await getLastRound();
    loadDefaults();
    showGenerate.value = true;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(
      errorMessage(err, "Get current round", algodEndpoint),
      "error"
    );
  }
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
    store.setSnackbar(
      errorMessage(err, "Generate participation key", algodEndpoint),
      "error"
    );
  }
  resetAll();
}

async function registerKey(item: Participation) {
  try {
    store.overlay = true;
    const atc = new algosdk.AtomicTransactionComposer();
    const suggestedParams = await getSuggestedParams(props.algodClient);
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
    store.setSnackbar(
      errorMessage(err, "Register participation key", algodEndpoint),
      "error"
    );
  }
  store.overlay = false;
}

async function offline() {
  try {
    store.overlay = true;
    const suggestedParams = await getSuggestedParams(props.algodClient);
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
    store.setSnackbar(
      errorMessage(err, "Take account offline", algodEndpoint),
      "error"
    );
  }
  store.overlay = false;
}

// The service estimates expiry from the recent block time; just the day is shown.
function expireDate(expires?: string) {
  if (!expires) return undefined;
  return new Date(expires).toLocaleDateString(undefined, {
    year: "numeric",
    month: "numeric",
    day: "numeric",
  });
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
  copyToClipboard(val.toString());
}

const ALGORAND_INDEXERS = [
  "https://mainnet-idx.4160.nodely.dev",
  "https://mainnet-idx.algonode.xyz",
  "https://mainnet-idx.algonode.network",
];

// Choose the first indexer that answers a quick health probe. algosdk has no
// per-request timeout, so probing up front avoids waiting out a dead primary.
// The last URL is the fallback and is used without probing.
async function pickIndexer(urls: string[], timeoutMs = 3000) {
  for (const url of urls.slice(0, -1)) {
    try {
      const resp = await fetch(`${url}/health`, {
        signal: AbortSignal.timeout(timeoutMs),
      });
      if (resp.ok) return url;
    } catch {
      // Unreachable or slow; try the next one.
    }
  }
  return urls[urls.length - 1];
}

async function getStats(addrs: string[]) {
  try {
    const resetDate = effectiveResetDate(
      store.resetDates.find((rr) => rr.name === props.name)
    );
    const stats: any = {};
    switch (props.name) {
      case "Algorand": {
        const indexer = new algosdk.Indexer(
          "",
          await pickIndexer(ALGORAND_INDEXERS),
          ""
        );
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
    store.setSnackbar(
      errorMessage(
        err,
        "Load block proposal stats",
        props.name === "Voi" ? "the Voi Rewards API" : "the Algorand indexer"
      ),
      "error"
    );
  }
}
</script>

<style scoped>
.node-section {
  margin-top: 32px;
}
.text-body {
  color: rgb(var(--v-theme-text-body));
}
/* Icon-only on xs: match the 32px small buttons instead of a large circle */
.generate-btn.v-btn--icon {
  --v-btn-height: 32px;
  width: 32px;
  height: 32px;
  border-radius: 8px;
}
.generate-btn.v-btn--icon :deep(.v-icon) {
  font-size: 18px;
}
/* The badge hangs off the address's right edge without taking layout width,
   so addresses stay centered whether or not they have one. */
.address-cell {
  position: relative;
  display: inline-block;
}
.address-cell > .v-chip {
  position: absolute;
  left: calc(100% + 8px);
  top: 50%;
  transform: translateY(-50%);
}
/* Phones: no room beside it, so the badge goes centered underneath */
@media (max-width: 600px) {
  .address-cell {
    display: inline-flex;
    flex-direction: column;
    align-items: center;
    gap: 2px;
  }
  .address-cell > .v-chip {
    position: static;
    transform: none;
  }
}
.status-btn {
  --v-btn-height: 28px;
  padding-inline: 10px 6px !important;
  gap: 6px;
}
.status-btn :deep(.v-btn__content) {
  gap: 8px;
}
.menu-hint {
  padding: 6px 10px 4px;
  font-size: 12px;
}
/* Expanded key details: a recessed panel under the row */
.keys-table :deep(.v-data-table__tr--expanded) > td {
  border-bottom-color: transparent !important;
}
.expand-row > td {
  background: rgb(var(--v-theme-background));
  border-bottom: none !important;
}
.expand-row--detail > td {
  border-bottom: 1px solid rgb(var(--v-theme-border)) !important;
  font-size: 12px;
  vertical-align: top;
}
.expand-stats {
  display: flex;
  justify-content: space-around;
  gap: 16px;
  padding: 14px 0 6px;
}
.expand-stats > div {
  display: flex;
  align-items: baseline;
  gap: 10px;
  font-size: 13px;
}
.expand-figure {
  font: 500 16px var(--font-mono);
}
/* Validity on the left at its natural width; the long keys take the rest and
   truncate. Stacked on narrow screens. */
.detail-grid {
  display: grid;
  grid-template-columns: max-content minmax(0, 1fr);
  column-gap: 40px;
}
@media (max-width: 600px) {
  .detail-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
.detail-list--valid > div {
  white-space: nowrap;
}
.detail-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 8px 0 14px;
}
.detail-list > div {
  display: flex;
  align-items: center;
  gap: 6px;
}
.detail-list > div.ellipsis {
  display: block;
}
.detail-list > div.ellipsis > * + * {
  margin-inline-start: 6px;
}
.detail-list .font-mono {
  color: rgb(var(--v-theme-text-body));
}
</style>
