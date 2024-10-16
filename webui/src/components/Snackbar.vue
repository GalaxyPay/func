<template>
  <v-snackbar
    v-model="store.snackbar.display"
    :timeout="store.snackbar.timeout"
    :color="store.snackbar.color"
    :text="store.snackbar.text"
  >
    <template #actions>
      <v-btn
        v-if="store.snackbar.timeout !== -1"
        text="Close"
        variant="text"
        color="currentColor"
        @click="store.snackbar.display = false"
      />
    </template>
  </v-snackbar>
</template>

<script lang="ts" setup>
const store = useAppStore();
</script>
