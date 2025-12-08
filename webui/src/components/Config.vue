<template>
  <v-dialog v-model="show" max-width="800" persistent>
    <v-card :disabled="loading">
      <v-card-title> Configure Node </v-card-title>
      <v-form ref="form" @submit.prevent="saveConfig()">
        <v-container v-if="config">
          <v-text-field
            label="Port"
            v-model.number="port"
            type="number"
            :rules="[portRule]"
            class="pb-2"
          />
          <v-text-field
            label="Token (Read-Only)"
            readonly
            :model-value="token"
            :append-inner-icon="mdiContentCopy"
            @click:append-inner="copyVal(token)"
          />
          <v-select
            label="BaseLoggerDebugLevel"
            v-model="baseLoggerDebugLevel"
            variant="outlined"
            density="comfortable"
            :items="[...Array(6).keys()]"
            hint="Must be 4 or greater for telemetry to work. For best performance, set to 0."
            persistent-hint
            class="pb-2"
          />
          <v-checkbox-btn
            v-model="showDNSBootstrapID"
            label="DNS Bootstrap ID"
          />
          <v-textarea
            :disabled="!showDNSBootstrapID"
            label="DNS Bootstrap ID"
            v-model="config.DNSBootstrapID"
            rows="2"
          />
          <v-radio-group v-model="p2p" density="comfortable">
            <template #label>
              P2P Setting (
              <a
                href="https://dev.algorand.co/nodes/management/p2p-config"
                target="_blank"
              >
                Learn more before enabling </a
              >)
            </template>
            <v-radio label="Off" value="ws" />
            <v-radio label="On" value="p2p" />
            <v-radio label="Hybrid" value="hybrid" />
          </v-radio-group>
        </v-container>
        <v-card-actions>
          <v-spacer />
          <v-btn text="Cancel" variant="tonal" @click="show = false" />
          <v-btn text="Save" color="primary" variant="tonal" type="submit" />
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { networks } from "@/data";
import FUNC from "@/services/api";
import { delay } from "@/utils";
import { mdiContentCopy } from "@mdi/js";

const props = defineProps({
  visible: { type: Boolean, required: true },
  name: { type: String, required: true },
  running: { type: Boolean, required: true },
  token: { type: String },
});
const emit = defineEmits(["close"]);

const show = computed({
  get() {
    return props.visible;
  },
  set(val) {
    if (!val) {
      emit("close");
    }
  },
});

const store = useAppStore();
const config = ref();
const debugConfig = ref();
const form = ref();

const invalidPorts = networks
  .map((n) => n.yarpAlgodPort.toString())
  .concat("3536");
const portRule = (v: string) => {
  return !invalidPorts.includes(v) || "Invalid Port";
};

const port = computed<number>({
  get() {
    if (!config.value?.EndpointAddress) return 0;
    return config.value.EndpointAddress.substring(
      config.value.EndpointAddress.indexOf(":") + 1
    );
  },
  set(val) {
    config.value.EndpointAddress = `0.0.0.0:${val}`;
  },
});

const baseLoggerDebugLevel = computed<number>({
  get() {
    return config.value.BaseLoggerDebugLevel ?? 4;
  },
  set(val) {
    config.value.BaseLoggerDebugLevel = val;
  },
});

const p2pEnabled = computed(
  () => config.value?.EnableP2P ?? debugConfig.value?.EnableP2P
);
const hybridEnabled = computed(
  () =>
    config.value?.EnableP2PHybridMode ?? debugConfig.value?.EnableP2PHybridMode
);

const p2p = computed<"ws" | "p2p" | "hybrid">({
  get() {
    if (hybridEnabled.value) return "hybrid";
    if (p2pEnabled.value) return "p2p";
    return "ws";
  },
  set(val) {
    if (!e2eIsSet) delete config.value.EnableP2P;
    if (!hybridIsSet) delete config.value.EnableP2PHybridMode;
    switch (val) {
      case "ws": {
        if (p2pEnabled.value) config.value.EnableP2P = false;
        if (hybridEnabled.value) config.value.EnableP2PHybridMode = false;
        break;
      }
      case "p2p": {
        if (!p2pEnabled.value) config.value.EnableP2P = true;
        if (hybridEnabled.value) config.value.EnableP2PHybridMode = false;
        break;
      }
      case "hybrid": {
        if (!hybridEnabled.value) config.value.EnableP2PHybridMode = true;
        if (p2pEnabled.value) config.value.EnableP2P = false;
        break;
      }
    }
  },
});

const showDNSBootstrapID = computed({
  get() {
    return config.value?.hasOwnProperty("DNSBootstrapID");
  },
  set(val) {
    if (val) {
      config.value.DNSBootstrapID = undefined;
    } else {
      delete config.value.DNSBootstrapID;
    }
  },
});

const loading = ref(false);

async function saveConfig() {
  const { valid } = await form.value.validate();
  if (!valid) return;
  try {
    loading.value = true;
    await FUNC.api.put(`${props.name}/config`, {
      json: JSON.stringify(config.value, null, 4),
    });
    if (props.running) {
      await FUNC.api.put(`${props.name}/stop`);
      await delay(500);
      await FUNC.api.put(`${props.name}/start`);
    }
    show.value = false;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  loading.value = false;
}

function copyVal(val: string | number | bigint | undefined) {
  if (!val) return;
  navigator.clipboard.writeText(val.toString());
  store.setSnackbar("Copied", "info", 1000);
}

let e2eIsSet: boolean;
let hybridIsSet: boolean;

watch(show, async (val) => {
  if (val) {
    try {
      const resp = await FUNC.api.get(`${props.name}/config`);
      config.value = resp.data;
      e2eIsSet = config.value.EnableP2P != null;
      hybridIsSet = config.value.EnableP2PHybridMode != null;
      const debugPort =
        location.protocol === "https:"
          ? networks.find((n) => n.title === props.name)?.yarpAlgodPort
          : port.value;
      const debugClient = axios.create({
        baseURL: `${location.protocol}//${location.hostname}:${debugPort}/debug`,
        headers: { "X-Algo-Api-Token": props.token },
      });
      const { data } = await debugClient.get("/settings/config");
      debugConfig.value = data;
    } catch (err: any) {
      console.error(err);
      store.setSnackbar(err?.response?.data || err.message, "error");
    }
  }
});
</script>
