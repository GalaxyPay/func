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
    <template v-if="algodStatus?.['catchpoint']">
      <v-data-table
        :headers="headers"
        :items="catchupData"
        density="comfortable"
      >
        <template #[`item.total`]="{ value }">
          {{ value.toLocaleString() }}
        </template>
        <template #[`item.processed`]="{ value }">
          {{ value.toLocaleString() }}
        </template>
        <template #[`item.verified`]="{ value }">
          {{ value.toLocaleString() }}
        </template>
        <template #bottom />
      </v-data-table>
    </template>
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
    <Reti
      :visible="showReti"
      :port="nodeStatus.port"
      :token="nodeStatus.token"
      @close="showReti = false"
    />
  </div>
</template>

<script setup lang="ts">
import AWN from "@/services/api";
import { NodeStatus } from "@/types";
import { checkCatchup, delay } from "@/utils";
import { mdiRefresh } from "@mdi/js";
import { Algodv2 } from "algosdk";

const store = useAppStore();
const props = defineProps({ name: { type: String, required: true } });
const nodeStatus = ref<NodeStatus>();
const loading = ref(false);
const showReti = ref(false);
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
  return (
    current.slice(27, 27 + current.slice(27).indexOf(" ")) !== retiLatest.value
  );
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
  if (!nodeStatus.value?.port) return undefined;
  return new Algodv2(
    nodeStatus.value.token,
    "http://localhost",
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

async function getNodeStatus() {
  const resp = await AWN.api.get(props.name);
  nodeStatus.value = resp.data;
  if (nodeStatus.value?.serviceStatus === "Running") {
    if (algodClient.value) {
      algodStatus.value = await algodClient.value?.status().do();
    }
    if (!refreshing) autoRefresh();
  } else {
    algodStatus.value = undefined;
  }
  if (nodeStatus.value?.retiStatus?.version && !retiLatest.value) {
    const releases = await axios({
      url: "https://api.github.com/repos/algorandfoundation/reti/releases",
    });
    retiLatest.value = releases.data[0].tag_name;
  }
  if (
    nodeStatus.value?.retiStatus?.serviceStatus === "Running" &&
    nodeStatus.value.retiStatus.exeStatus === "Stopped" &&
    !restartAttempted
  ) {
    restartAttempted = true;
    console.error("reti.exe not running - atempting restart");
    await AWN.api.put("reti/stop");
    await AWN.api.put("reti/start");
  }
}

const headers: any[] = [
  { key: "entity", sortable: false },
  { title: "Total", key: "total", sortable: false },
  { title: "Processed", key: "processed", sortable: false },
  { title: "Verified", key: "verified", sortable: false },
];

const catchupData = computed(() => {
  const val: any[] = [];
  if (!algodStatus.value?.["catchpoint"]) return val;
  val.push({
    entity: "Accounts",
    total: algodStatus.value["catchpoint-total-accounts"],
    processed: algodStatus.value["catchpoint-processed-accounts"],
    verified: algodStatus.value["catchpoint-verified-accounts"],
  });
  val.push({
    entity: "KVs",
    total: algodStatus.value["catchpoint-total-kvs"],
    processed: algodStatus.value["catchpoint-processed-kvs"],
    verified: algodStatus.value["catchpoint-verified-kvs"],
  });
  val.push({
    entity: "Blocks",
    total: algodStatus.value["catchpoint-total-blocks"],
    processed: "",
    verified: algodStatus.value["catchpoint-acquired-blocks"],
  });
  return val;
});

async function updateReti() {
  if (!retiUpdate.value) return;
  loading.value = true;
  await AWN.api.post("reti/update");
  await finish("Reti Updated");
}

async function finish(message: string) {
  await getNodeStatus();
  loading.value = false;
  store.setSnackbar(message, "success");
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
  (val) => {
    if (val && nodeStatus.value?.serviceStatus === "Running") {
      paused = true;
      AWN.api.put(`${props.name}/stop`);
      nodeStatus.value.serviceStatus = "Stopped";
    }
    if (!val && paused) {
      paused = false;
      AWN.api.put(`${props.name}/start`);
    }
  }
);
</script>
