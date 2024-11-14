<template>
  <v-btn
    variant="tonal"
    color="primary"
    block
    :append-icon="mdiChevronDown"
    :disabled="loading"
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
    <Reti
      :visible="showReti"
      :port="nodeStatus.port"
      :token="nodeStatus.token"
      @close="showReti = false"
    />
  </v-btn>
</template>

<script setup lang="ts">
import AWN from "@/services/api";
import { NodeStatus } from "@/types";
import { mdiChevronDown } from "@mdi/js";
import { PropType } from "vue";

const store = useAppStore();
const props = defineProps({
  name: { type: String, required: true },
  nodeStatus: { type: Object as PropType<NodeStatus>, required: true },
});
const emit = defineEmits(["getStatus"]);
const loading = ref(false);
const showReti = ref(false);
const algodStatus = ref();
const showConfig = ref(false);

const isSyncing = computed(() => !!algodStatus.value?.["catchup-time"]);

const status = computed(() =>
  isSyncing.value
    ? "Syncing"
    : props.nodeStatus
    ? props.nodeStatus.serviceStatus
    : "Unknown"
);

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
  loading.value = true;
  await AWN.api.post(`${props.name}/reset`);
  loading.value = false;
  store.setSnackbar("Data Deleted", "success");
}
</script>
