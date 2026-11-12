<template>
  <v-dialog v-model="show" max-width="1100" scrollable>
    <v-card>
      <v-card-title class="d-flex align-center">
        Node Log
        <v-spacer />
        <v-icon :icon="mdiClose" @click="show = false" />
      </v-card-title>
      <div class="log-bar">
        <v-text-field
          v-model="query"
          class="log-search"
          label="Search"
          placeholder="Text, address, round..."
          density="compact"
          hide-details
          clearable
          :prepend-inner-icon="mdiMagnify"
          @keydown.enter="load()"
        />
        <v-select
          v-model="level"
          class="log-level"
          label="Show"
          :items="levels"
          :item-props="levelProps"
          density="compact"
          hide-details
        />
        <!-- A disabled switch shows no tooltip of its own, so the wrapper has it. -->
        <span class="log-follow">
          <v-switch
            v-model="follow"
            label="Follow"
            hide-details
            :disabled="level === everything"
          />
          <v-tooltip
            activator="parent"
            location="top"
            :text="
              level === everything
                ? 'Choose Errors or Warnings to follow new entries. A busy node writes too much to follow everything.'
                : 'Add new entries as the node writes them'
            "
          />
        </span>
        <v-btn
          variant="outlined"
          size="small"
          :disabled="!lines.length"
          :prepend-icon="mdiDownload"
          text="Download"
          @click="download()"
        />
      </div>
      <v-progress-linear indeterminate :active="loading" height="2" />
      <v-card-text class="log-body">
        <v-alert
          v-if="loaded && !found"
          type="warning"
          density="compact"
          text="This node has no node.log yet. It is created when the node first starts."
        />
        <template v-else>
          <div class="group" v-if="lines.length">
            <div class="entry" v-for="line in lines" :key="line.offset">
              <div
                class="entry-head"
                role="button"
                tabindex="0"
                :aria-expanded="open.has(line.offset)"
                @click="toggle(line.offset)"
                @keydown.enter="toggle(line.offset)"
              >
                <span class="entry-time" :title="fullTime(line)">
                  {{ shortTime(line) }}
                </span>
                <span class="status-dot" :class="`bg-${color(line.level)}`">
                  <v-tooltip activator="parent" location="top" :text="line.level" />
                </span>
                <span class="entry-msg">{{ line.msg }}</span>
                <v-icon
                  :icon="open.has(line.offset) ? mdiChevronUp : mdiChevronDown"
                  size="18"
                  class="entry-toggle"
                />
              </div>
              <div class="entry-raw" v-if="open.has(line.offset)">
                <pre>{{ pretty(line.raw) }}</pre>
                <v-btn
                  size="small"
                  variant="outlined"
                  :prepend-icon="mdiContentCopy"
                  text="Copy"
                  @click="copyToClipboard(line.raw)"
                />
              </div>
            </div>
          </div>
          <div class="log-end" v-if="loaded">
            <span class="text-dim">{{ summary }}</span>
            <v-btn
              v-if="next != null"
              variant="outlined"
              size="small"
              :disabled="loading"
              :text="lines.length ? 'Load older' : 'Keep searching'"
              @click="loadOlder()"
            />
          </div>
        </template>
        <p class="footnote">
          A node keeps only its most recent log and replaces the oldest entries as
          it fills up. On a busy network that can be just the last few hours.
        </p>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import type { LogLine, LogPage } from "@/types";
import { copyToClipboard, errorMessage } from "@/utils";
import {
  mdiChevronDown,
  mdiChevronUp,
  mdiClose,
  mdiContentCopy,
  mdiDownload,
  mdiMagnify,
} from "@mdi/js";

const props = defineProps({
  visible: { type: Boolean, required: true },
  name: { type: String, required: true },
});
const emit = defineEmits(["close"]);

const show = computed({
  get() {
    return props.visible;
  },
  set(val) {
    if (!val) emit("close");
  },
});

const everything = "info";
const levels = [
  { value: "error", title: "Errors" },
  { value: "warning", title: "Warnings and errors" },
  { value: everything, title: "Everything" },
];

// A busy node writes hundreds of entries a second, more than following can keep
// up with, so following and showing everything rule each other out.
function levelProps(item: (typeof levels)[number]) {
  return {
    title: item.title,
    value: item.value,
    disabled: follow.value && item.value === everything,
  };
}

// How many entries to ask for at a time, and to keep on the page at most.
const pageSize = 200;
const maxLines = 2000;
// FUNC reads a limited stretch of the log per request, so sparse matches take
// several. One load keeps going until it has this many entries, for at most this
// many requests, which is enough to cover a full-size log.
const minLines = 20;
const maxPages = 16;
const followMs = 3000;
const searchDelayMs = 400;

const store = useAppStore();
const query = ref<string | null>("");
const level = ref("warning");
const follow = ref(false);
const lines = ref<LogLine[]>([]);
const next = ref<number | null>(null);
const end = ref(0);
const found = ref(true);
const loaded = ref(false);
const loading = ref(false);
const open = ref(new Set<number>());

let request = 0;
let followTimer: ReturnType<typeof setInterval> | undefined;
let searchTimer: ReturnType<typeof setTimeout> | undefined;

function fetchPage(params: Record<string, unknown>) {
  return store.api.get<LogPage>(`${props.name}/log`, {
    params: { q: query.value?.trim() || undefined, level: level.value, ...params },
  });
}

// Runs one exchange with the server, ignoring it if the filters changed meanwhile.
async function run(work: (current: () => boolean) => Promise<void>) {
  const id = ++request;
  loading.value = true;
  try {
    await work(() => id === request);
  } catch (err: any) {
    if (id !== request) return;
    console.error(err);
    store.setSnackbar(errorMessage(err, "Loading the node log"), "error");
  } finally {
    if (id === request) loading.value = false;
  }
}

// Adds older entries to the page, going back from the cursor until there are
// `wanted` more, the start of the log is reached, or the request allowance is spent.
async function extendBack(wanted: number, current: () => boolean) {
  let added = 0;
  for (let pages = 0; pages < maxPages && next.value != null; pages++) {
    const { data } = await fetchPage({ before: next.value, limit: pageSize });
    if (!current()) return;
    lines.value = lines.value.concat(data.lines);
    next.value = data.next ?? null;
    added += data.lines.length;
    if (added >= wanted) return;
  }
}

// Start again from the newest entries.
function load() {
  clearTimeout(searchTimer);
  return run(async (current) => {
    const { data } = await fetchPage({ limit: pageSize });
    if (!current()) return;
    lines.value = data.lines;
    next.value = data.next ?? null;
    end.value = data.end;
    found.value = data.found;
    open.value = new Set();
    loaded.value = true;
    // Warnings can be far apart: look further back until there is a screenful.
    if (data.found && data.lines.length < minLines)
      await extendBack(minLines - data.lines.length, current);
  });
}

function loadOlder() {
  return run((current) => extendBack(minLines, current));
}

// Entries written since the last look, added at the top.
async function loadNewer() {
  if (loading.value || !loaded.value || !found.value) return;
  const id = request;
  try {
    const { data } = await fetchPage({ after: end.value, limit: 1000 });
    if (id !== request) return;
    if (data.reset) {
      // The node started a new log file; positions in the old one mean nothing now.
      await load();
      return;
    }
    end.value = data.end;
    if (!data.lines.length) return;
    let merged = data.lines.concat(lines.value);
    if (merged.length > maxLines) {
      merged = merged.slice(0, maxLines);
      // "Load older" then resumes from the last entry still shown.
      next.value = merged[merged.length - 1].offset;
    }
    lines.value = merged;
  } catch (err) {
    // A missed refresh is made up by the next one.
    console.error(err);
  }
}

function stopFollowing() {
  clearInterval(followTimer);
  followTimer = undefined;
}

watch(
  [show, follow],
  ([visible, following]) => {
    stopFollowing();
    if (visible && following) followTimer = setInterval(loadNewer, followMs);
  },
  { immediate: true }
);

watch(
  show,
  (visible) => {
    if (visible) {
      load();
    } else {
      // Drop anything still on its way and forget the page.
      request++;
      clearTimeout(searchTimer);
      loading.value = false;
      loaded.value = false;
      lines.value = [];
      follow.value = false;
    }
  },
  { immediate: true }
);

watch(level, () => {
  if (show.value) load();
});
watch(query, () => {
  if (!show.value) return;
  clearTimeout(searchTimer);
  searchTimer = setTimeout(load, searchDelayMs);
});
onBeforeUnmount(() => {
  stopFollowing();
  clearTimeout(searchTimer);
  request++;
});

const summary = computed(() => {
  const count = lines.value.length;
  const entries = `${count.toLocaleString()} ${count === 1 ? "entry" : "entries"}`;
  if (next.value != null) {
    return count
      ? `${entries} so far`
      : "Nothing found yet. There is more of the log to search.";
  }
  return count ? `${entries}. That is all of them.` : "No matching entries in the log.";
});

function toggle(offset: number) {
  const set = new Set(open.value);
  if (!set.delete(offset)) set.add(offset);
  open.value = set;
}

function color(level: string) {
  if (level === "warning") return "warning";
  return ["error", "fatal", "panic"].includes(level) ? "error" : "icon";
}

function shortTime(line: LogLine) {
  if (!line.time) return "";
  const date = new Date(line.time);
  const clock = date.toLocaleTimeString(undefined, {
    hour: "numeric",
    minute: "2-digit",
    second: "2-digit",
  });
  if (date.toDateString() === new Date().toDateString()) return clock;
  const day = date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
  return `${day}, ${clock}`;
}

function fullTime(line: LogLine) {
  return line.time ? new Date(line.time).toLocaleString() : "";
}

// The entry's JSON laid out for reading; anything else is shown as written.
function pretty(raw: string) {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

// Saves what is on the page, oldest first, as the node wrote it.
function download() {
  const text = lines.value
    .slice()
    .reverse()
    .map((l) => l.raw)
    .join("\n");
  const url = URL.createObjectURL(new Blob([text + "\n"], { type: "text/plain" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = `${props.name.toLowerCase()}-node-log.txt`;
  link.click();
  URL.revokeObjectURL(url);
}
</script>

<style scoped>
.log-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  padding: 4px 24px 14px;
}
.log-search {
  flex: 1 1 240px;
}
.log-level {
  flex: 0 1 220px;
}
.log-follow {
  flex: none;
}
.log-body {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 14px 24px 24px !important;
}
.log-body > * {
  flex: none;
}
.log-body .group {
  background: rgb(var(--v-theme-background));
}
.entry + .entry {
  border-top: 1px solid rgb(var(--v-theme-border));
}
.entry-head {
  display: flex;
  align-items: baseline;
  gap: 12px;
  padding: 8px 14px;
  font-size: 13px;
  line-height: 1.45;
  cursor: pointer;
}
.entry-head:hover,
.entry-head:focus-visible {
  background: rgb(var(--v-theme-surface));
  outline: none;
}
.entry-time {
  flex: none;
  min-width: 84px;
  font: 12px/1.45 var(--font-mono);
  color: rgb(var(--v-theme-text-muted));
  white-space: nowrap;
}
.entry-head .status-dot,
.entry-toggle {
  align-self: center;
}
.entry-msg {
  flex: 1 1 0;
  min-width: 0;
  overflow-wrap: anywhere;
}
.entry-toggle {
  flex: none;
  color: rgb(var(--v-theme-icon));
}
.entry-raw {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 10px;
  padding: 4px 14px 14px;
}
.entry-raw pre {
  width: 100%;
  max-height: 320px;
  overflow: auto;
  padding: 10px 12px;
  border: 1px solid rgb(var(--v-theme-border));
  border-radius: 8px;
  background: rgb(var(--v-theme-surface));
  font: 12px/1.5 var(--font-mono);
  color: rgb(var(--v-theme-text-body));
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
.log-end {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  font-size: 13px;
}
@media (max-width: 599.98px) {
  .log-bar {
    padding: 4px 14px 12px;
  }
  .log-body {
    padding: 12px 14px 16px !important;
  }
  .entry-head {
    flex-wrap: wrap;
  }
  .entry-msg {
    flex-basis: 100%;
    order: 3;
  }
  .entry-toggle {
    margin-left: auto;
  }
}
</style>
