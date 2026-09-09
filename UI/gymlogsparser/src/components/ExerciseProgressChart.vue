<script setup lang="ts">
import { computed } from "vue";

interface ChartPoint {
  date: string | null;
  value: number;
}

const props = withDefaults(
  defineProps<{
    title: string;
    unit: string;
    points: ChartPoint[];
    color?: string;
  }>(),
  {
    color: "var(--accent)",
  },
);

const width = 640;
const height = 200;
const padX = 28;
const padY = 20;
const tickCount = 4;

const usablePoints = computed(() =>
  props.points
    .filter((p): p is { date: string; value: number } => p.date !== null)
    .map((p) => ({ ...p, time: new Date(p.date).getTime() }))
    .sort((a, b) => a.time - b.time),
);

const hasEnoughData = computed(() => usablePoints.value.length >= 2);

const valueBounds = computed(() => {
  const values = usablePoints.value.map((p) => p.value);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || max || 1;
  return {
    min: Math.max(0, min - span * 0.1),
    max: max + span * 0.1,
  };
});

const timeBounds = computed(() => {
  const times = usablePoints.value.map((p) => p.time);
  return { min: Math.min(...times), max: Math.max(...times) };
});

/** Maps a timestamp to an x position, scaled by real elapsed time so old
 *  and recent sessions aren't spaced as if they happened at even intervals. */
function xForTime(time: number) {
  const { min, max } = timeBounds.value;
  const range = max - min;
  if (range === 0) return width / 2;
  return padX + ((time - min) / range) * (width - padX * 2);
}

function yForValue(value: number) {
  const { min, max } = valueBounds.value;
  const range = max - min || 1;
  return height - padY - ((value - min) / range) * (height - padY * 2);
}

const coords = computed(() =>
  usablePoints.value.map((p) => ({
    x: xForTime(p.time),
    y: yForValue(p.value),
    date: p.date,
    value: p.value,
  })),
);

const linePath = computed(() =>
  coords.value.map((c, i) => `${i === 0 ? "M" : "L"} ${c.x} ${c.y}`).join(" "),
);

const areaPath = computed(() => {
  if (coords.value.length === 0) return "";
  const first = coords.value[0];
  const last = coords.value[coords.value.length - 1];
  return `${linePath.value} L ${last.x} ${height - padY} L ${first.x} ${height - padY} Z`;
});

const latest = computed(() =>
  usablePoints.value.length > 0
    ? usablePoints.value[usablePoints.value.length - 1]
    : null,
);

/** Evenly spaced date ticks across the real time range, so a big gap between
 *  old and recent sessions is visible and legible rather than hidden. */
const ticks = computed(() => {
  if (!hasEnoughData.value) return [];

  const { min, max } = timeBounds.value;
  if (max === min) {
    return [{ x: width / 2, label: formatDate(new Date(min).toISOString()) }];
  }

  return Array.from({ length: tickCount }, (_, i) => {
    const t = min + (i / (tickCount - 1)) * (max - min);
    return {
      x: padX + (i / (tickCount - 1)) * (width - padX * 2),
      label: formatDate(new Date(t).toISOString()),
    };
  });
});

function formatDate(date: string | null) {
  if (!date) return "";
  return new Date(date).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

function formatValue(value: number) {
  return Number.isInteger(value) ? value.toString() : value.toFixed(1);
}
</script>

<template>
  <div class="chart-card">
    <div class="chart-header">
      <span class="chart-title">{{ title }}</span>
      <span v-if="latest" class="chart-latest">
        {{ formatValue(latest.value) }}<span class="unit">{{ unit }}</span>
      </span>
    </div>

    <div v-if="!hasEnoughData" class="empty-state">
      Not enough logged sessions yet to chart this.
    </div>

    <template v-else>
      <svg
        class="chart-svg"
        :viewBox="`0 0 ${width} ${height}`"
        preserveAspectRatio="none"
      >
        <line
          v-for="(tick, i) in ticks"
          :key="i"
          :x1="tick.x"
          :x2="tick.x"
          :y1="padY"
          :y2="height - padY"
          class="chart-gridline"
        />

        <path :d="areaPath" class="chart-area" :style="{ fill: color }" />
        <path :d="linePath" class="chart-line" :style="{ stroke: color }" />

        <circle
          v-for="(c, i) in coords"
          :key="i"
          :cx="c.x"
          :cy="c.y"
          :r="i === coords.length - 1 ? 4 : 2.5"
          class="chart-point"
          :style="{ fill: color }"
        >
          <title>
            {{ formatDate(c.date) }}: {{ formatValue(c.value) }}{{ unit }}
          </title>
        </circle>
      </svg>

      <div class="chart-ticks">
        <span
          v-for="(tick, i) in ticks"
          :key="i"
          class="tick-label"
          :style="{ left: `${(tick.x / width) * 100}%` }"
        >
          {{ tick.label }}
        </span>
      </div>
    </template>
  </div>
</template>

<style scoped>
.chart-card {
  padding: 18px 20px;

  background: var(--glass-soft);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xl);
}

.chart-header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;

  margin-bottom: 10px;
}

.chart-title {
  font-size: 0.9rem;
  color: var(--text-secondary);
}

.chart-latest {
  font-size: 1.4rem;
  color: var(--text-primary);
  font-variant-numeric: tabular-nums;
}

.chart-latest .unit {
  margin-left: 3px;
  font-size: 0.8rem;
  color: var(--text-tertiary);
}

.chart-svg {
  display: block;
  width: 100%;
  height: 180px;
}

.chart-gridline {
  stroke: var(--glass-border);
  stroke-width: 1;
  stroke-dasharray: 3 4;
}

.chart-line {
  fill: none;
  stroke-width: 2;
  stroke-linejoin: round;
  stroke-linecap: round;
}

.chart-area {
  opacity: 0.08;
}

.chart-point {
  transition: r 0.15s var(--ease);
}

.chart-ticks {
  position: relative;
  height: 16px;
  margin-top: 4px;
}

.tick-label {
  position: absolute;
  transform: translateX(-50%);

  font-size: 0.7rem;
  color: var(--text-quiet);
  white-space: nowrap;
}

.tick-label:first-child {
  transform: translateX(0);
}

.tick-label:last-child {
  transform: translateX(-100%);
}

.empty-state {
  padding: 40px 0;
  text-align: center;

  font-size: 0.85rem;
  color: var(--text-tertiary);
}
</style>
