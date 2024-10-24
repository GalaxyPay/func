<template>
  <v-app>
    <AppBar />
    <v-main>
      <Goal />
      <Node v-show="store.ready && !store.showFNet" name="Algorand" />
      <Node v-show="store.ready && !store.showFNet" name="Voi" />
      <Node v-show="store.ready && store.showFNet" name="FNet" />
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
