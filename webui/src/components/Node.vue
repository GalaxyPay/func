<template>
  <div v-if="nodeStatus">
    <v-progress-linear indeterminate v-show="loading" class="mb-n1" />
    <v-container class="pl-5">
      <v-row>
        <v-col cols="8">
          <div class="py-1">
            <v-badge floating dot class="mr-3 mb-1" :color="runningColor" />
            Node Running
            <v-chip
              v-show="nodeStatus.retiStatus?.serviceStatus === 'Running'"
              :color="retiUpdate ? 'warning' : 'primary'"
              size="small"
              class="ml-2 mt-1"
              @click="updateReti()"
              :class="retiUpdate ? '' : 'arrow'"
            >
              Reti
              <v-tooltip
                activator="parent"
                location="top"
                :text="retiUpdate ? `Update to ${retiLatest}` : retiLatest"
              />
            </v-chip>
          </div>
          <div class="py-1">
            <v-badge
              floating
              dot
              class="mr-3 mb-1"
              :class="isSyncing ? 'pulsate' : ''"
              :color="syncedColor"
            />
            Node Synced
          </div>
          <div class="py-1">
            <v-badge floating dot class="mr-3 mb-1" color="red" />
            Participating in Concensus
          </div>
        </v-col>
        <v-col class="text-right">
          <v-btn
            variant="tonal"
            :append-icon="mdiChevronDown"
            :disabled="loading"
          >
            Manage
            <v-menu activator="parent" bottom>
              <v-list density="compact">
                <v-list-item
                  title="Create Service"
                  @click="createNode()"
                  v-show="status === 'Not Found'"
                />
                <v-list-item
                  title="Start Node"
                  @click="startNode()"
                  v-show="status === 'Stopped'"
                />
                <v-list-item
                  title="Stop Node"
                  @click="stopNode()"
                  v-show="nodeStatus.serviceStatus === 'Running'"
                />
                <v-list-item
                  title="Remove Service"
                  @click="deleteNode()"
                  v-show="status === 'Stopped'"
                />
                <v-list-item
                  title="Delete Node Data"
                  base-color="error"
                  @click="resetNode()"
                  v-show="status === 'Not Found'"
                />
                <template
                  v-if="
                    nodeStatus.retiStatus &&
                    (status === 'Running' ||
                      ['Running', 'Stopped'].includes(
                        nodeStatus.retiStatus.serviceStatus
                      ))
                  "
                >
                  <v-divider class="ml-6" />
                  <v-list-subheader title="Reti" class="ml-3" />
                  <v-list-item
                    title="Add Reti Service"
                    @click="showReti = true"
                    v-show="
                      nodeStatus.retiStatus.serviceStatus === 'Not Found' &&
                      status === 'Running'
                    "
                  />
                  <v-list-item
                    title="Stop Reti"
                    @click="stopReti()"
                    v-show="nodeStatus.retiStatus.serviceStatus === 'Running'"
                  />
                  <v-list-item
                    title="Start Reti"
                    @click="startReti()"
                    v-show="
                      nodeStatus.retiStatus.serviceStatus === 'Stopped' &&
                      status === 'Running'
                    "
                  />
                  <v-list-item
                    title="Remove Reti"
                    @click="deleteReti()"
                    v-show="nodeStatus.retiStatus.serviceStatus === 'Stopped'"
                  />
                </template>
              </v-list>
            </v-menu>
          </v-btn>
        </v-col>
      </v-row>
    </v-container>
    <v-card-text :class="loading ? 'text-grey' : ''">
      <template v-if="nodeStatus.serviceStatus === 'Running'">
        <div>Current Round: {{ algodStatus?.["last-round"] }}</div>
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
      </template>
    </v-card-text>
    <Participation
      v-if="algodClient && algodStatus?.['last-round'] > 100"
      :port="nodeStatus.port"
      :token="nodeStatus.token"
      :algod-client="algodClient"
      :status="status"
    />
    <Reti
      :visible="showReti"
      :port="nodeStatus.port"
      :token="nodeStatus.token"
      @close="showReti = false"
      @start="startReti()"
    />
  </div>
</template>

<script setup lang="ts">
import AWN from "@/services/api";
import { NodeStatus } from "@/types";
import { delay } from "@/utils";
import { mdiChevronDown } from "@mdi/js";
import { Algodv2 } from "algosdk";

const CATCHUP_THRESHOLD = 20000; // catchup is triggered if node is this many blocks behind
const MAINNET_URL =
  "https://afmetrics.api.nodely.io/v1/delayed/catchup/label/current";
const VOIMAIN_URL = "https://mainnet-api.voi.nodely.dev/v2/status";
// const FNET_URL = "https://fnet-api.4160.nodely.io/v2/status";

const store = useAppStore();
const props = defineProps({ name: { type: String, required: true } });
const nodeStatus = ref<NodeStatus>();
const loading = ref(false);
const showReti = ref(false);
const algodStatus = ref();
const retiLatest = ref<string>();

const retiUpdate = computed(() => {
  const current = nodeStatus.value?.retiStatus?.version;
  if (!current) return false;
  return (
    current.slice(27, 27 + current.slice(27).indexOf(" ")) !== retiLatest.value
  );
});

async function getCatchpoint() {
  switch (props.name) {
    case "Algorand": {
      const resp = await axios({ url: MAINNET_URL });
      return resp.data["last-catchpoint"];
    }
    case "Voi": {
      const resp = await axios({ url: VOIMAIN_URL });
      return resp.data["last-catchpoint"];
    }
    case "FNet": {
      // const resp = await axios({ url: FNET_URL });
      // return resp.data["last-catchpoint"];
      return "1780000#Z36DSLJRFJ3FYPUMFJQK22OTALCIOCFIGNKS26S4VRP6JUWOCYEQ";
    }
  }
}

const isSyncing = computed(() => !!algodStatus.value?.["catchup-time"]);

const runningColor = computed(() =>
  nodeStatus.value?.serviceStatus === "Running" ? "success" : "red"
);

const syncedColor = computed(() =>
  isSyncing.value ? "warning" : runningColor.value
);

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
    if (!isMounted.value) return;
    await delay(1200);
    await getNodeStatus();
  }
  refreshing = false;
}

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
    nodeStatus.value.retiStatus.exeStatus === "Stopped"
  ) {
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

async function createNode() {
  loading.value = true;
  await AWN.api.post(props.name);
  store.setSnackbar("Service Created. Starting...", "success", -1);
  await startNode();
}

async function startNode() {
  loading.value = true;
  await AWN.api.put(`${props.name}/start`);
  await finish("Node Started");
}

async function stopNode() {
  loading.value = true;
  await AWN.api.put(`${props.name}/stop`);
  await finish("Node Stopped");
}

async function deleteNode() {
  loading.value = true;
  await AWN.api.delete(props.name);
  await finish("Service Removed");
}

async function startReti() {
  loading.value = true;
  await AWN.api.put("reti/start");
  await finish("Reti Started");
}

async function stopReti() {
  loading.value = true;
  await AWN.api.put("reti/stop");
  await finish("Reti Stopped");
}

async function deleteReti() {
  loading.value = true;
  await AWN.api.delete("reti");
  await finish("Reti Removed");
}

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

async function resetNode() {
  if (
    !confirm(
      "Are you sure you want to delete all data associated with this node?"
    )
  )
    return;
  loading.value = true;
  await AWN.api.post(`${props.name}/reset`);
  loading.value = false;
  store.setSnackbar("Data Deleted", "success");
}

watch(
  () => status.value,
  (val) => {
    if (val === "Syncing") {
      checkCatchup();
    }
  }
);

async function checkCatchup() {
  if (algodStatus.value?.["catchup-time"]) {
    const catchpoint = await getCatchpoint();
    const catchpointRound = +catchpoint.split("#")[0];
    const isCatchingUp = algodStatus.value?.["catchpoint"] === catchpoint;
    const needsCatchUp =
      catchpointRound - algodStatus.value?.["last-round"] > CATCHUP_THRESHOLD;
    if (!isCatchingUp && needsCatchUp) {
      await AWN.api.post(`${props.name}/catchup`, { catchpoint });
    }
  }
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
      startNode();
    }
  }
);
</script>

<style>
.pulsate {
  animation: pulse 3s ease infinite;
}

@keyframes pulse {
  0% {
    transform: scale(1);
    opacity: 1;
  }
  60% {
    transform: scale(1.2);
    opacity: 0.8;
  }
}
</style>
