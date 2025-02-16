<template>
  <v-btn
    variant="tonal"
    color="primary"
    :append-icon="mdiChevronDown"
    :disabled="loading"
    :width="230"
  >
    Manage
    <v-menu activator="parent" bottom scrim>
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
        <v-list-item title="Configure" @click="showConfig = true" />
        <v-list-item
          :title="(telemetryEnabled ? 'Dis' : 'En') + 'able Telemetry'"
          @click="toggleTelemetry()"
          v-show="status === 'Running' && nodeStatus.telemetryStatus"
        />
        <v-list-item
          title="Node Data Directory"
          @click="showDataDir = true"
          v-show="status === 'Not Found'"
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
    <Config
      :visible="showConfig"
      :name="name"
      :running="nodeStatus.serviceStatus === 'Running'"
      :token="nodeStatus.token"
      @close="
        showConfig = false;
        emit('getStatus');
      "
    />
    <DataDir
      :visible="showDataDir"
      :name="name"
      @close="
        showDataDir = false;
        emit('getStatus');
      "
    />
    <Reti
      :visible="showReti"
      :port="nodeStatus.port"
      :token="nodeStatus.token"
      @close="showReti = false"
    />
  </v-btn>
</template>

<script setup lang="ts">
import FUNC from "@/services/api";
import { NodeStatus } from "@/types";
import { delay } from "@/utils";
import { mdiChevronDown } from "@mdi/js";
import { modelsv2 } from "algosdk";
import { PropType } from "vue";

const store = useAppStore();
const props = defineProps({
  name: { type: String, required: true },
  nodeStatus: { type: Object as PropType<NodeStatus>, required: true },
});
const emit = defineEmits(["getStatus"]);
const loading = ref(false);
const showReti = ref(false);
const algodStatus = ref<modelsv2.NodeStatusResponse>();
const showConfig = ref(false);
const showDataDir = ref(false);

const isSyncing = computed(() => !!algodStatus.value?.catchupTime);
const telemetryEnabled = computed(() =>
  props.nodeStatus?.telemetryStatus?.includes("enabled")
);

const status = computed(() =>
  isSyncing.value
    ? "Syncing"
    : props.nodeStatus
    ? props.nodeStatus.serviceStatus
    : "Unknown"
);

async function createNode() {
  try {
    loading.value = true;
    await FUNC.api.post(props.name);
    store.setSnackbar("Service Created. Starting...", "success", -1);
    await startNode();
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function startNode() {
  try {
    loading.value = true;
    await FUNC.api.put(`${props.name}/start`);
    await delay(500);
    await finish("Node Started");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function stopNode() {
  try {
    loading.value = true;
    await FUNC.api.put(`${props.name}/stop`);
    await finish("Node Stopped");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function deleteNode() {
  try {
    loading.value = true;
    await FUNC.api.delete(props.name);
    await finish("Service Removed");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function startReti() {
  try {
    loading.value = true;
    store.stoppingReti = false;
    await FUNC.api.put("reti/start");
    await finish("Reti Started");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function stopReti() {
  try {
    loading.value = true;
    store.stoppingReti = true;
    await FUNC.api.put("reti/stop");
    await finish("Reti Stopped");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function deleteReti() {
  try {
    loading.value = true;
    await FUNC.api.delete("reti");
    await finish("Reti Removed");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function toggleTelemetry() {
  try {
    loading.value = true;
    const action = telemetryEnabled.value ? "Disable" : "Enable";
    await FUNC.api.put(`${props.name}/telemetry/${action}`);
    await finish(`Telemetry ${action}d`);
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function finish(message: string) {
  emit("getStatus");
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
  try {
    loading.value = true;
    await FUNC.api.post(`${props.name}/reset`);
    store.setSnackbar("Data Deleted", "success");
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  loading.value = false;
}
</script>
