<template>
  <v-container v-if="totalBlocks > 0" fluid>
    <v-divider />
    <v-card-title class="d-flex align-center">
      Block Calendar
      <v-spacer />
      <span class="text-body-2 text-grey">
        {{ totalBlocks.toLocaleString() }} blocks · last 12 months
      </span>
    </v-card-title>
    <v-container fluid class="overflow-x-auto text-center">
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
    </v-container>
  </v-container>
</template>

<script setup lang="ts">
const props = defineProps<{
  name: string;
  timestamps: number[];
}>();

const ACCENT = { r: 26, g: 203, b: 247 };

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
  return labels;
});

const gridColumnsStyle = computed(() => ({
  gridTemplateColumns: `repeat(${weeks.value.length}, 12px)`,
}));

function levelColor(level: number) {
  if (level <= 0) return "rgba(127, 127, 127, 0.15)";
  const alpha = 0.25 + (level / 4) * 0.75;
  return `rgba(${ACCENT.r}, ${ACCENT.g}, ${ACCENT.b}, ${alpha})`;
}

function cellColor(count: number) {
  if (count <= 0) return levelColor(0);
  const max = maxCount.value || 1;
  const level = Math.max(1, Math.ceil((count / max) * 4));
  return levelColor(level);
}
</script>

<style scoped>
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
  color: rgb(var(--v-theme-on-surface));
  opacity: 0.6;
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
  color: rgb(var(--v-theme-on-surface));
  opacity: 0.6;
}
.legend-cell {
  display: inline-block;
}
</style>
