<template>
  <v-app>
    <AppBar />
    <v-main>
      <Goal />
      <Node v-show="store.ready" name="Algorand" />
      <Node v-show="store.ready" name="Voi" />
    </v-main>
    <v-overlay v-model="store.overlay" persistent />
    <Snackbar />
  </v-app>
</template>

<script setup lang="ts">
const store = useAppStore();
</script>

<style>
.no-uppercase {
  text-transform: unset !important;
}
.no-select {
  user-select: none;
}
.pointer {
  cursor: pointer;
}
</style>
