<template>
  <v-container fluid>
    <v-divider />
    <v-card-title class="d-flex">
      Participation Keys <v-spacer />
      <v-btn
        text="Generate Key..."
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
                <v-col class="text-subtitle-1">
                  Blocks Certified:
                  <span class="font-weight-bold">
                    {{ partStats[item.address]?.votes.toLocaleString() }}
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
      <v-card :disabled="loading">
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
              :loading="loading"
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
import { b64, delay, execAtc, formatAddr } from "@/utils";
import {
  mdiChevronDown,
  mdiClose,
  mdiContentCopy,
  mdiOpenInNew,
} from "@mdi/js";
import { useNetwork, useWallet } from "@txnlab/use-wallet-vue";
import algosdk, { Algodv2, modelsv2 } from "algosdk";

const props = defineProps({
  name: { type: String, required: true },
  port: { type: Number, required: true },
  token: { type: String, required: true },
  algodClient: { type: Algodv2, required: true },
  status: { type: String, required: true },
});

const emit = defineEmits(["partDetails", "generatingKey"]);

const store = useAppStore();
const { activeAccount, transactionSigner } = useWallet();
const { activeNetwork } = useNetwork();

const loading = ref();
const keys = ref<Participation[]>();
const showGenerate = ref(false);
const form = ref();
const addr = ref<string>();
const gen = ref<{ first?: bigint | string; last?: bigint | string }>({});
const lastRound = ref<bigint>();
const acctInfos = ref<modelsv2.Account[]>([]);

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
  baseURL: `${location.protocol}//${location.hostname}:${port}/v2/participation`,
  headers: { "X-Algo-Api-Token": props.token },
});

const statsClient = axios.create({
  baseURL:
    props.name === "Algorand"
      ? "https://lab-mainnet-gql.4160.nodely.dev"
      : props.name === "Voi"
      ? "https://api.voirewards.com/proposers"
      : undefined,
});

const partStats = ref<any>({});

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

async function refreshData() {
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

    const activeKeys = tempKeys?.filter((k) => isKeyActive(k));
    let proposals: number | undefined;
    let votes: number | undefined;
    if (activeKeys?.length) {
      proposals = 0;
      votes = 0;
      partStats.value = await getStats(activeKeys.map((k) => k.address));
      for (const value of Object.values(partStats.value) as any[]) {
        proposals += value?.proposals || 0;
        votes += value?.votes || 0;
      }
    }

    const activeStake = acctInfos.value
      .filter((a) => activeKeys?.some((k) => k.address === a.address))
      .reduce((a, c) => a + Number(c.amount), 0);
    const partDetails: PartDetails = {
      activeKeys: activeKeys?.length || 0,
      activeStake,
      proposals,
      votes,
    };
    emit("partDetails", partDetails);
    keys.value = tempKeys;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

onMounted(() => {
  refreshData();
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
    await refreshData();
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
    loading.value = true;
    emit("generatingKey", true);
    await partClient
      .post(
        `generate/${addr.value}?first=${gen.value.first}&last=${gen.value.last}`
      )
      .then(async () => {
        let generating = true;
        while (generating) {
          await delay(920);
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
        await refreshData();
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
  if (confirm("Are you sure you want to take this account offline?")) {
    try {
      store.overlay = true;
      const suggestedParams = await props.algodClient
        .getTransactionParams()
        .do();
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
  loading.value = false;
}

watch(
  () => store.refresh,
  async () => await refreshData()
);

function copyVal(val: string | number | bigint | undefined) {
  if (!val) return;
  navigator.clipboard.writeText(val.toString());
  store.setSnackbar("Copied", "info", 1000);
}

async function getStats(addrs: string[]) {
  try {
    const resetDate = store.resetDates.find(
      (rr) => rr.name === props.name
    )?.date;
    const stats: any = {};
    switch (props.name) {
      case "Algorand": {
        let query = "    query bulkAccounts {";
        addrs.forEach((a) => {
          query += `
          addr_${a}: votingAddrStat(
            addrBin: "${a}"
          ) { ...addrData	}`;
        });
        query += `
        }
        fragment addrData on VotingAddrStat {
          proposals
          votes
        }`;
        const { data } = await statsClient.post("graphql", {
          query,
          operationName: "bulkAccounts",
        });
        Object.keys(data.data).forEach((key) => {
          stats[key.substring(5)] = data.data[key];
        });
        if (resetDate) {
          const nodley = "https://mainnet-idx.4160.nodely.dev";
          const indexer = new algosdk.Indexer("", nodley, "");
          const afterTime = new Date(resetDate).toISOString();
          const { blocks } = await indexer
            .searchForBlockHeaders()
            .afterTime(afterTime)
            .proposers(addrs)
            .do();
          addrs.forEach(
            (addr) =>
              (stats[addr].proposals = blocks.filter(
                (b) => b.proposer?.toString() === addr
              ).length)
          );
        }
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
              votes: data.vote_count,
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
