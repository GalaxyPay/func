<template>
  <div
    ref="root"
    class="sparkline"
    :style="{ height: `${height}px` }"
    @pointermove="onMove"
    @pointerleave="hover = undefined"
  >
    <svg
      :viewBox="`0 0 ${width} ${height}`"
      :width="width"
      :height="height"
      role="img"
      :aria-label="label"
    >
      <line class="baseline" x1="0" :x2="width" :y1="height - 0.5" :y2="height - 0.5" />
      <template v-if="bars">
        <path
          v-for="bar in rects"
          :key="bar.index"
          class="bar"
          :class="{ dimmed: hover != null && hover !== bar.index }"
          :d="barPath(bar)"
        />
      </template>
      <template v-else>
        <path v-for="(d, i) in line.paths" :key="i" class="line" :d="d" />
        <circle
          v-for="dot in line.dots"
          :key="dot.index"
          class="dot"
          :cx="dot.x"
          :cy="dot.y"
          r="2"
        />
        <template v-if="marker">
          <line
            class="crosshair"
            :x1="marker.x"
            :x2="marker.x"
            y1="0"
            :y2="height"
          />
          <circle class="marker" :cx="marker.x" :cy="marker.y" r="4" />
        </template>
      </template>
    </svg>
    <div v-if="hover != null" class="tip" :style="tipStyle">
      <span class="tip-value">{{ tipValue }}</span>
      <span class="tip-time">{{ tipTime }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  barRects,
  linePaths,
  lineY,
  slotAt,
  slotX,
  type Bar,
  type Series,
} from "@/utils/sparkline";

const props = withDefaults(
  defineProps<{
    // One value per interval, oldest first; null where nothing was measured.
    values: Series;
    // Start of each interval (Unix seconds) and the intervals' length.
    times: number[];
    bucketSeconds: number;
    // Bars for counts and amounts, a line for levels and averages.
    bars?: boolean;
    format: (value: number) => string;
    label: string;
    height?: number;
  }>(),
  { height: 56 }
);

const root = ref<HTMLElement>();
const width = ref(300);
const hover = ref<number>();

let observer: ResizeObserver | undefined;
onMounted(() => {
  if (!root.value || typeof ResizeObserver === "undefined") return;
  observer = new ResizeObserver(([entry]) => {
    if (entry.contentRect.width > 0) width.value = entry.contentRect.width;
  });
  observer.observe(root.value);
});
onBeforeUnmount(() => observer?.disconnect());

// The line is inset so its stroke and the hover marker aren't clipped.
const inset = 5;
const rects = computed(() => barRects(props.values, width.value, props.height));
const line = computed(() =>
  linePaths(props.values, width.value, props.height, inset)
);

// Rounded at the data end, square on the baseline.
function barPath(b: Bar) {
  const r = Math.min(2, b.width / 2, b.height);
  const right = b.x + b.width;
  const bottom = b.y + b.height;
  return `M${b.x},${bottom}V${b.y + r}Q${b.x},${b.y} ${b.x + r},${b.y}H${right - r}Q${right},${b.y} ${right},${b.y + r}V${bottom}Z`;
}

function onMove(e: PointerEvent) {
  const box = root.value?.getBoundingClientRect();
  if (!box) return;
  hover.value = slotAt(e.clientX - box.left, props.values.length, box.width);
}

const hoverValue = computed(() =>
  hover.value == null ? undefined : props.values[hover.value]
);

const marker = computed(() => {
  const v = hoverValue.value;
  if (hover.value == null || v == null) return undefined;
  return {
    x: slotX(hover.value, props.values.length, width.value),
    y: lineY(v, props.values, props.height, inset),
  };
});

const tipValue = computed(() => {
  const v = hoverValue.value;
  if (v == null) return "No data";
  return props.format(v);
});

const tipTime = computed(() => {
  if (hover.value == null) return "";
  const start = new Date(props.times[hover.value] * 1000);
  const time = start.toLocaleTimeString(undefined, {
    hour: "numeric",
    minute: "2-digit",
  });
  // Ranges within a day only need the time of day.
  if (props.bucketSeconds * props.values.length <= 86400) return time;
  const day = start.toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
  });
  return `${day}, ${time}`;
});

// Keep the tooltip inside the chart's width.
const tipStyle = computed(() => {
  if (hover.value == null) return {};
  const x = slotX(hover.value, props.values.length, width.value);
  const half = 70;
  const left = Math.min(Math.max(x, half), Math.max(width.value - half, half));
  return { left: `${left}px` };
});
</script>

<style scoped>
.sparkline {
  position: relative;
  width: 100%;
  touch-action: pan-y;
}
svg {
  display: block;
  overflow: visible;
}
.baseline {
  stroke: rgb(var(--v-theme-border-strong));
  stroke-width: 1;
}
.bar {
  fill: rgb(var(--v-theme-primary));
}
.bar.dimmed {
  fill: rgba(var(--v-theme-primary), 0.45);
}
.line {
  fill: none;
  stroke: rgb(var(--v-theme-primary));
  stroke-width: 2;
  stroke-linejoin: round;
  stroke-linecap: round;
}
.dot {
  fill: rgb(var(--v-theme-primary));
}
.crosshair {
  stroke: rgb(var(--v-theme-border-input));
  stroke-width: 1;
}
.marker {
  fill: rgb(var(--v-theme-primary));
  stroke: rgb(var(--v-theme-surface));
  stroke-width: 2;
}
.tip {
  position: absolute;
  bottom: calc(100% + 6px);
  transform: translateX(-50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  min-width: 96px;
  max-width: 140px;
  padding: 5px 9px;
  border: 1px solid rgb(var(--v-theme-border-strong));
  border-radius: 6px;
  background: rgb(var(--v-theme-surface-selected));
  white-space: nowrap;
  pointer-events: none;
  z-index: 2;
}
.tip-value {
  font: 500 13px/1.3 var(--font-mono);
  color: rgb(var(--v-theme-on-surface));
}
.tip-time {
  font-size: 11px;
  line-height: 1.3;
  color: rgb(var(--v-theme-text-muted));
}
</style>
