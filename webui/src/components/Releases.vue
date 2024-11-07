<template>
  <v-btn
    variant="tonal"
    :append-icon="mdiChevronDown"
    :disabled="store.downloading"
  >
    Advanced
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

const props = defineProps({
  algowin: { type: String, required: true },
});
const emit = defineEmits(["release"]);

const store = useAppStore();
const releases = ref();

onMounted(async () => {
  releases.value = (await axios({ url: `${props.algowin}/releases` })).data.map(
    (t: any) => t.name
  );
});
</script>
