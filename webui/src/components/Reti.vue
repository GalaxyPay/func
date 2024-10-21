<template>
  <v-dialog v-model="show" max-width="400" persistent>
    <v-card>
      <v-card-title class="d-flex">
        Reti Validator
        <v-spacer />
        <v-icon color="currentColor" :icon="mdiClose" @click="show = false" />
      </v-card-title>
      <v-container>
        <div>Latest: {{ latest }}</div>
        <div>Version: {{ version }}</div>
      </v-container>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { mdiClose } from "@mdi/js";

const props = defineProps({
  visible: { type: Boolean, required: true },
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

const latest = ref();
const version = ref();

onBeforeMount(async () => {
  const releases = await axios({
    url: "https://api.github.com/repos/TxnLab/reti/releases",
  });
  latest.value = releases.data[0].tag_name;
  const resp = await axios({
    url: "http://localhost:3536/reti/version?latest=" + latest,
  });
  version.value = resp.data.slice(27, 27 + resp.data.slice(27).indexOf(" "));
});
</script>
