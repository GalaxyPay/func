<template>
  <v-container>
    <v-card v-if="nodeConfig">
      <v-progress-linear indeterminate v-show="loading" class="mb-n1" />
      <v-card-title class="d-flex">
        {{ props.name }} Node
        <v-chip
          v-show="retiStatus?.serviceStatus === 'Running'"
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
            :text="
              retiUpdate
                ? `Update to ${retiVersion.latest}`
                : retiVersion.latest
            "
          />
        </v-chip>
        <v-spacer />
        <v-btn variant="tonal" :append-icon="mdiChevronDown">
          Manage
          <v-menu activator="parent" bottom :disabled="loading">
            <v-list density="compact">
              <v-list-item
                title="Create Service"
                @click="createNode()"
                v-show="nodeStatus === 'Not Found'"
              />
              <v-list-item
                title="Start Node"
                @click="startNode()"
                v-show="nodeStatus === 'Stopped'"
              />
              <v-list-item
                title="Stop Node"
                @click="stopNode()"
                v-show="nodeConfig.serviceStatus === 'Running'"
              />
              <v-list-item
                title="Remove Service"
                @click="deleteNode()"
                v-show="nodeStatus === 'Stopped'"
              />
              <v-list-item
                title="Delete Node Data"
                base-color="error"
                @click="resetNode()"
                v-show="nodeStatus === 'Not Found'"
              />
              <template v-if="name === 'FNet'">
                <v-divider class="ml-6" />
                <v-list-subheader title="Reti" class="ml-3" />
                <v-list-item
                  title="Add Reti Service"
                  @click="showReti = true"
                  v-show="
                    retiStatus?.serviceStatus === 'Not Found' &&
                    nodeStatus === 'Running'
                  "
                />
                <v-list-item
                  title="Stop Reti"
                  @click="stopReti()"
                  v-show="retiStatus?.serviceStatus === 'Running'"
                />
                <v-list-item
                  title="Start Reti"
                  @click="startReti()"
                  v-show="retiStatus?.serviceStatus === 'Stopped'"
                />
                <v-list-item
                  title="Remove Reti"
                  @click="deleteReti()"
                  v-show="retiStatus?.serviceStatus === 'Stopped'"
                />
              </template>
            </v-list>
          </v-menu>
        </v-btn>
      </v-card-title>
      <v-card-text :class="loading ? 'text-grey' : ''">
        <div v-if="nodeConfig.serviceStatus !== 'Not Found'">
          Status: {{ nodeStatus }}
        </div>
        <template v-if="nodeConfig.serviceStatus === 'Running'">
          <div>Last Round: {{ algodStatus?.["last-round"] }}</div>
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
        :port="nodeConfig.port"
        :token="nodeConfig.token"
        :algod-client="algodClient"
        :node-status="nodeStatus"
      />
      <Reti
        :visible="showReti"
        :port="nodeConfig.port"
        :token="nodeConfig.token"
        @close="showReti = false"
        @start="startReti()"
      />
    </v-card>
  </v-container>
</template>

<script setup lang="ts">
import AWN from "@/services/api";
import { NodeConfig } from "@/types";
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
const nodeConfig = ref<NodeConfig>();
const loading = ref(false);
const showReti = ref(false);
const algodStatus = ref();
const retiStatus = ref();
const emptyVersion = JSON.stringify({ latest: undefined, current: undefined });
const retiVersion = ref(JSON.parse(emptyVersion));

const retiUpdate = computed(
  () => retiVersion.value.current !== retiVersion.value.latest
);

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

const nodeStatus = computed(() =>
  algodStatus.value?.["catchup-time"]
    ? "Syncing"
    : nodeConfig.value
    ? nodeConfig.value.serviceStatus
    : "Unknown"
);

const algodClient = computed(() => {
  if (!nodeConfig.value?.port) return undefined;
  return new Algodv2(
    nodeConfig.value.token,
    "http://localhost",
    nodeConfig.value.port
  );
});

onBeforeMount(async () => {
  await getNodeStatus();
});

let refreshing = false;

async function autoRefresh() {
  if (refreshing) return;
  refreshing = true;
  while (nodeConfig.value?.serviceStatus === "Running") {
    await delay(1500);
    await getNodeStatus();
  }
  refreshing = false;
}

async function getNodeStatus() {
  const resp = await AWN.api.get(props.name);
  nodeConfig.value = resp.data;
  if (nodeConfig.value?.serviceStatus === "Running") {
    if (algodClient.value) {
      algodStatus.value = await algodClient.value?.status().do();
    }
    if (!refreshing) autoRefresh();
  } else {
    algodStatus.value = undefined;
  }
  if (props.name === "FNet") {
    const reti = await AWN.api.get("reti");
    retiStatus.value = reti.data;
    if (retiStatus.value.version) {
      const releases = await axios({
        url: "https://api.github.com/repos/TxnLab/reti/releases",
      });
      retiVersion.value.latest = releases.data[0].tag_name;
      retiVersion.value.current = retiStatus.value.version.slice(
        27,
        27 + retiStatus.value.version.slice(27).indexOf(" ")
      );
    }
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
  await stopReti();
  await AWN.api.post("reti/update", { latest: retiVersion.value.latest });
  await startReti();
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
  () => nodeStatus.value,
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
    if (val && nodeConfig.value?.serviceStatus === "Running") {
      paused = true;
      AWN.api.put(`${props.name}/stop`);
      nodeConfig.value.serviceStatus = "Stopped";
    }
    if (!val && paused) {
      paused = false;
      startNode();
    }
  }
);
</script>
