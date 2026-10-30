<template>
  <v-snackbar
    v-model="store.snackbar.display"
    :timeout="store.snackbar.timeout"
  >
    <div class="d-flex align-center ga-2">
      <span class="snack-icon" :class="status.bg">
        <v-icon :icon="status.icon" size="12" />
      </span>
      {{ store.snackbar.text }}
    </div>
    <template #actions>
      <v-btn
        v-if="store.snackbar.timeout !== -1"
        text="Close"
        variant="text"
        size="small"
        @click="store.snackbar.display = false"
      />
    </template>
  </v-snackbar>
</template>

<script lang="ts" setup>
import { mdiCheck, mdiExclamation, mdiInformationVariant } from "@mdi/js";

const store = useAppStore();

const status = computed(() => {
  switch (store.snackbar.color) {
    case "success":
      return { bg: "bg-success", icon: mdiCheck };
    case "error":
      return { bg: "bg-danger-fill", icon: mdiExclamation };
    case "warning":
      return { bg: "bg-warning", icon: mdiExclamation };
    default:
      return { bg: "bg-info", icon: mdiInformationVariant };
  }
});
</script>

<style scoped>
.snack-icon {
  display: inline-flex;
  flex: none;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: 50%;
}
</style>
