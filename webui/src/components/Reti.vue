<template>
  <v-dialog v-model="show" max-width="400" persistent>
    <v-card>
      <v-progress-linear indeterminate v-show="loading" class="mb-n1" />
      <v-card-title class="d-flex">
        Reti Validator
        <v-spacer />
        <v-icon color="currentColor" :icon="mdiClose" @click="show = false" />
      </v-card-title>
      <v-container>
        <div :class="loading ? 'text-grey' : ''">
          <div>Latest: {{ version.latest }}</div>
          <div>Current: {{ version.current }}</div>
        </div>
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
      version.value = { latest: undefined, current: undefined };
      emit("close");
    }
  },
});

const loading = ref(false);
const version = ref({ latest: undefined, current: undefined });

watch(
  () => show.value,
  async (val) => {
    if (val) {
      loading.value = true;
      const releases = await axios({
        url: "https://api.github.com/repos/TxnLab/reti/releases",
      });
      version.value.latest = releases.data[0].tag_name;
      const resp = await axios({
        url:
          "http://localhost:3536/reti/version?latest=" + version.value.latest,
      });
      version.value.current = resp.data.slice(
        27,
        27 + resp.data.slice(27).indexOf(" ")
      );
      loading.value = false;
    }
  }
);
</script>
