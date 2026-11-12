<template>
  <div class="health-details">
    <div class="details-head">
      Peers
      <span class="ml-auto" v-if="health.peersAt">
        as of {{ when(health.peersAt) }}
      </span>
    </div>
    <div class="details-row text-dim" v-if="!health.peers.length">
      Not listed yet. The node lists them about once an hour.
    </div>
    <div class="details-row" v-for="(peer, i) in health.peers" :key="i">
      <span
        class="peer-name ellipsis"
        :title="peerTitle(peer)"
      >
        {{ peerName(peer) }}
      </span>
      <span class="row-end">
        {{ peer.incoming ? "In" : "Out" }} ·
        {{ formatDuration(peer.connectedSeconds) }}
      </span>
    </div>
  </div>
</template>

<script lang="ts" setup>
import type { HealthPeer, NodeHealth } from "@/types";
import { formatDuration } from "@/utils/health";
import type { PropType } from "vue";

const props = defineProps({
  health: { type: Object as PropType<NodeHealth>, required: true },
  now: { type: Date, required: true },
});

// The domain the relays share, such as algorand-mainnet.network, if they all do.
const sharedDomain = computed(() => {
  const domains = props.health.peers.map((p) =>
    hostOf(p).split(".").slice(1).join(".")
  );
  return domains.length > 1 && domains[0].includes(".") && domains.every((d) => d === domains[0])
    ? domains[0]
    : "";
});

function hostOf(peer: HealthPeer) {
  return (peer.endpoint || peer.address || "").replace(/:\d+$/, "");
}

// The column is narrow: drop the port, which is always the same, and the domain
// when every relay shares it, leaving the part that tells them apart.
function peerName(peer: HealthPeer) {
  const host = hostOf(peer);
  return sharedDomain.value ? host.slice(0, -sharedDomain.value.length - 1) : host;
}

// The full address, and the machine behind it when algod names one.
function peerTitle(peer: HealthPeer) {
  return [peer.endpoint || peer.address, peer.host].filter(Boolean).join("\n");
}

function when(time: string) {
  const date = new Date(time);
  const clock = date.toLocaleTimeString(undefined, {
    hour: "numeric",
    minute: "2-digit",
  });
  if (date.toDateString() === props.now.toDateString()) return clock;
  const day = date.toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
  });
  return `${day}, ${clock}`;
}

</script>

<style scoped>
/* Opens under the Node Health row, indented to line up with the row's text. */
.health-details {
  padding: 0 14px 10px 36px;
  font-size: 12.5px;
  line-height: 1.45;
}
.details-head {
  display: flex;
  padding: 8px 0 4px;
  font-size: 11px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: rgb(var(--v-theme-text-dim));
}
.details-row {
  display: flex;
  align-items: baseline;
  gap: 10px;
  padding: 3px 0;
}
.peer-name {
  flex: 0 1 auto;
  min-width: 0;
  font-family: var(--font-mono);
  font-size: 12px;
}
.row-end {
  flex: none;
  margin-left: auto;
  font-size: 11.5px;
  color: rgb(var(--v-theme-text-muted));
  white-space: nowrap;
}
</style>
