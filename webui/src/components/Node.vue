<template>
  <v-container>
    <v-card v-if="nodeConfig">
      <v-progress-linear indeterminate v-show="loading" class="mb-n1" />
      <v-card-title class="d-flex">
        {{ props.name }} Node <v-spacer />
        <v-btn variant="tonal" :append-icon="mdiChevronDown">
          Manage
          <v-menu activator="parent" bottom :disabled="loading">
            <v-list density="compact">
              <v-list-item
                title="Create Service"
                @click="createService()"
                v-show="nodeConfig.serviceStatus === 'Not Found'"
              />
              <v-list-item
                title="Start Node"
                @click="startService()"
                v-show="nodeConfig.serviceStatus === 'Stopped'"
              />
              <v-list-item
                title="Add Reti Service"
                @click="showReti = true"
                v-show="name === 'Fnet' && nodeStatus === 'Running'"
              />
              <v-list-item
                title="Stop Node"
                @click="stopService()"
                v-show="nodeConfig.serviceStatus === 'Running'"
              />
              <v-list-item
                title="Remove Service"
                @click="deleteService()"
                v-show="nodeConfig.serviceStatus === 'Stopped'"
              />
              <v-list-item
                title="Delete Node Data"
                base-color="error"
                @click="resetNode()"
                v-show="nodeConfig.serviceStatus === 'Not Found'"
              />
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
      />
    </v-card>
  </v-container>
</template>

<script setup lang="ts">
import { NodeConfig } from "@/types";
import { delay } from "@/utils";
import { mdiChevronDown } from "@mdi/js";
import { Algodv2 } from "algosdk";

const CATCHUP_THRESHOLD = 20000; // catchup is triggered if node is this many blocks behind

const store = useAppStore();
const props = defineProps({ name: { type: String, required: true } });
const url = `http://localhost:3536/${props.name}`;
const nodeConfig = ref<NodeConfig>();
const loading = ref(false);
const showReti = ref(false);
const algodStatus = ref();

async function getCatchpoint() {
  switch (props.name) {
    case "Algorand": {
      const resp = await axios({
        url: "https://afmetrics.api.nodely.io/v1/delayed/catchup/label/current",
      });
      return resp.data["last-catchpoint"];
    }
    case "Voi": {
      const resp = await axios({
        url: "https://mainnet-api.voi.nodely.dev/v2/status",
      });
      return resp.data["last-catchpoint"];
    }
    case "Fnet": {
      // const resp = await axios({
      //   url: "https://fnet-api.4160.nodely.io/v2/status",
      // });
      // return resp.data["last-catchpoint"];
      return "1740000#P367HXO7AIWXCPP55MSNN3X6IBX3NGO5RIB53APY7GZ64FRENKKQ";
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
  await getStatus();
});

let refreshing = false;

async function autoRefresh() {
  if (refreshing) return;
  refreshing = true;
  while (nodeConfig.value?.serviceStatus === "Running") {
    await delay(1500);
    await getStatus();
  }
  refreshing = false;
}

async function getStatus() {
  const resp = await axios({ url });
  nodeConfig.value = resp.data;
  if (nodeConfig.value?.serviceStatus === "Running") {
    if (algodClient.value) {
      algodStatus.value = await algodClient.value?.status().do();
    }
    if (!refreshing) autoRefresh();
  } else {
    algodStatus.value = undefined;
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

async function createService() {
  loading.value = true;
  await axios({ url, method: "post" });
  store.setSnackbar("Service Created. Starting...", "success", -1);
  await startService();
}

async function startService() {
  loading.value = true;
  await axios({ url: url + "/start", method: "put" });
  await getStatus();
  loading.value = false;
  store.setSnackbar("Node Started", "success");
}

async function stopService() {
  loading.value = true;
  await axios({ url: url + "/stop", method: "put" });
  await getStatus();
  loading.value = false;
  store.setSnackbar("Node Stopped", "success");
}

async function deleteService() {
  loading.value = true;
  await axios({ url, method: "delete" });
  await getStatus();
  loading.value = false;
  store.setSnackbar("Service Removed", "success");
}

async function resetNode() {
  if (
    !confirm(
      "Are you sure you want to delete all data associated with this node?"
    )
  )
    return;
  loading.value = true;
  await axios({ url: url + "/reset", method: "post" });
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
      await axios({
        url: url + "/catchup",
        method: "post",
        data: { catchpoint },
      });
    }
  }
}

let paused = false;

watch(
  () => store.stopNodeServices,
  (val) => {
    if (val && nodeConfig.value?.serviceStatus === "Running") {
      paused = true;
      axios({ url: url + "/stop", method: "put" });
      nodeConfig.value.serviceStatus = "Stopped";
    }
    if (!val && paused) {
      paused = false;
      startService();
    }
  }
);
</script>
