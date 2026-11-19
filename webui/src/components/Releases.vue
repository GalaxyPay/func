<template>
  <v-btn variant="outlined" size="small" :disabled="store.downloading">
    Select
    <template #append>
      <v-icon :icon="mdiChevronDown" size="16" />
    </template>
    <v-menu activator="parent" bottom scrim>
      <v-list density="compact">
        <v-list-subheader title="Choose a Version" />
        <v-list-item
          v-for="release in releases"
          :key="release.name"
          :title="release.name"
          :subtitle="release.unsupported ?? undefined"
          :disabled="!!release.unsupported"
          @click="emit('release', release.name)"
        />
      </v-list>
    </v-menu>
  </v-btn>
</template>

<script setup lang="ts">
import { errorMessage } from "@/utils";
import { mdiChevronDown } from "@mdi/js";

const emit = defineEmits(["release"]);

const store = useAppStore();
// unsupported: why the release can't follow the network's consensus protocol.
const releases = ref<{ name: string; unsupported: string | null }[]>();

onMounted(async () => {
  try {
    const { data } = await store.api.get("goal/releases");
    releases.value = data;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(errorMessage(err, "Load node releases"), "error");
  }
});
</script>
