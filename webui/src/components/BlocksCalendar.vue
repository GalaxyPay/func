<template>
  <section v-if="totalBlocks > 0" class="node-section">
    <div class="section-head">
      <span class="section-label">Block Calendar</span>
      <v-spacer />
      <span class="text-caption text-dim">
        <span class="font-mono">{{ totalBlocks.toLocaleString() }}</span>
        blocks · last 12 months
      </span>
    </div>
    <div class="group calendar-wrap">
      <div class="calendar">
        <div class="month-labels" :style="gridColumnsStyle">
          <div
            v-for="label in monthLabels"
            :key="label.key"
            class="month-label"
            :style="{ gridColumnStart: label.column }"
          >
            {{ label.text }}
          </div>
        </div>
        <div class="heatmap" :style="gridColumnsStyle">
          <template v-for="(week, wi) in weeks" :key="wi">
            <div
              v-for="day in week"
              :key="day.key"
              class="cell"
              :style="{
                gridColumn: wi + 1,
                gridRow: day.weekday + 1,
                backgroundColor: day.placeholder
                  ? 'transparent'
                  : cellColor(day.count),
              }"
            >
              <v-tooltip
                v-if="!day.placeholder"
                activator="parent"
                location="top"
                :text="`${day.count} block${day.count === 1 ? '' : 's'} on ${day.label}`"
              />
            </div>
          </template>
        </div>
        <div class="legend">
          Less
          <span
            v-for="level in 5"
            :key="level"
            class="cell legend-cell"
            :style="{ backgroundColor: levelColor(level - 1) }"
          />
          More
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
const props = defineProps<{
  name: string;
  timestamps: number[];
}>();

function dayKey(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(
    d.getDate()
  ).padStart(2, "0")}`;
}

// Per-day block counts keyed by local YYYY-MM-DD.
const dayCounts = computed(() => {
  const counts = new Map<string, number>();
  for (const ts of props.timestamps) {
    const key = dayKey(new Date(ts * 1000));
    counts.set(key, (counts.get(key) || 0) + 1);
  }
  return counts;
});

const totalBlocks = computed(() => {
  let total = 0;
  for (const week of weeks.value)
    for (const day of week) if (!day.placeholder) total += day.count;
  return total;
});

const maxCount = computed(() => {
  let max = 0;
  for (const week of weeks.value)
    for (const day of week)
      if (!day.placeholder && day.count > max) max = day.count;
  return max;
});

type Day = {
  key: string;
  label: string;
  weekday: number;
  count: number;
  placeholder: boolean;
};

// Build a grid of week columns covering roughly the last 12 months,
// aligned so each column starts on Sunday (GitHub-style). Iteration uses
// calendar-day increments (setDate) so it stays correct across DST changes.
const weeks = computed<Day[][]>(() => {
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  const start = new Date(today);
  start.setDate(start.getDate() - 364);
  // Back up to the start of the week (Sunday).
  start.setDate(start.getDate() - start.getDay());

  const result: Day[][] = [];
  const current = new Date(start);
  while (current <= today || current.getDay() !== 0) {
    if (current.getDay() === 0) result.push([]);
    const isPlaceholder = current > today;
    const key = dayKey(current);
    result[result.length - 1].push({
      key,
      label: current.toLocaleDateString(undefined, {
        year: "numeric",
        month: "short",
        day: "numeric",
      }),
      weekday: current.getDay(),
      count: isPlaceholder ? 0 : dayCounts.value.get(key) || 0,
      placeholder: isPlaceholder,
    });
    current.setDate(current.getDate() + 1);
  }
  return result;
});

const monthLabels = computed(() => {
  const labels: { key: string; text: string; column: number }[] = [];
  let lastMonth = -1;
  weeks.value.forEach((week, index) => {
    const firstReal = week.find((d) => !d.placeholder);
    if (!firstReal) return;
    const [y, m, d] = firstReal.key.split("-").map(Number);
    const date = new Date(y, m - 1, d);
    const month = date.getMonth();
    if (month !== lastMonth) {
      lastMonth = month;
      labels.push({
        key: firstReal.key,
        text: date.toLocaleDateString(undefined, { month: "short" }),
        column: index + 1,
      });
    }
  });
  // A label needs ~3 week columns of room. The window usually opens partway
  // through a month, so drop that leading partial month rather than let it
  // collide with the next one.
  if (labels.length > 1 && labels[1].column - labels[0].column < 3) {
    labels.shift();
  }
  return labels;
});

const gridColumnsStyle = computed(() => ({
  gridTemplateColumns: `repeat(${weeks.value.length}, 12px)`,
}));

function levelColor(level: number) {
  if (level <= 0) return "rgb(var(--v-theme-surface-selected))";
  const alpha = 0.25 + (level / 4) * 0.75;
  return `rgba(var(--v-theme-primary), ${alpha})`;
}

function cellColor(count: number) {
  if (count <= 0) return levelColor(0);
  const max = maxCount.value || 1;
  const level = Math.max(1, Math.ceil((count / max) * 4));
  return levelColor(level);
}
</script>

<style scoped>
.node-section {
  margin-top: 32px;
}
.calendar-wrap {
  overflow-x: auto;
  padding: 18px 16px 14px;
  text-align: center;
}
.calendar {
  display: inline-block;
}
.month-labels {
  display: grid;
  grid-auto-flow: column;
  column-gap: 3px;
  height: 16px;
  margin-bottom: 2px;
}
.month-label {
  font-size: 10px;
  color: rgb(var(--v-theme-text-dim));
  white-space: nowrap;
}
.heatmap {
  display: grid;
  grid-template-rows: repeat(7, 12px);
  grid-auto-flow: column;
  gap: 3px;
}
.cell {
  width: 12px;
  height: 12px;
  border-radius: 2px;
}
.legend {
  display: flex;
  align-items: center;
  gap: 3px;
  margin-top: 8px;
  font-size: 10px;
  color: rgb(var(--v-theme-text-dim));
}
.legend-cell {
  display: inline-block;
}
</style>
