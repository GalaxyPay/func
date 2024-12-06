<template>
  <div v-if="nodeStatus">
    <v-progress-linear indeterminate v-show="loading" class="mb-n1" />
    <v-container class="pl-5" fluid>
      <v-row>
        <v-col cols="3">
          <div class="py-1">
            <v-badge floating dot class="mx-3 mb-1" :color="createdColor" />
            Service Created
          </div>
          <div class="py-1">
            <v-badge floating dot class="mx-3 mb-1" :color="runningColor" />
            Node Running
          </div>
          <div class="py-1">
            <v-badge
              floating
              dot
              class="mx-3 mb-1"
              :class="isSyncing && !generatingKey ? 'pulsate' : ''"
              :color="syncedColor"
            />
            Node Synced
          </div>
          <div class="py-1 ellipsis">
            <v-badge
              floating
              dot
              class="mx-3 mb-1"
              :color="participatingColor"
            />
            Participating in Consensus
          </div>
          <div
            class="py-1"
            v-show="
              nodeStatus.retiStatus &&
              nodeStatus.retiStatus.serviceStatus !== 'Not Found'
            "
          >
            <v-badge floating dot class="mx-3 mb-1" :color="retiColor" />
            Reti Running
            <v-chip
              v-show="retiUpdate"
              color="warning"
              size="small"
              class="ml-1"
              @click="updateReti()"
              density="compact"
            >
              Update
              <v-tooltip
                activator="parent"
                location="top"
                :text="`Update to ${retiLatest}`"
              />
            </v-chip>
          </div>
          <Manage
            class="mt-4"
            :name="name"
            :node-status="nodeStatus"
            @get-status="getNodeStatus()"
          />
        </v-col>
        <template v-if="nodeStatus.serviceStatus === 'Running'">
          <v-col cols="3" class="text-center">
            <div class="text-h4" style="white-space: nowrap">
              {{
                algodStatus
                  ? algodStatus["last-round"].toLocaleString() || "-"
                  : "-"
              }}
            </div>
            <div>Current Block</div>
            <div class="mt-13 text-h4">
              {{ partDetails ? partDetails.activeKeys : "-" }}
            </div>
            <div>Online Accounts</div>
          </v-col>
          <v-col cols="3" class="text-center">
            <div class="text-h4">
              {{
                partDetails?.proposals == null
                  ? "-"
                  : partDetails.proposals.toLocaleString()
              }}
            </div>
            <div>Blocks Proposed</div>
            <div class="mt-13 text-h4" style="white-space: nowrap">
              {{
                partDetails
                  ? (partDetails.activeStake / 10 ** 6).toLocaleString()
                  : "-"
              }}
            </div>
            <div>Online Stake</div>
          </v-col>
          <v-col cols="3" class="text-center">
            <div class="text-h4">
              {{
                partDetails?.votes == null
                  ? "-"
                  : partDetails.votes.toLocaleString()
              }}
            </div>
            <div>Blocks Voted</div>
            <div
              :class="partDetails ? 'pointer' : 'text-grey'"
              @click="reloadPartDetails()"
            >
              <v-icon class="mt-14 mb-1" :icon="mdiRefresh" size="x-large" />
              <div class="text-decoration-underline">Refresh Data</div>
            </div>
          </v-col>
        </template>
      </v-row>
    </v-container>
    <v-container v-if="algodStatus?.catchpoint" fluid>
      <v-divider />
      <v-card-title>Fast Catchup</v-card-title>
      <v-card-text class="pb-0">
        Block:
        {{
          algodStatus.catchpoint.substring(
            0,
            algodStatus.catchpoint.indexOf("#")
          )
        }}
      </v-card-text>
      <v-container :max-width="500">
        <div class="pb-4" v-for="prog in catchupProgress">
          {{ prog.name }}
          {{
            prog.verified > 0
              ? "(Verifying)"
              : prog.processed > 0 && prog.processed < 100
              ? "(Processing)"
              : ""
          }}
          <v-progress-linear
            v-show="prog.processed != 100"
            :model-value="prog.processed"
            color="warning"
          />
          <v-progress-linear
            v-show="prog.processed == 100"
            :model-value="prog.verified"
            color="primary"
          />
        </div>
      </v-container>
    </v-container>
    <Participation
      v-if="algodClient && algodStatus?.['last-round'] > 100"
      :name="name"
      :port="nodeStatus.port"
      :token="nodeStatus.token"
      :algod-client="algodClient"
      :status="status"
      @part-details="(val) => (partDetails = val)"
      @generating-key="(val) => (generatingKey = val)"
    />
    <v-container fluid v-if="peers">
      <v-divider />
      <v-card-title>Peers ({{ peers.length }})</v-card-title>
      <v-card-text>
        <div v-for="item in peers">
          {{ item.address }}
          <span>
            <v-icon
              v-if="item.network === 'p2p'"
              class="ml-1"
              size="small"
              color="primary"
              :icon="mdiLanConnect"
            />
            <v-tooltip activator="parent" location="right" text="P2P" />
          </span>
          <span>
            <v-icon
              v-if="!item.outgoing"
              class="ml-1"
              size="small"
              color="success"
              :icon="mdiArrowLeft"
            />
            <v-tooltip activator="parent" location="right" text="Inbound" />
          </span>
        </div>
      </v-card-text>
    </v-container>
  </div>
</template>

<script setup lang="ts">
import FUNC from "@/services/api";
import { NodeStatus, Peer } from "@/types";
import { checkCatchup, delay } from "@/utils";
import { mdiArrowLeft, mdiLanConnect, mdiRefresh } from "@mdi/js";
import { Algodv2 } from "algosdk";

const store = useAppStore();
const props = defineProps({ name: { type: String, required: true } });
const nodeStatus = ref<NodeStatus>();
const loading = ref(false);
const algodStatus = ref();
const retiLatest = ref<string>();
const partDetails = ref();
const generatingKey = ref<boolean>(false);

const retiRunning = computed(
  () => nodeStatus.value?.retiStatus?.exeStatus === "Running"
);

const retiUpdate = computed(() => {
  const current = nodeStatus.value?.retiStatus?.version;
  if (!current || !retiLatest.value) return false;
  const trimL = current.slice(current.indexOf("version") + 8);
  return trimL.slice(0, trimL.indexOf(" ")) !== retiLatest.value;
});

const isSyncing = computed(() => !!algodStatus.value?.["catchup-time"]);

const createdColor = computed(() =>
  nodeStatus.value?.serviceStatus !== "Not Found" ? "success" : "red"
);
const runningColor = computed(() =>
  nodeStatus.value?.serviceStatus === "Running" ? "success" : "red"
);

const syncedColor = computed(() =>
  isSyncing.value && !generatingKey.value ? "warning" : runningColor.value
);

const participatingColor = computed(() =>
  nodeStatus.value?.serviceStatus !== "Running" ||
  (isSyncing.value && !generatingKey.value) ||
  (partDetails.value && !partDetails.value.activeKeys)
    ? "red"
    : generatingKey.value || !partDetails.value
    ? "grey"
    : "success"
);

const retiColor = computed(() => (retiRunning.value ? "success" : "red"));

const status = computed(() =>
  isSyncing.value
    ? "Syncing"
    : nodeStatus.value
    ? nodeStatus.value.serviceStatus
    : "Unknown"
);

const algodClient = computed(() => {
  if (!nodeStatus.value?.token) return undefined;
  return new Algodv2(
    nodeStatus.value.token,
    `http://${location.hostname}`,
    nodeStatus.value.port
  );
});

onBeforeMount(async () => {
  await getNodeStatus();
});

const isMounted = ref(true);
onBeforeUnmount(() => {
  isMounted.value = false;
});

let refreshing = false;

async function autoRefresh() {
  if (refreshing) return;
  refreshing = true;
  while (nodeStatus.value?.serviceStatus === "Running") {
    await delay(1200);
    if (!isMounted.value) return;
    await getNodeStatus();
  }
  refreshing = false;
}

let restartAttempted = false;

const peers = ref<Peer[]>();

async function getNodeStatus() {
  const resp = await FUNC.api.get(props.name);
  nodeStatus.value = resp.data;
  if (nodeStatus.value?.serviceStatus === "Running") {
    if (algodClient.value) {
      algodStatus.value = await algodClient.value?.status().do();
      if (nodeStatus.value.p2p) {
        try {
          const response = (
            await axios({
              url: `http://${location.hostname}:${nodeStatus.value.port}/v2/status/peers`,
              headers: { "X-Algo-Api-Token": nodeStatus.value.token },
            })
          ).data as Peer[];
          peers.value = response.sort((a, b) =>
            a.address.localeCompare(b.address)
          );
        } catch {}
      } else {
        peers.value = undefined;
      }
    }
    if (!refreshing) autoRefresh();
  } else {
    algodStatus.value = undefined;
    peers.value = undefined;
  }
  if (nodeStatus.value?.retiStatus?.version && !retiLatest.value) {
    const releases = await axios({
      url: "https://api.github.com/repos/algorandfoundation/reti/releases/latest",
    });
    retiLatest.value = releases.data.name;
  }
  if (
    nodeStatus.value?.retiStatus?.serviceStatus === "Running" &&
    nodeStatus.value.retiStatus.exeStatus === "Stopped" &&
    !store.stoppingReti &&
    !restartAttempted
  ) {
    restartAttempted = true;
    console.error("reti not running - atempting restart");
    await FUNC.api.put("reti/stop");
    await FUNC.api.put("reti/start");
  }
}

const catchupProgress = computed(() => {
  if (!algodStatus.value?.catchpoint) return undefined;
  return [
    {
      name: "Accounts",
      processed:
        (algodStatus.value["catchpoint-processed-accounts"] /
          algodStatus.value["catchpoint-total-accounts"]) *
        100,
      verified:
        (algodStatus.value["catchpoint-verified-accounts"] /
          algodStatus.value["catchpoint-total-accounts"]) *
        100,
    },
    {
      name: "KVs",
      processed:
        (algodStatus.value["catchpoint-processed-kvs"] /
          algodStatus.value["catchpoint-total-kvs"]) *
        100,
      verified:
        (algodStatus.value["catchpoint-verified-kvs"] /
          algodStatus.value["catchpoint-total-kvs"]) *
        100,
    },
    {
      name: "Blocks",
      processed: 100,
      verified:
        (algodStatus.value["catchpoint-acquired-blocks"] /
          algodStatus.value["catchpoint-total-blocks"]) *
        100,
    },
  ];
});

async function updateReti() {
  if (!retiUpdate.value) return;
  loading.value = true;
  await FUNC.api.post("reti/update");
  await getNodeStatus();
  loading.value = false;
  store.setSnackbar("Reti Updated", "success");
}

watch(
  () => status.value,
  (val) => {
    if (val === "Syncing") {
      checkCatchup(algodStatus.value, props.name);
    }
  }
);

function reloadPartDetails() {
  if (!partDetails.value) return;
  partDetails.value = undefined;
  store.refresh++;
}

let paused = false;

watch(
  () => store.stopNodeServices,
  async (val) => {
    if (val && nodeStatus.value?.serviceStatus === "Running") {
      paused = true;
      FUNC.api.put(`${props.name}/stop`);
      nodeStatus.value.serviceStatus = "Stopped";
      algodStatus.value = undefined;
      peers.value = undefined;
    }
    if (!val && paused) {
      paused = false;
      await FUNC.api.put(`${props.name}/start`);
      getNodeStatus();
    }
  }
);
</script>
