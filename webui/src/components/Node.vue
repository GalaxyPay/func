<template>
  <div v-if="nodeStatus">
    <v-progress-linear indeterminate v-show="loading" class="mb-n1" />
    <v-container class="pl-5" fluid>
      <v-row>
        <v-col cols="12" sm="6" md="3">
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
          <div class="py-1" v-show="nodeStatus.telemetryStatus">
            <v-badge floating dot class="mx-3 mb-1" :color="telemetryColor" />
            Telemetry
            <a :href="telemetryLink" target="_blank" v-if="telemetryEnabled">
              <v-icon :icon="mdiOpenInNew" class="pb-1" color="primary" />
            </a>
          </div>
          <Manage
            class="mt-4"
            :name="name"
            :node-status="nodeStatus"
            @get-status="getAllStatus()"
          />
        </v-col>
        <template v-if="nodeStatus.serviceStatus === 'Running'">
          <v-col cols="6" md="3" class="text-center">
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
          <v-col cols="6" md="3" class="text-center">
            <div class="text-h4">
              {{
                partDetails?.proposals == null
                  ? "-"
                  : partDetails.proposals.toLocaleString()
              }}
            </div>
            <div>
              Blocks Created
              <span>
                <v-icon :icon="mdiInformation" class="pb-2" />
                <v-tooltip
                  activator="parent"
                  text="via Proposals"
                  location="bottom"
                />
              </span>
            </div>
            <div class="mt-13 text-h4" style="white-space: nowrap">
              {{
                partDetails
                  ? (partDetails.activeStake / 10 ** 6).toLocaleString()
                  : "-"
              }}
            </div>
            <div>Online Stake</div>
          </v-col>
          <v-col
            cols="12"
            sm="6"
            md="3"
            class="text-center"
            align-self="center"
          >
            <div class="text-h4">
              {{
                partDetails?.votes == null
                  ? "-"
                  : partDetails.votes.toLocaleString()
              }}
            </div>
            <div>
              Blocks Certified
              <span>
                <v-icon :icon="mdiInformation" class="pb-2" />
                <v-tooltip
                  activator="parent"
                  text="via Votes"
                  location="bottom"
                />
              </span>
            </div>
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
    <v-container
      class="text-caption text-grey text-center"
      v-show="nodeStatus?.serviceStatus === 'Running'"
    >
      This dashboard does NOT need to be open for the node to run. In fact, it
      is more efficient to close it when you are not using it.
    </v-container>
  </div>
</template>

<script setup lang="ts">
import FUNC from "@/services/api";
import { NodeStatus } from "@/types";
import { checkCatchup, delay } from "@/utils";
import { mdiInformation, mdiOpenInNew, mdiRefresh } from "@mdi/js";
import { Algodv2 } from "algosdk";

const FuncApi = FUNC.api;
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
  nodeStatus.value?.serviceStatus === "Unknown"
    ? "grey"
    : nodeStatus.value?.serviceStatus !== "Not Found"
    ? "success"
    : "red"
);

const runningColor = computed(() =>
  nodeStatus.value?.serviceStatus === "Running" ? "success" : "red"
);

const syncedColor = computed(() =>
  algodStatus.value?.["last-round"] == 0 && !isSyncing.value
    ? "red"
    : isSyncing.value && !generatingKey.value
    ? "warning"
    : runningColor.value
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

const telemetryEnabled = computed(() =>
  nodeStatus.value?.telemetryStatus?.includes("enabled")
);

const telemetryColor = computed(() => {
  if (nodeStatus.value?.serviceStatus !== "Running") return "grey";
  return telemetryEnabled.value ? "success" : "grey";
});

const telemetryLink = computed(() => {
  const ts = nodeStatus.value?.telemetryStatus;
  if (!ts) return undefined;
  const guid = ts.slice(ts.lastIndexOf(" ") + 1);
  return "https://g.nodely.io/d/telemetry?var-GUID=" + guid;
});

const status = computed(() =>
  isSyncing.value
    ? "Syncing"
    : nodeStatus.value
    ? nodeStatus.value.serviceStatus
    : "Unknown"
);

const algodClient = ref<Algodv2>();

onBeforeMount(async () => {
  await getAllStatus();
});

onBeforeUnmount(() => {
  refreshing = false;
});

let refreshing = false;

async function autoRefresh() {
  if (refreshing) return;
  refreshing = true;
  while (refreshing) {
    await getAlgodStatus();
    await delay(1200);
  }
}

async function getAllStatus() {
  await getNodeStatus();
  await getAlgodStatus();
}

async function getNodeStatus() {
  try {
    const oldStatus: NodeStatus | undefined = nodeStatus.value
      ? JSON.parse(JSON.stringify(nodeStatus.value))
      : undefined;
    const resp = await FuncApi.get(props.name);
    nodeStatus.value = resp.data;
    store.machineName = nodeStatus.value?.machineName;
    if (nodeStatus.value?.serviceStatus !== "Running") {
      refreshing = false;
    }
    if (
      nodeStatus.value &&
      (oldStatus?.port !== nodeStatus.value.port ||
        oldStatus?.token !== nodeStatus.value.token)
    ) {
      algodClient.value = new Algodv2(
        nodeStatus.value.token,
        `http://${location.hostname}`,
        nodeStatus.value.port
      );
      await delay(500);
    }
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

let restartAttempted = false;

async function getAlgodStatus() {
  try {
    if (nodeStatus.value?.serviceStatus === "Running") {
      algodStatus.value = await algodClient.value?.status().do();
    } else {
      algodStatus.value = undefined;
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
      console.error("reti not running - attempting restart");
      await FuncApi.put("reti/stop");
      await FuncApi.put("reti/start");
    }
    if (nodeStatus.value?.serviceStatus === "Running" && !refreshing) {
      await delay(1200);
      autoRefresh();
    }
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
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
  try {
    if (!retiUpdate.value) return;
    loading.value = true;
    await FuncApi.post("reti/update");
    await getNodeStatus();
    store.setSnackbar("Reti Updated", "success");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  loading.value = false;
}

watch(
  () => status.value,
  async (val) => {
    if (val === "Syncing") {
      try {
        await checkCatchup(algodStatus.value, props.name);
      } catch (err: any) {
        console.error(err);
        store.setSnackbar(err?.response?.data || err.message, "error");
      }
    }
  }
);

function reloadPartDetails() {
  if (!partDetails.value) return;
  partDetails.value = undefined;
  store.refresh++;
}
</script>
