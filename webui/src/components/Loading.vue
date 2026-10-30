<template>
  <div class="loading" v-if="!store.ready || store.downloading">
    <v-progress-circular :size="40" :width="3" indeterminate />
    <div v-if="store.downloading" class="text-muted">
      Downloading node software, please wait
    </div>
  </div>
</template>

<script setup lang="ts">
const store = useAppStore();
</script>

<style scoped>
.loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  padding: 64px 16px;
}
</style>
