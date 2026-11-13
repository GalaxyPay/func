<template>
  <section>
    <div class="section-label d-flex">
      Getting Started
      <a href="#" class="ml-auto hide-link" @click.prevent="emit('hide')">
        Hide
        <v-tooltip
          activator="parent"
          location="top"
          text="Just running a node? Hide these steps. The Manage menu and Participation Keys section still have every option."
        />
      </a>
    </div>
    <div class="group">
      <div
        v-for="(step, i) in steps"
        :key="step.title"
        class="step"
        :class="{
          'step--done': i < current,
          'step--current': i === current,
        }"
      >
        <div class="step-marker">
          <v-icon v-if="i < current" :icon="mdiCheck" size="14" />
          <template v-else>{{ i + 1 }}</template>
        </div>
        <div class="step-body">
          <div class="step-title">{{ step.title }}</div>
          <template v-if="i === current">
            <div class="step-desc">{{ step.desc }}</div>
            <div v-if="step.detail" class="step-detail">{{ step.detail }}</div>
            <v-btn
              v-if="step.action"
              color="primary"
              variant="flat"
              size="small"
              class="mt-3"
              :loading="step.busy"
              @click="step.action.run()"
            >
              {{ step.action.label }}
            </v-btn>
          </template>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { NodeStatus, PartDetails } from "@/types";
import { formatAddr } from "@/utils";
import { mdiCheck } from "@mdi/js";
import { useWallet } from "@txnlab/use-wallet-vue";
import { PropType } from "vue";

const props = defineProps({
  nodeStatus: { type: Object as PropType<NodeStatus>, required: true },
  // Synced far enough for the Participation Keys section to show.
  synced: { type: Boolean, required: true },
  catchingUp: { type: Boolean, required: true },
  lastRound: { type: BigInt as unknown as PropType<bigint> },
  partDetails: { type: Object as PropType<PartDetails> },
  generatingKey: { type: Boolean, required: true },
  starting: { type: Boolean, required: true },
});
const emit = defineEmits(["create", "start", "generate", "hide"]);

const { activeAccount } = useWallet();

interface Step {
  title: string;
  desc: string;
  detail?: string;
  action?: { label: string; run: () => void };
  busy?: boolean;
}

const running = computed(() => props.nodeStatus.serviceStatus === "Running");

// The first step not yet done; steps.length when every step is.
const current = computed(() => {
  if (!running.value) return 0;
  if (!props.synced) return 1;
  if (!props.partDetails?.keys) return 2;
  if (!props.partDetails.activeKeys) return 3;
  return 4;
});

const steps = computed<Step[]>(() => {
  const s = props.nodeStatus;
  const found = s.detected;
  return [
    {
      title: "Start your node",
      desc: found
        ? `An existing Algorand node was found at ${found.dataDir}. FUNC won't run a second node beside it: remove that node first, or keep using it as it is.`
        : s.serviceStatus === "Stopped"
          ? "The node's service is set up but stopped."
          : "FUNC runs the node as a background service that starts again whenever this computer restarts.",
      action: found
        ? undefined
        : s.serviceStatus === "Stopped"
          ? { label: "Start Node", run: () => emit("start") }
          : { label: "Set Up Node", run: () => emit("create") },
      busy: props.starting,
    },
    {
      title: "Sync with the network",
      desc: "The node downloads the network's latest state. With fast catchup this usually takes under an hour. You can close this page: the node keeps running.",
      detail: props.catchingUp
        ? "Fast catchup in progress. See its progress below."
        : props.lastRound
          ? `At block ${props.lastRound.toLocaleString()}`
          : "Starting...",
    },
    {
      title: "Generate a participation key",
      desc: "The node makes a key for the account you want to take part in consensus with. You only need the account's address, not a wallet, and generating takes a few minutes.",
      action: props.generatingKey
        ? undefined
        : { label: "Generate Key...", run: () => emit("generate") },
      detail: props.generatingKey ? "Generating key..." : undefined,
    },
    {
      title: "Register the key to go online",
      desc:
        (activeAccount.value
          ? `Wallet connected (${formatAddr(activeAccount.value.address)}). `
          : "Connect the wallet that holds the account using the button at the top right. ") +
        "Then click the key's status button under Participation Keys and choose Register. Using a staking pool or escrow? Copy the key's details into its site instead.",
    },
  ];
});
</script>

<style scoped>
.hide-link {
  font-size: 12px;
  font-weight: 400;
  letter-spacing: normal;
  text-transform: none;
  color: rgb(var(--v-theme-text-dim));
}
.step {
  display: flex;
  gap: 12px;
  padding: 12px 16px;
  font-size: 13.5px;
}
.step + .step {
  border-top: 1px solid rgb(var(--v-theme-border));
}
.step-marker {
  flex: none;
  display: grid;
  place-items: center;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  border: 1px solid rgb(var(--v-theme-border));
  font-size: 11.5px;
  color: rgb(var(--v-theme-text-dim));
}
.step--current .step-marker {
  border-color: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-primary));
}
.step--done .step-marker {
  border-color: rgb(var(--v-theme-success));
  background: rgb(var(--v-theme-success));
  color: rgb(var(--v-theme-on-success));
}
.step-body {
  min-width: 0;
  padding-top: 2px;
}
.step-title {
  color: rgb(var(--v-theme-text-muted));
}
.step--current .step-title {
  font-weight: 500;
  color: inherit;
}
.step--done .step-title {
  color: rgb(var(--v-theme-text-dim));
}
.step-desc {
  margin-top: 4px;
  font-size: 12.5px;
  line-height: 1.5;
  color: rgb(var(--v-theme-text-muted));
}
.step-detail {
  margin-top: 6px;
  font-size: 12.5px;
  color: rgb(var(--v-theme-text-dim));
}
</style>
