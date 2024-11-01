<template>
  <v-app>
    <AppBar />
    <v-main>
      <Loading />
      <NodeTabs />
    </v-main>
    <v-overlay v-model="store.overlay" persistent />
    <Snackbar />
  </v-app>
</template>

<script setup lang="ts">
const store = useAppStore();
</script>

<style>
a {
  color: #2196f3;
}
.no-uppercase {
  text-transform: unset !important;
}
.no-select {
  user-select: none;
}
.pointer {
  cursor: pointer;
}
.arrow {
  cursor: default;
}
.ellipsis {
  text-overflow: ellipsis;
  white-space: nowrap;
  overflow: hidden;
}
.pulsate {
  animation: pulse 3s ease infinite;
}
@keyframes pulse {
  0% {
    transform: scale(1);
    opacity: 1;
  }
  60% {
    transform: scale(1.2);
    opacity: 0.8;
  }
}
</style>
