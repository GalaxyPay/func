<template>
  <div v-if="nodeStatus">
    <v-progress-linear indeterminate v-show="loading" class="mb-n1" />
    <v-container class="pl-5" fluid>
      <v-row>
        <v-col cols="12" sm="4">
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
            :status="status"
          />
        </v-col>
        <template v-if="nodeStatus.serviceStatus === 'Running'">
          <v-col cols="12" sm="4" class="text-center">
            <div class="text-h4" style="white-space: nowrap">
              {{
                algodStatus
                  ? algodStatus.lastRound.toLocaleString() || "-"
                  : "-"
              }}
            </div>
            <div>Current Block</div>
            <template v-if="!xs">
              <div class="mt-13 text-h4">
                {{ partDetails ? partDetails.activeKeys : "-" }}
              </div>
              <div>Online Accounts</div>
            </template>
          </v-col>
          <v-col cols="12" sm="4" class="text-center">
            <div class="text-h4">
              {{
                partDetails?.proposals == null
                  ? "-"
                  : partDetails.proposals.toLocaleString()
              }}
            </div>
            <div>
              Blocks Created
              <div>
                <v-btn
                  size="x-small"
                  :text="resetLabel"
                  color="grey"
                  variant="tonal"
                  :disabled="!algodStatus"
                  @click="showResetDialog()"
                />
              </div>
            </div>
            <div class="mt-7 text-h4" style="white-space: nowrap">
              {{
                partDetails
                  ? (partDetails.activeStake / 10 ** 6).toLocaleString()
                  : "-"
              }}
            </div>
            <div>Online Stake</div>
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
      v-if="algodClient && (algodStatus?.lastRound || 0) > 100"
      :name="name"
      :port="nodeStatus.port"
      :token="nodeStatus.token"
      :algod-client="algodClient"
      :status="status"
      :current-round="algodStatus?.lastRound"
      @part-details="(val) => (partDetails = val)"
      @generating-key="(val) => (generatingKey = val)"
      @block-timestamps="(val) => (blockTimestamps = val)"
    />
    <BlocksCalendar
      v-if="name === 'Algorand' && nodeStatus.serviceStatus === 'Running'"
      :name="name"
      :timestamps="blockTimestamps"
    />
    <v-container
      class="text-caption text-grey text-center"
      v-show="nodeStatus?.serviceStatus === 'Running'"
    >
      This dashboard does NOT need to be open for the node to run. In fact, it
      is more efficient to close it when you are not using it.
    </v-container>
    <v-dialog v-model="showReset" max-width="350" persistent>
      <v-card>
        <v-card-title class="d-flex">
          Count Blocks
          <v-spacer />
          <v-icon :icon="mdiClose" @click="showReset = false" />
        </v-card-title>
        <v-card-text>
          <v-radio-group
            class="pa-1"
            v-model="mode"
            color="primary"
            density="comfortable"
            hide-details
          >
            <v-radio value="all" label="All-Time" />
            <v-radio value="year" label="This Year" />
            <v-radio value="month" label="This Month" />
            <v-radio value="today" label="Today" />
            <v-radio value="custom" label="Since..." />
          </v-radio-group>
          <v-text-field
            :disabled="mode !== 'custom'"
            v-model="date"
            label="Date"
            variant="outlined"
            density="compact"
            hide-details
            class="pt-3"
            clearable
            persistent-clear
            hint="YYYY-MM-DD"
          />
        </v-card-text>
        <v-card-actions>
          <v-btn
            text="Save"
            color="primary"
            variant="tonal"
            @click="setResetDate()"
          />
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { networks } from "@/data";
import FUNC from "@/services/api";
import { NodeStatus, PartDetails } from "@/types";
import { checkCatchup, delay, effectiveResetDate } from "@/utils";
import { mdiClose, mdiOpenInNew } from "@mdi/js";
import { Algodv2, modelsv2 } from "algosdk";
import { useDisplay } from "vuetify";

const FuncApi = FUNC.api;
const store = useAppStore();
const { xs } = useDisplay();
const props = defineProps({ name: { type: String, required: true } });
const nodeStatus = ref<NodeStatus>();
const loading = ref(false);
const algodStatus = ref<modelsv2.NodeStatusResponse>();
const retiLatest = ref<string>();
const partDetails = ref<PartDetails>();
const generatingKey = ref(false);
const showReset = ref(false);
const blockTimestamps = ref<number[]>([]);

const retiRunning = computed(
  () => nodeStatus.value?.retiStatus?.exeStatus === "Running"
);

const retiUpdate = computed(() => {
  const current = nodeStatus.value?.retiStatus?.version;
  if (!current || !retiLatest.value) return false;
  const trimL = current.slice(current.indexOf("version") + 8);
  return trimL.slice(0, trimL.indexOf(" ")) !== retiLatest.value;
});

const isSyncing = computed(() => !!algodStatus.value?.catchupTime);

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
  algodStatus.value?.lastRound == 0n && !isSyncing.value
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
let pendingStatus: Promise<modelsv2.NodeStatusResponse> | null = null;
let pendingRound = -1n;

async function autoRefresh() {
  if (refreshing) return;
  refreshing = true;
  while (refreshing) {
    try {
      if (
        nodeStatus.value?.serviceStatus !== "Running" ||
        store.downloading ||
        !algodClient.value
      ) {
        algodStatus.value = undefined;
        await delay(500);
        continue;
      }
      const round = algodStatus.value?.lastRound ?? 0n;
      // Reuse the in-flight statusAfterBlock for the same round so a stalled
      // wait doesn't pile up long-poll connections (browsers cap ~6 per host).
      if (!pendingStatus || pendingRound !== round) {
        pendingRound = round;
        pendingStatus = algodClient.value.statusAfterBlock(round).do();
      }
      // Blocks average ~2.8s. If statusAfterBlock waits noticeably longer the
      // node is likely catching up — where lastRound advances in bursts or
      // stays at 0 during fast catchup, and catchupTime is not always
      // reported — so poll directly to keep the counter and catchup progress
      // live without relying on catchupTime.
      const next = await Promise.race([
        pendingStatus,
        delay(4000).then(() => null),
      ]);
      if (next) {
        pendingStatus = null;
        algodStatus.value = next;
      } else {
        algodStatus.value = await algodClient.value.status().do();
      }
      retry = false;
      await checkReti();
    } catch (err: any) {
      // Drop the cached promise so a rejected statusAfterBlock isn't re-raced.
      pendingStatus = null;
      if (!retry) {
        retry = true;
        await getAllStatus();
      } else {
        console.error(err);
        if (err.status !== 502 && !store.downloading)
          store.setSnackbar(err?.response?.data || err.message, "error");
        await delay(500);
      }
    }
  }
}

watch(
  () => store.refreshStatus,
  async () => await getAllStatus()
);

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
    store.isWindows = nodeStatus.value?.isWindows;
    if (nodeStatus.value?.serviceStatus !== "Running") {
      refreshing = false;
    }
    if (location.protocol === "https:") {
      if (nodeStatus.value && oldStatus?.token !== nodeStatus.value.token) {
        algodClient.value = new Algodv2(
          nodeStatus.value.token,
          `https://${location.hostname}`,
          networks.find((n) => n.title === props.name)?.yarpAlgodPort
        );
      }
    } else if (
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
let retry = false;

async function getAlgodStatus() {
  try {
    if (nodeStatus.value?.serviceStatus === "Running" && !store.downloading) {
      algodStatus.value = await algodClient.value?.status().do();
    } else {
      algodStatus.value = undefined;
    }
    retry = false;
    await checkReti();
    if (nodeStatus.value?.serviceStatus === "Running" && !refreshing) {
      autoRefresh();
    }
  } catch (err: any) {
    if (!retry) {
      retry = true;
      await getAllStatus();
      return;
    }
    console.error(err);
    if (err.status !== 502 && !store.downloading)
      store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function checkReti() {
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
}

const catchupProgress = computed(() => {
  if (!algodStatus.value?.catchpoint) return undefined;
  return [
    {
      name: "Accounts",
      processed:
        ((algodStatus.value.catchpointProcessedAccounts || 0) /
          (algodStatus.value.catchpointTotalAccounts || 0)) *
        100,
      verified:
        ((algodStatus.value.catchpointVerifiedAccounts || 0) /
          (algodStatus.value.catchpointTotalAccounts || 0)) *
        100,
    },
    {
      name: "KVs",
      processed:
        ((algodStatus.value.catchpointProcessedKvs || 0) /
          (algodStatus.value.catchpointTotalKvs || 0)) *
        100,
      verified:
        ((algodStatus.value.catchpointVerifiedKvs || 0) /
          (algodStatus.value.catchpointTotalKvs || 0)) *
        100,
    },
    {
      name: "Blocks",
      processed: 100,
      verified:
        ((algodStatus.value.catchpointAcquiredBlocks || 0) /
          (algodStatus.value.catchpointTotalBlocks || 0)) *
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
  async (val, oldVal) => {
    if (val === "Syncing") {
      try {
        await checkCatchup(algodStatus.value, props.name);
      } catch (err: any) {
        console.error(err);
        store.setSnackbar(err?.response?.data || err.message, "error");
      }
    }
    if (oldVal === "Syncing") reloadPartDetails();
  }
);

function reloadPartDetails() {
  if (!partDetails.value) return;
  partDetails.value = undefined;
  store.refreshPart++;
  showReset.value = false;
}

const date = ref();
const mode = ref<"all" | "year" | "month" | "today" | "custom">("all");
const resetEntry = computed(() =>
  store.resetDates.find((rr) => rr.name === props.name)
);

const resetLabel = computed(() => {
  const e = resetEntry.value;
  if (!e?.date) return "All-Time";
  switch (e.mode) {
    case "year":
      return "This Year";
    case "month":
      return "This Month";
    case "today":
      return "Today";
    default:
      return `Since ${e.date}`;
  }
});

function showResetDialog() {
  const e = resetEntry.value;
  mode.value = !e?.date ? "all" : (e.mode ?? "custom");
  date.value = e?.date ?? new Date().toLocaleDateString();
  showReset.value = true;
}

function setResetDate() {
  let newDate: string | undefined;
  switch (mode.value) {
    case "all":
      newDate = undefined;
      break;
    case "custom":
      newDate = date.value || undefined;
      break;
    default:
      // year / month / today — store a snapshot of the boundary; it is
      // recomputed live on read via effectiveResetDate so the filter
      // refreshes with each block.
      newDate = effectiveResetDate({ date: "snapshot", mode: mode.value });
      break;
  }
  const idx = store.resetDates.findIndex((rr) => rr.name === props.name);
  if (!newDate) {
    if (idx >= 0) store.resetDates.splice(idx, 1);
  } else {
    const entry = {
      name: props.name,
      date: newDate,
      mode: mode.value === "custom" ? "custom" : mode.value,
    } as (typeof store.resetDates)[number];
    if (idx >= 0) store.resetDates[idx] = entry;
    else store.resetDates.push(entry);
  }
  localStorage.setItem("resetDates", JSON.stringify(store.resetDates));
  showReset.value = false;
  reloadPartDetails();
}
</script>
