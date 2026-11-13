<template>
  <div class="health">
    <div class="section-head">
      <span class="section-label">Consensus</span>
      <v-spacer />
      <v-btn-toggle
        :model-value="range"
        @update:model-value="(value: string) => emit('update:range', value)"
        mandatory
        divided
        density="compact"
        variant="outlined"
        class="range"
      >
        <v-btn v-for="r in ranges" :key="r.value" :value="r.value" size="x-small">
          {{ r.title }}
        </v-btn>
      </v-btn-toggle>
    </div>
    <div class="tiles">
      <v-card class="stat-card" v-for="tile in tiles" :key="tile.label">
        <div class="stat-label">
          {{ tile.label }}
          <span class="ml-auto tile-sub">{{ tile.sub }}</span>
        </div>
        <div class="stat-value">{{ tile.value }}</div>
        <Sparkline
          :values="tile.values"
          :times="times"
          :bucket-seconds="health.bucketSeconds"
          :bars="true"
          :height="44"
          :format="tile.format"
          :label="`${tile.label} over the ${period}`"
        />
      </v-card>
    </div>
  </div>
</template>

<script lang="ts" setup>
import type { NodeHealth } from "@/types";
import { total } from "@/utils/health";
import type { Series } from "@/utils/sparkline";
import type { PropType } from "vue";

const props = defineProps({
  health: { type: Object as PropType<NodeHealth>, required: true },
  range: { type: String, required: true },
});
const emit = defineEmits<{ "update:range": [value: string] }>();

const ranges = [
  { value: "1h", title: "Hour", period: "last hour" },
  { value: "24h", title: "Day", period: "last 24 hours" },
  { value: "7d", title: "Week", period: "last 7 days" },
  { value: "30d", title: "Month", period: "last 30 days" },
];

const period = computed(
  () => ranges.find((r) => r.value === props.range)?.period ?? ""
);

const times = computed(() => props.health.points.map((p) => p.t));

const count = (noun: string) => (n: number) =>
  `${n.toLocaleString()} ${noun}${n === 1 ? "" : "s"}`;

interface Tile {
  label: string;
  value: string;
  sub: string;
  values: Series;
  format: (value: number) => string;
}

const tiles = computed<Tile[]>(() => {
  const p = props.health.points;
  const proposals = total(p, "proposals");
  return [
    {
      label: "Votes Sent",
      value: total(p, "votes").toLocaleString(),
      sub: `in ${count("block")(total(p, "rounds"))}`,
      values: p.map((x) => x.votes),
      format: count("vote"),
    },
    {
      label: "Blocks Proposed",
      value: proposals.toLocaleString(),
      sub: proposals ? `${total(p, "wins").toLocaleString()} won` : "none",
      values: p.map((x) => x.proposals),
      format: count("proposal"),
    },
  ];
});
</script>

<style scoped>
.health {
  margin-top: 16px;
}
.range {
  height: 24px;
}
.range .v-btn {
  min-width: 0;
  padding: 0 10px;
  font-size: 11.5px;
  letter-spacing: normal;
}
.tiles {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
.tiles .stat-card {
  overflow: visible;
  gap: 4px;
}
/* On a narrow card the note drops below the label. */
.tiles .stat-label {
  flex-wrap: wrap;
  row-gap: 0;
}
.tile-sub {
  font-size: 12px;
  color: rgb(var(--v-theme-text-dim));
  white-space: nowrap;
}
@media (max-width: 599.98px) {
  .tiles .stat-card {
    padding: 12px;
  }
  .tiles .stat-value {
    font-size: 20px;
  }
}
</style>
