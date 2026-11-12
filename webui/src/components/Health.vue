<template>
  <section class="node-section">
    <div class="section-head">
      <span class="section-label">Node Health</span>
      <v-spacer />
      <span class="text-caption text-dim">{{ footer }}</span>
    </div>
    <div class="tiles">
      <v-card class="stat-card" v-for="tile in tiles" :key="tile.label">
        <div class="stat-label">{{ tile.label }}</div>
        <div class="stat-value">{{ tile.value }}</div>
        <div class="tile-sub">{{ tile.sub }}</div>
        <Sparkline
          class="mt-2"
          :values="tile.values"
          :times="times"
          :bucket-seconds="health.bucketSeconds"
          :bars="tile.bars"
          :format="tile.format"
          :label="`${tile.label} over the last 24 hours`"
        />
      </v-card>
    </div>
    <div class="lists">
      <div>
        <div class="section-head">
          <span class="section-label">Peers</span>
          <v-spacer />
          <span class="text-caption text-dim" v-if="health.peersAt">
            as of {{ when(health.peersAt) }}
          </span>
        </div>
        <div class="group">
          <div class="row text-dim" v-if="!health.peers.length">
            The node hasn't listed its peers yet. It does so about once an hour.
          </div>
          <div class="row" v-for="(peer, i) in health.peers" :key="i">
            <span class="peer-name ellipsis" :title="peer.host">
              {{ peer.endpoint || peer.address }}
            </span>
            <span class="row-end">
              {{ peer.incoming ? "Incoming" : "Outgoing" }},
              {{ formatDuration(peer.connectedSeconds) }}
            </span>
          </div>
        </div>
      </div>
      <div>
        <div class="section-head">
          <span class="section-label">Recent Errors</span>
        </div>
        <div class="group">
          <div class="row text-dim" v-if="!errors.length">
            No errors in the node's log.
          </div>
          <div class="row" v-for="(note, i) in errors" :key="i">
            <span class="note-text" :title="note.text">
              <span class="text-dim" v-if="note.count > 1">
                {{ note.count.toLocaleString() }}&times;
              </span>
              {{ note.text }}
            </span>
            <span class="row-end">{{ when(note.time) }}</span>
          </div>
        </div>
        <p class="footnote mt-2">The full log is under Manage > View Log.</p>
      </div>
    </div>
  </section>
</template>

<script lang="ts" setup>
import type { NodeHealth } from "@/types";
import { formatDuration, total } from "@/utils/health";
import type { Series } from "@/utils/sparkline";
import type { PropType } from "vue";

const props = defineProps({
  health: { type: Object as PropType<NodeHealth>, required: true },
  now: { type: Date, required: true },
});

const maxErrors = 5;

const times = computed(() => props.health.points.map((p) => p.t));

const count = (noun: string) => (n: number) =>
  `${n.toLocaleString()} ${noun}${n === 1 ? "" : "s"}`;

interface Tile {
  label: string;
  value: string;
  sub: string;
  values: Series;
  bars: boolean;
  format: (value: number) => string;
}

const tiles = computed<Tile[]>(() => {
  const h = props.health;
  const p = h.points;
  const proposals = total(p, "proposals");
  const peers =
    h.peersIn == null && h.peersOut == null
      ? undefined
      : (h.peersIn ?? 0) + (h.peersOut ?? 0);
  return [
    {
      label: "Votes Sent",
      value: total(p, "votes").toLocaleString(),
      sub: `In ${count("block")(total(p, "rounds"))}`,
      values: p.map((x) => x.votes),
      bars: true,
      format: count("vote"),
    },
    {
      label: "Blocks Proposed",
      value: proposals.toLocaleString(),
      sub: proposals
        ? `${total(p, "wins").toLocaleString()} chosen by the network`
        : "None in the last 24 hours",
      values: p.map((x) => x.proposals),
      bars: true,
      format: count("proposal"),
    },
    {
      label: "Peers",
      value: peers == null ? "-" : peers.toLocaleString(),
      sub:
        peers == null
          ? "Not reported yet"
          : `${h.peersOut ?? 0} outgoing, ${h.peersIn ?? 0} incoming`,
      values: p.map((x) =>
        x.peersIn == null && x.peersOut == null
          ? null
          : (x.peersIn ?? 0) + (x.peersOut ?? 0)
      ),
      bars: false,
      format: count("peer"),
    },
  ];
});

const errors = computed(() => props.health.notes.slice(0, maxErrors));

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

const footer = computed(() => {
  const h = props.health;
  const parts: string[] = [];
  if (h.version) parts.push(`algod ${h.version}`);
  if (h.startedAt) {
    const up = (props.now.getTime() - new Date(h.startedAt).getTime()) / 1000;
    parts.push(`up ${formatDuration(up)}`);
  }
  return parts.join(" · ");
});
</script>

<style scoped>
.node-section {
  margin-top: 32px;
}
.tiles {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}
.tiles .stat-card {
  overflow: visible;
}
.tile-sub {
  min-height: 18px;
  font-size: 12px;
  line-height: 1.5;
  color: rgb(var(--v-theme-text-dim));
}
.lists {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  margin-top: 20px;
}
.row {
  display: flex;
  align-items: baseline;
  gap: 12px;
  padding: 9px 14px;
  font-size: 13px;
  line-height: 1.45;
}
.row + .row {
  border-top: 1px solid rgb(var(--v-theme-border));
}
.note-text {
  min-width: 0;
  overflow: hidden;
  overflow-wrap: anywhere;
  display: -webkit-box;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  line-clamp: 2;
}
.row-end {
  flex: none;
  margin-left: auto;
  font-size: 12px;
  color: rgb(var(--v-theme-text-muted));
  white-space: nowrap;
}
.peer-name {
  flex: 0 1 auto;
  font-family: var(--font-mono);
  font-size: 12.5px;
}
@media (max-width: 959.98px) {
  .tiles,
  .lists {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
