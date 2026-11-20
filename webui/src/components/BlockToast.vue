<template>
  <v-snackbar
    v-model="show"
    location="top"
    :timeout="8000"
    class="block-toast"
    role="status"
  >
    <div :key="block?.round.toString()" class="block-toast__body">
      <span class="block-toast__badge">
        <span v-for="n in 10" :key="n" class="block-toast__confetti" />
        <v-icon :icon="mdiCube" size="20" />
      </span>
      <div class="block-toast__text">
        <div class="block-toast__title">Block created!</div>
        <div class="block-toast__detail">
          Round
          <span class="font-mono">{{ block?.round.toLocaleString() }}</span>
          <template v-if="reward">
            <span class="block-toast__sep">·</span>
            <span class="block-toast__reward font-mono">+{{ reward }}</span>
          </template>
        </div>
        <div v-if="block?.address" class="block-toast__addr font-mono">
          {{ formatAddr(block.address, 5) }}
        </div>
      </div>
    </div>
    <template #actions>
      <v-btn
        :icon="mdiClose"
        variant="text"
        size="small"
        aria-label="Close"
        @click="show = false"
      />
    </template>
  </v-snackbar>
</template>

<script lang="ts" setup>
import { CreatedBlock } from "@/types";
import { formatAddr } from "@/utils";
import { mdiClose, mdiCube } from "@mdi/js";
import type { PropType } from "vue";

const props = defineProps({
  block: { type: Object as PropType<CreatedBlock> },
});

const show = ref(false);

// Each new block re-opens the toast, restarting its timer and animation.
watch(
  () => props.block,
  async (val) => {
    if (!val) return;
    show.value = false;
    await nextTick();
    show.value = true;
  }
);

const reward = computed(() => {
  if (!props.block?.payout) return undefined;
  const amount = Number(props.block.payout) / 10 ** 6;
  return `${amount.toLocaleString(undefined, { maximumFractionDigits: 3 })} ${props.block.unit}`;
});
</script>

<!-- Not scoped: the snackbar renders in Vuetify's overlay container. -->
<style>
.v-snackbar.block-toast {
  margin-top: 72px;
}
.v-snackbar.block-toast .v-snackbar__wrapper {
  background: rgb(var(--v-theme-surface-bright));
  color: rgb(var(--v-theme-on-surface));
  border: 1px solid rgb(var(--v-theme-primary), 0.45);
  box-shadow:
    0 0 0 4px rgb(var(--v-theme-primary), 0.08),
    0 12px 32px rgb(var(--v-theme-primary), 0.18),
    var(--shadow-snackbar);
  animation: block-toast-pop 420ms cubic-bezier(0.2, 1.4, 0.4, 1);
}
.v-snackbar.block-toast .v-snackbar__actions .v-btn {
  color: rgb(var(--v-theme-text-muted)) !important;
}
.block-toast__body {
  display: flex;
  align-items: center;
  gap: 14px;
}
.block-toast__badge {
  position: relative;
  display: inline-flex;
  flex: none;
  align-items: center;
  justify-content: center;
  width: 38px;
  height: 38px;
  border-radius: 10px;
  background: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-on-primary));
  animation:
    block-toast-drop 520ms cubic-bezier(0.3, 1.6, 0.5, 1) both,
    block-toast-glow 1.6s ease-out 300ms;
}
.block-toast__title {
  font-size: 15px;
  font-weight: 600;
  letter-spacing: 0.01em;
}
.block-toast__detail {
  font-size: 13px;
  color: rgb(var(--v-theme-text-body));
}
.block-toast__sep {
  margin: 0 6px;
  color: rgb(var(--v-theme-text-dim));
}
.block-toast__reward {
  color: rgb(var(--v-theme-success));
  font-weight: 500;
}
.block-toast__addr {
  font-size: 11px;
  color: rgb(var(--v-theme-text-dim));
}

/* Ten pieces of confetti burst out of the badge, each at its own angle. */
.block-toast__confetti {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 6px;
  height: 6px;
  margin: -3px;
  border-radius: 1px;
  opacity: 0;
  animation: block-toast-burst 900ms cubic-bezier(0.1, 0.8, 0.3, 1) 180ms;
}
.block-toast__confetti:nth-child(4n + 1) {
  background: rgb(var(--v-theme-primary));
}
.block-toast__confetti:nth-child(4n + 2) {
  background: rgb(var(--v-theme-success));
}
.block-toast__confetti:nth-child(4n + 3) {
  background: rgb(var(--v-theme-warning));
}
.block-toast__confetti:nth-child(4n) {
  background: rgb(var(--v-theme-info));
  border-radius: 50%;
}
.block-toast__confetti:nth-child(1) {
  --a: 0deg;
  --d: 34px;
}
.block-toast__confetti:nth-child(2) {
  --a: 36deg;
  --d: 28px;
}
.block-toast__confetti:nth-child(3) {
  --a: 72deg;
  --d: 38px;
}
.block-toast__confetti:nth-child(4) {
  --a: 108deg;
  --d: 30px;
}
.block-toast__confetti:nth-child(5) {
  --a: 144deg;
  --d: 36px;
}
.block-toast__confetti:nth-child(6) {
  --a: 180deg;
  --d: 27px;
}
.block-toast__confetti:nth-child(7) {
  --a: 216deg;
  --d: 35px;
}
.block-toast__confetti:nth-child(8) {
  --a: 252deg;
  --d: 31px;
}
.block-toast__confetti:nth-child(9) {
  --a: 288deg;
  --d: 38px;
}
.block-toast__confetti:nth-child(10) {
  --a: 324deg;
  --d: 29px;
}

@keyframes block-toast-pop {
  0% {
    transform: translateY(-12px) scale(0.92);
  }
  100% {
    transform: none;
  }
}
@keyframes block-toast-drop {
  0% {
    transform: translateY(-14px) rotate(-25deg) scale(0.4);
  }
  100% {
    transform: none;
  }
}
@keyframes block-toast-glow {
  0% {
    box-shadow: 0 0 0 0 rgb(var(--v-theme-primary), 0.6);
  }
  100% {
    box-shadow: 0 0 0 14px rgb(var(--v-theme-primary), 0);
  }
}
@keyframes block-toast-burst {
  0% {
    opacity: 1;
    transform: rotate(var(--a)) translateX(0) scale(1);
  }
  100% {
    opacity: 0;
    transform: rotate(var(--a)) translateX(var(--d)) rotate(180deg) scale(0.6);
  }
}
@media (prefers-reduced-motion: reduce) {
  .v-snackbar.block-toast .v-snackbar__wrapper,
  .block-toast__badge,
  .block-toast__confetti {
    animation: none;
  }
}
</style>
