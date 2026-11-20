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
          :lines="release.unsupported ? 'two' : 'one'"
          :disabled="!!release.unsupported"
          class="release"
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
// unsupported: set on the last entry only, the newest release that can't follow
// the network's consensus protocol; older ones are left out.
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

<style scoped>
.release {
  max-width: 320px;
}
</style>
