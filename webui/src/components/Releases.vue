<template>
  <v-btn variant="tonal" :disabled="store.downloading">
    <v-icon :icon="mdiChevronDown" />
    <v-menu activator="parent" bottom scrim>
      <v-list density="compact">
        <v-list-subheader title="Choose a Release" />
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

const GOALGOWIN = "https://api.github.com/repos/GalaxyPay/go-algo-win";

const store = useAppStore();
const releases = ref();

onMounted(async () => {
  releases.value = (await axios({ url: `${GOALGOWIN}/releases` })).data.map(
    (t: any) => t.name
  );
});
</script>
