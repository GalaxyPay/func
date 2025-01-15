<template>
  <v-container
    class="text-center font-italic"
    v-if="!store.ready || store.downloading"
  >
    <div v-if="store.downloading">Downloading node software, please wait</div>
    <v-container>
      <v-progress-circular :size="60" color="primary" indeterminate />
    </v-container>
  </v-container>
</template>

<script setup lang="ts">
const store = useAppStore();
</script>
