<template>
  <v-app>
    <AppBar />
    <v-main>
      <Loading />
      <NodeTabs />
    </v-main>
    <v-overlay v-model="store.overlay" persistent />
    <Snackbar />
    <Login />
  </v-app>
</template>

<script setup lang="ts">
const store = useAppStore();
</script>
