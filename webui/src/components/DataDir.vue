<template>
  <v-dialog v-model="show" max-width="500" persistent>
    <v-card :disabled="loading">
      <v-card-title> Change Data Directory </v-card-title>
      <v-container v-if="dataDir">
        <v-text-field v-model.number="dataDir" density="comfortable">
          <template #append-inner> /{{ name.toLowerCase() }} </template>
        </v-text-field>
      </v-container>
      <v-card-actions>
        <v-btn text="Cancel" variant="tonal" @click="show = false" />
        <v-btn
          text="Save"
          color="primary"
          variant="tonal"
          @click="save()"
          :loading="loading"
        />
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import FUNC from "@/services/api";

const store = useAppStore();

const props = defineProps({
  visible: { type: Boolean, required: true },
  name: { type: String, required: true },
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

const dataDir = ref();
const loading = ref(false);

async function save() {
  try {
    loading.value = true;
    await FUNC.api.put(`${props.name}/dir`, { path: dataDir.value });
    show.value = false;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  loading.value = false;
}

watch(show, async (val) => {
  if (val) {
    try {
      const resp = await FUNC.api.get(`${props.name}/dir`);
      dataDir.value = resp.data;
    } catch (err: any) {
      console.error(err);
      store.setSnackbar(err?.response?.data || err.message, "error");
    }
  }
});
</script>
