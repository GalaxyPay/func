<template>
  <v-container fluid>
    <v-divider />
    <v-card-title class="d-flex">
      Participation Keys <v-spacer />
      <v-btn
        :icon="mdiPlus"
        variant="plain"
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
              <v-icon
                v-show="
                  activeAccount?.address === item.address || !isKeyActive(item)
                "
                :icon="mdiChevronDown"
                size="large"
              />
            </span>
            <v-tooltip
              activator="parent"
              location="top"
              :text="keyStatus(item).text"
            />
            <v-menu
              activator="parent"
              :disabled="
                activeAccount?.address !== item.address && isKeyActive(item)
              "
              bottom
              scrim
            >
              <v-list density="compact">
                <v-list-item
                  :title="(isKeyActive(item) ? 'Re-' : '') + 'Register'"
                  @click="registerKey(item)"
                  v-show="
                    activeAccount?.address === item.address &&
                    (!isKeyActive(item) || incentiveIneligible(item.address))
                  "
                />
                <v-list-item
                  title="Go Offline"
                  @click="offline()"
                  v-show="
                    activeAccount?.address === item.address && isKeyActive(item)
                  "
                />
                <v-list-item
                  title="Delete Key"
                  @click="deleteKey(item.id)"
                  v-show="!isKeyActive(item)"
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
          {{ expireDt(Number(item.key.voteLastValid)) }}
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
            <td :colspan="2">
              <div class="pa-1">
                <div class="pa-1">
                  <v-icon
                    :icon="mdiClipboardOutline"
                    @click="copyVal(item.key.voteFirstValid)"
                  />
                  First Valid: {{ item.key.voteFirstValid.toLocaleString() }}
                </div>
                <div class="pa-1">
                  <v-icon
                    :icon="mdiClipboardOutline"
                    @click="copyVal(item.key.voteLastValid)"
                  />
                  Last Valid: {{ item.key.voteLastValid.toLocaleString() }}
                </div>
                <div class="pa-1">
                  <v-icon
                    :icon="mdiClipboardOutline"
                    @click="copyVal(item.key.voteKeyDilution)"
                  />
                  Key Dilution: {{ item.key.voteKeyDilution.toLocaleString() }}
                </div>
              </div>
            </td>
            <td :colspan="columns.length - 2" style="max-width: 0">
              <div class="pa-1">
                <div class="pa-1 ellipsis">
                  <v-icon
                    :icon="mdiClipboardOutline"
                    @click="copyVal(b64(item.key.voteParticipationKey))"
                  />
                  Vote Key: {{ b64(item.key.voteParticipationKey) }}
                </div>
                <div class="pa-1 ellipsis">
                  <v-icon
                    :icon="mdiClipboardOutline"
                    @click="copyVal(b64(item.key.selectionParticipationKey))"
                  />
                  Selection Key: {{ b64(item.key.selectionParticipationKey) }}
                </div>
                <div class="pa-1 ellipsis">
                  <v-icon
                    :icon="mdiClipboardOutline"
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
import { b64, delay, execAtc, formatAddr } from "@/utils";
import {
  mdiChevronDown,
  mdiClipboardOutline,
  mdiClose,
  mdiPlus,
} from "@mdi/js";
import { useWallet } from "@txnlab/use-wallet-vue";
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
const { activeAccount, activeNetwork, transactionSigner } = useWallet();

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

const baseUrl = computed(
  () => `http://${location.hostname}:${props.port}/v2/participation`
);

const partStats = ref<any>({});

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

    const activeKeys = keys.value?.filter((k) => isKeyActive(k));
    let proposals: number | undefined;
    let votes: number | undefined;
    if (activeKeys?.length) {
      if (props.name === "Algorand") {
        proposals = 0;
        votes = 0;
        partStats.value = {};
        const stats = await getAlgoStats(activeKeys.map((k) => k.address));
        partStats.value = stats;
      }
      if (props.name === "Voi") {
        proposals = 0;
        votes = 0;
        partStats.value = {};
        await Promise.all(
          activeKeys?.map(async (k) => {
            const stats = await axios({
              url: `https://api.voirewards.com/proposers/index_main_3.php?action=walletDetails&wallet=${k.address}`,
            });
            partStats.value[k.address] = {
              proposals: stats.data.total_blocks,
              votes: stats.data.vote_count,
            };
          })
        );
      }
      for (const value of Object.values(partStats.value) as any[]) {
        proposals += value?.proposals || 0;
        votes += value?.votes || 0;
      }
    }

    const activeStake = acctInfos.value
      .filter((a) => activeKeys?.some((k) => k.address === a.address))
      .reduce((a, c) => a + Number(c.amount), 0);
    const partDetails = {
      activeKeys: activeKeys?.length || 0,
      activeStake,
      proposals,
      votes,
    };
    emit("partDetails", partDetails);
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

function incentiveIneligible(addr: string) {
  if (!store.isIncentiveReady || activeNetwork.value !== "mainnet")
    return false;
  const acctInfo = acctInfos.value.find((ai) => ai.address === addr);
  return (
    (acctInfo?.amount || 0) >= 3 * 10 ** 10 &&
    (acctInfo?.amount || 0) < 7 * 10 ** 16 &&
    !acctInfo?.incentiveEligible
  );
}

function keyStatus(item: Participation) {
  return !isKeyActive(item)
    ? { text: "Unregistered", color: "red" }
    : incentiveIneligible(item.address)
    ? { text: "Ineligible For Incentives", color: "warning" }
    : { text: "Online", color: "success" };
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

async function showGenerateDialog() {
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
    emit("generatingKey", true);
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
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  resetAll();
}

async function registerKey(item: Participation) {
  try {
    store.overlay = true;
    const atc = new algosdk.AtomicTransactionComposer();
    const suggestedParams = await props.algodClient.getTransactionParams().do();
    if (incentiveIneligible(item.address)) {
      suggestedParams.flatFee = true;
      suggestedParams.fee = 2 * 10 ** 6;
    }
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
        from: activeAccount.value!.address,
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
  emit("generatingKey", false);
  form.value.reset();
  showGenerate.value = false;
  loading.value = false;
}

watch(
  () => store.refresh,
  async () => await getKeys()
);

function copyVal(val: string | number | bigint | undefined) {
  if (!val) return;
  navigator.clipboard.writeText(val.toString());
  store.setSnackbar("Copied", "info", 1000);
}

async function getAlgoStats(addrs: string[]) {
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
      lastProposalRound
      lastVoteRound
      proposals
      votes
    }`;
  try {
    const { data } = await axios({
      url: "https://lab-mainnet-gql.4160.nodely.dev/graphql",
      method: "post",
      data: { query, operationName: "bulkAccounts" },
    });
    const stats: any = {};
    Object.keys(data.data).forEach((key) => {
      stats[key.substring(5)] = data.data[key];
    });
    return stats;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}
</script>
