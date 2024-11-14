<template>
  <v-dialog v-model="show" max-width="800" persistent>
    <v-card :disabled="loading">
      <v-card-title> Configure Node </v-card-title>
      <v-container v-if="config">
        <v-text-field label="Port" v-model.number="port" type="number" />
        <v-text-field
          label="Token (Read-Only)"
          readonly
          :model-value="token"
          :append-inner-icon="mdiContentCopy"
          @click:append-inner="copyVal(token)"
        />
        <v-checkbox-btn v-model="showDNSBootstrapID" label="DNS Bootstrap ID" />
        <v-textarea
          :disabled="!showDNSBootstrapID"
          label="DNS Bootstrap ID"
          v-model="config.DNSBootstrapID"
          rows="2"
        />
        <v-checkbox-btn v-model="enableP2P" label="Enable P2P" />
        <v-checkbox-btn
          v-model="enableP2PHybridMode"
          label="Enable P2P Hybrid Mode"
        />
      </v-container>
      <v-card-actions>
        <v-btn text="Cancel" variant="tonal" @click="show = false" />
        <v-btn
          text="Save"
          color="primary"
          variant="tonal"
          @click="saveConfig()"
        />
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import AWN from "@/services/api";
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

const port = computed({
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

const enableP2P = computed({
  get() {
    return config.value.EnableP2P;
  },
  set(val) {
    if (val) {
      config.value.EnableP2P = true;
      enableP2PHybridMode.value = false;
    } else {
      delete config.value.EnableP2P;
    }
  },
});

const enableP2PHybridMode = computed({
  get() {
    return config.value.EnableP2PHybridMode;
  },
  set(val) {
    if (val) {
      config.value.EnableP2PHybridMode = true;
      enableP2P.value = false;
    } else {
      delete config.value.EnableP2PHybridMode;
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
  loading.value = true;
  await AWN.api.put(`${props.name}/config`, {
    json: JSON.stringify(config.value, null, 4),
  });
  if (props.running) {
    await AWN.api.put(`${props.name}/stop`);
    await AWN.api.put(`${props.name}/start`);
  }
  show.value = false;
  loading.value = false;
}

function copyVal(val: string | number | bigint | undefined) {
  if (!val) return;
  navigator.clipboard.writeText(val.toString());
  store.setSnackbar("Copied", "info", 1000);
}

watch(show, async (val) => {
  if (val) {
    const resp = await AWN.api.get(`${props.name}/config`);
    config.value = resp.data;
  }
});
</script>
