<template>
  <v-btn variant="tonal" :disabled="store.downloading">
    <v-icon :icon="mdiChevronDown" />
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
  const url = "https://api.github.com/repos/algorand/go-algorand/releases";
  releases.value = (await axios({ url })).data
    .map((r: any) => r.name)
    .filter((n: string) => !n.includes("Net"));
});
</script>
