<template>
  <v-btn variant="tonal" :disabled="store.downloading">
    Select
    <template #append>
      <v-icon :icon="mdiChevronDown" />
    </template>
    <v-menu activator="parent" bottom scrim>
      <v-list density="compact">
        <v-list-subheader title="Choose a Version" />
        <v-list-item
          v-for="release in releases"
          :title="release"
          @click="emit('release', release)"
        />
      </v-list>
    </v-menu>
  </v-btn>
</template>

<script setup lang="ts">
import { mdiChevronDown } from "@mdi/js";

const emit = defineEmits(["release"]);

const store = useAppStore();
const releases = ref();

onMounted(async () => {
  const repo = store.isWindows
    ? "GalaxyPay/go-algo-win"
    : "algorand/go-algorand";
  const url = `https://api.github.com/repos/${repo}/releases`;
  const { data } = await axios({ url });
  releases.value = data
    .map((r: any) => r.tag_name)
    .filter((n: string) => n.toLowerCase().includes("stable"));
});
</script>
