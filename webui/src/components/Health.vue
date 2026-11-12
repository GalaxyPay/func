<template>
  <v-dialog v-model="show" max-width="1000" scrollable>
    <v-card>
      <v-card-title class="d-flex align-center">
        Node Health
        <v-spacer />
        <v-icon :icon="mdiClose" @click="show = false" />
      </v-card-title>
      <v-progress-linear indeterminate v-show="loading" height="2" />
      <v-card-text class="health-body">
        <div class="health-bar">
          <v-btn-toggle
            v-model="range"
            mandatory
            divided
            density="compact"
            variant="outlined"
          >
            <v-btn v-for="r in ranges" :key="r.value" :value="r.value" size="small">
              {{ r.title }}
            </v-btn>
          </v-btn-toggle>
          <span class="text-caption text-dim" v-if="health">
            {{ footer }}
          </span>
        </div>
        <v-alert v-if="hint" type="warning" density="compact" :text="hint" />
        <template v-if="health">
          <div class="group checks">
            <div class="check" v-for="check in checks" :key="check.title">
              <span class="status-dot" :class="`bg-${check.color}`" />
              <span class="check-title">{{ check.title }}</span>
              <span class="check-detail">{{ check.detail }}</span>
            </div>
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
                :label="`${tile.label} over the ${rangeTitle}`"
              />
            </v-card>
          </div>
          <section>
            <div class="section-head">
              <span class="section-label">Recent events</span>
              <v-spacer />
              <v-btn size="small" text="View log" @click="emit('showLog')" />
            </div>
            <div class="group">
              <div class="note text-dim" v-if="!health.notes.length">
                No warnings or errors in the node's log.
              </div>
              <div class="note" v-for="(note, i) in notes" :key="i">
                <span class="status-dot" :class="`bg-${noteColor(note.level)}`" />
                <span class="note-text">
                  {{ note.text }}
                  <span class="text-dim" v-if="note.count > 1">
                    ({{ note.count.toLocaleString() }} times)
                  </span>
                </span>
                <span class="note-time">{{ when(note.time) }}</span>
              </div>
              <div class="note" v-if="health.notes.length > notes.length">
                <v-btn
                  size="small"
                  :text="`Show all ${health.notes.length}`"
                  @click="allNotes = true"
                />
              </div>
            </div>
          </section>
          <section v-if="health.peers.length">
            <div class="section-head">
              <span class="section-label">Peers</span>
              <v-spacer />
              <span class="text-caption text-dim" v-if="health.peersAt">
                as of {{ when(health.peersAt) }}
              </span>
            </div>
            <div class="group">
              <div class="peer" v-for="(peer, i) in health.peers" :key="i">
                <span class="peer-name ellipsis">
                  {{ peer.endpoint || peer.address }}
                </span>
                <span class="peer-host ellipsis text-dim">{{ peer.host }}</span>
                <span class="peer-time">
                  {{ peer.incoming ? "Incoming" : "Outgoing" }},
                  {{ formatDuration(peer.connectedSeconds) }}
                </span>
              </div>
            </div>
          </section>
        </template>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import type { NodeHealth } from "@/types";
import { errorMessage } from "@/utils";
import {
  average,
  formatBytes,
  formatDuration,
  formatMs,
  healthChecks,
  total,
} from "@/utils/health";
import type { Series } from "@/utils/sparkline";
import { mdiClose } from "@mdi/js";

const props = defineProps({
  visible: { type: Boolean, required: true },
  name: { type: String, required: true },
  running: { type: Boolean, required: true },
});
const emit = defineEmits(["close", "showLog"]);

const show = computed({
  get() {
    return props.visible;
  },
  set(val) {
    if (!val) emit("close");
  },
});

const ranges = [
  { value: "1h", title: "Hour", period: "last hour" },
  { value: "24h", title: "Day", period: "last 24 hours" },
  { value: "7d", title: "Week", period: "last 7 days" },
  { value: "30d", title: "Month", period: "last 30 days" },
];

const store = useAppStore();
const range = ref("24h");
const health = ref<NodeHealth>();
const loading = ref(false);
const now = ref(new Date());
const allNotes = ref(false);

const rangeTitle = computed(
  () => ranges.find((r) => r.value === range.value)?.period ?? ""
);

// FUNC gathers this in the background, so the dialog only reads it: on opening,
// then every few seconds for as long as it stays open.
const refreshMs = 10000;
let timer: ReturnType<typeof setInterval> | undefined;
let request = 0;

async function refresh(showProgress = false) {
  const id = ++request;
  if (showProgress) loading.value = true;
  try {
    const resp = await store.api.get(`${props.name}/health`, {
      params: { range: range.value },
    });
    // A slower answer for a range the user has since left is out of date.
    if (id !== request) return;
    health.value = resp.data;
    now.value = new Date();
  } catch (err: any) {
    if (id !== request) return;
    // A failed background refresh keeps what is on screen; the next one retries.
    if (showProgress || !health.value) {
      console.error(err);
      store.setSnackbar(errorMessage(err, "Loading node health"), "error");
    }
  } finally {
    if (id === request) loading.value = false;
  }
}

function stop() {
  clearInterval(timer);
  timer = undefined;
  request++;
}

watch(
  show,
  (val) => {
    stop();
    if (!val) return;
    allNotes.value = false;
    refresh(true);
    timer = setInterval(() => refresh(), refreshMs);
  },
  { immediate: true }
);
watch(range, () => {
  if (show.value) refresh(true);
});
onBeforeUnmount(stop);

const checks = computed(() =>
  health.value ? healthChecks(health.value, now.value) : []
);

// What to do when the log can't tell us anything. A stopped node not writing to
// its log is expected, and the page already says the node is stopped.
const hint = computed(() => {
  switch (health.value?.source) {
    case "noLog":
      return "FUNC found no node.log in this node's data directory, so there is nothing to show yet. Start the node, or check that its LogSizeLimit setting isn't 0.";
    case "noEvents":
      return "The node's log has no telemetry events in it, so most of this page stays empty. Check that TelemetryToLog isn't set to false in the node's config.json.";
    case "stale":
      return props.running
        ? "The node is running but isn't writing to its log. Set BaseLoggerDebugLevel to 4 or higher under Manage > Configure to see its health here."
        : undefined;
    default:
      return undefined;
  }
});

const times = computed(() => health.value?.points.map((p) => p.t) ?? []);

interface Tile {
  label: string;
  value: string;
  sub: string;
  values: Series;
  bars: boolean;
  format: (value: number) => string;
}

const count = (noun: string) => (n: number) =>
  `${n.toLocaleString()} ${noun}${n === 1 ? "" : "s"}`;

const tiles = computed<Tile[]>(() => {
  const h = health.value;
  if (!h) return [];
  const p = h.points;
  const max = (key: "roundMsMax" | "arrivalMsMax") =>
    Math.max(0, ...p.map((x) => x[key]));

  const roundMs = average(p, "roundMs", "rounds");
  const arrivalMs = average(p, "arrivalMs", "blocks");
  const recoveries = total(p, "recoveries");
  const warnings = total(p, "warnings");
  const errors = total(p, "errors");
  const peers =
    h.peersIn == null && h.peersOut == null
      ? undefined
      : (h.peersIn ?? 0) + (h.peersOut ?? 0);

  const list: Tile[] = [
    {
      label: "Time per block",
      value: roundMs == null ? "-" : formatMs(roundMs),
      sub:
        roundMs == null
          ? "No blocks seen"
          : `Slowest ${formatMs(max("roundMsMax"))}` +
            (recoveries ? `, ${count("round")(recoveries)} retried` : ""),
      values: p.map((x) => x.roundMs),
      bars: false,
      format: formatMs,
    },
    {
      label: "Block arrival",
      value: arrivalMs == null ? "-" : formatMs(arrivalMs),
      sub:
        arrivalMs == null
          ? "No blocks seen"
          : `After its round began, slowest ${formatMs(max("arrivalMsMax"))}`,
      values: p.map((x) => x.arrivalMs),
      bars: false,
      format: formatMs,
    },
    {
      label: "Votes sent",
      value: total(p, "votes").toLocaleString(),
      sub: `In ${count("block")(total(p, "rounds"))}`,
      values: p.map((x) => x.votes),
      bars: true,
      format: count("vote"),
    },
    {
      label: "Blocks proposed",
      value: total(p, "proposals").toLocaleString(),
      sub: "Proposals this node put forward",
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
    {
      label: "Warnings and errors",
      value: (warnings + errors).toLocaleString(),
      sub: errors ? count("error")(errors) : "No errors",
      values: p.map((x) => x.warnings + x.errors),
      bars: true,
      format: (n) => `${n.toLocaleString()} ${n === 1 ? "entry" : "entries"}`,
    },
  ];

  // Traffic is only known while algod's metrics endpoint answers.
  const received = total(p, "bytesIn");
  if (h.metrics || received > 0) {
    const dropped = total(p, "dropped");
    list.splice(5, 0, {
      label: "Data received",
      value: formatBytes(received),
      sub:
        `Sent ${formatBytes(total(p, "bytesOut"))}` +
        (dropped ? `, ${count("message")(dropped)} dropped` : ""),
      values: p.map((x) => x.bytesIn),
      bars: true,
      format: formatBytes,
    });
  }
  return list;
});

const notes = computed(() =>
  allNotes.value ? health.value!.notes : health.value!.notes.slice(0, 8)
);

function noteColor(level: string) {
  return level === "error" ? "error" : level === "warning" ? "warning" : "icon";
}

function when(time: string) {
  const date = new Date(time);
  const clock = date.toLocaleTimeString(undefined, {
    hour: "numeric",
    minute: "2-digit",
  });
  if (date.toDateString() === now.value.toDateString()) return clock;
  const day = date.toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
  });
  return `${day}, ${clock}`;
}

const footer = computed(() => {
  const h = health.value;
  if (!h) return "";
  const parts: string[] = [];
  if (h.version) parts.push(`algod ${h.version}`);
  if (h.startedAt) {
    const up = (now.value.getTime() - new Date(h.startedAt).getTime()) / 1000;
    parts.push(`running for ${formatDuration(up)}`);
  }
  return parts.join(", ");
});
</script>

<style scoped>
.health-body {
  display: flex;
  flex-direction: column;
  gap: 18px;
  padding: 10px 24px 24px !important;
}
/* The body scrolls; its sections keep their height instead of shrinking to fit. */
.health-body > * {
  flex: none;
}
.health-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}
.health-body .group,
.health-body .stat-card {
  background: rgb(var(--v-theme-background));
}
.checks {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}
.check {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 40px;
  padding: 8px 14px;
  font-size: 13.5px;
  border-bottom: 1px solid rgb(var(--v-theme-border));
}
.check:nth-last-child(-n + 2) {
  border-bottom: 0;
}
.check:nth-child(odd) {
  border-right: 1px solid rgb(var(--v-theme-border));
}
.check-title {
  flex: none;
}
.check-detail {
  margin-left: auto;
  text-align: right;
  color: rgb(var(--v-theme-text-muted));
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
.note,
.peer {
  display: flex;
  align-items: baseline;
  gap: 12px;
  padding: 9px 14px;
  font-size: 13px;
  line-height: 1.45;
}
.note + .note,
.peer + .peer {
  border-top: 1px solid rgb(var(--v-theme-border));
}
.note .status-dot {
  align-self: center;
}
.note-text {
  min-width: 0;
  overflow-wrap: anywhere;
}
.note-time,
.peer-time {
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
.peer-host {
  flex: 1 1 0;
  font-size: 12px;
}
@media (max-width: 959.98px) {
  .tiles {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@media (max-width: 599.98px) {
  .health-body {
    padding: 8px 14px 16px !important;
  }
  .checks,
  .tiles {
    grid-template-columns: minmax(0, 1fr);
  }
  .check {
    border-right: 0 !important;
    border-bottom: 1px solid rgb(var(--v-theme-border)) !important;
  }
  .check:last-child {
    border-bottom: 0 !important;
  }
  .peer-host {
    display: none;
  }
}
</style>
