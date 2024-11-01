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
</style>
