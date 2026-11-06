<script setup lang="ts">
import { computed } from "vue";

import SegmentedControl from "@/components/dashboard/SegmentedControl.vue";
import { useDashboardStore } from "@/stores/dashboard";
import type { DashboardMetric } from "@/types/dashboard";
import {
  CARDIO,
  formatDay,
  formatKg,
  formatNumber,
  monthName,
  nextMonthStart,
  signed,
} from "@/utils/dashboard";

const store = useDashboardStore();

const metrics: { value: DashboardMetric; label: string }[] = [
  { value: "e1rm", label: "Est. 1RM" },
  { value: "top", label: "Top set" },
  { value: "volume", label: "Volume" },
];

const metricLabel: Record<DashboardMetric, string> = {
  e1rm: "estimated 1RM",
  top: "top set",
  volume: "session volume",
};

/** SVG user space; the svg stretches to its box (preserveAspectRatio="none"). */
const SIZE = 1000;

const title = computed(
  () =>
    `${store.chartExercise?.name ?? store.muscle} · ${metricLabel[store.metric]}`,
);

const chart = computed(() => {
  const points = store.chartPoints;
  if (points.length < 2) return null;

  const t0 = store.rangeStart;
  const t1 = store.toDay;
  const values = points.map((p) => p.value);
  const vmin = Math.min(...values);
  const vmax = Math.max(...values);
  const span = vmax - vmin || vmax * 0.1 || 1;
  const lo = vmin - span * 0.2;
  const hi = vmax + span * 0.2;

  const x = (day: number) => ((day - t0) / Math.max(1, t1 - t0)) * SIZE;
  const y = (v: number) => (1 - (v - lo) / (hi - lo)) * SIZE;

  const line = points
    .map((p, i) => `${i ? "L" : "M"}${x(p.day).toFixed(1)} ${y(p.value).toFixed(1)}`)
    .join(" ");
  const area = `${line} L${x(points[points.length - 1].day).toFixed(1)} ${SIZE} L${x(points[0].day).toFixed(1)} ${SIZE} Z`;

  const dots = points.map((p) => ({
    key: p.day,
    left: `${(x(p.day) / 10).toFixed(2)}%`,
    top: `${(y(p.value) / 10).toFixed(2)}%`,
    pr: p.pr,
    tip: `${formatDay(p.day)} · ${formatKg(p.value)}${p.pr ? " · PR" : ""}`,
  }));

  const yTicks = [0.125, 0.375, 0.625, 0.875].map((f) => ({
    top: `${f * 100}%`,
    label: formatNumber(hi - (hi - lo) * f),
  }));

  const xTicks: { left: string; label: string }[] = [];
  for (
    let day = nextMonthStart(t0), i = 0;
    day <= t1;
    day = nextMonthStart(day), i++
  ) {
    if (store.range === "1Y" && i % 2 === 1) continue;
    xTicks.push({ left: `${(x(day) / 10).toFixed(2)}%`, label: monthName(day) });
  }

  const first = values[0];
  const last = values[values.length - 1];
  const delta = last - first;

  return {
    line,
    area,
    dots,
    yTicks,
    xTicks,
    last: formatKg(last),
    delta: `${delta >= 0 ? "+" : "−"}${formatKg(Math.abs(delta))} (${signed((delta / first) * 100)}%)`,
    up: delta >= 0,
  };
});

const emptyText = computed(() => {
  if (!store.chartExercise) {
    return store.muscle === CARDIO
      ? "Cardio has no strength chart. Pick a body part with lifts."
      : "No exercise to chart yet.";
  }
  return "Not enough sessions in this range.";
});
</script>

<template>
  <section aria-label="Progress chart" class="card">
    <div class="head">
      <div>
        <div class="title">{{ title }}</div>
        <div class="figures">
          <span class="last">{{ chart?.last ?? "–" }}</span>
          <span v-if="chart" :class="chart.up ? 'good' : 'warn'">
            {{ chart.delta }}
          </span>
        </div>
      </div>
      <SegmentedControl
        v-model="store.metric"
        :options="metrics"
        label="Metric"
        compact
        class="metric"
      />
    </div>

    <div v-if="chart" class="plot-wrap">
      <div class="plot">
        <template v-for="tick in chart.yTicks" :key="tick.top">
          <div class="gridline" :style="{ top: tick.top }" />
          <div class="y-label" :style="{ top: tick.top }">{{ tick.label }}</div>
        </template>

        <svg
          :viewBox="`0 0 ${SIZE} ${SIZE}`"
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          <path :d="chart.area" class="area" />
          <path :d="chart.line" class="line" />
        </svg>

        <div
          v-for="dot in chart.dots"
          :key="dot.key"
          class="dot"
          :class="{ pr: dot.pr }"
          :style="{ left: dot.left, top: dot.top }"
          :title="dot.tip"
        />
      </div>
      <div class="x-axis">
        <span
          v-for="tick in chart.xTicks"
          :key="tick.left"
          class="x-label"
          :style="{ left: tick.left }"
        >
          {{ tick.label }}
        </span>
      </div>
      <div class="key">
        <span class="key-dot pr" /> PR session
      </div>
    </div>

    <div v-else class="empty">{{ emptyText }}</div>
  </section>
</template>

<style scoped>
.card {
  display: flex;
  flex-direction: column;
  gap: 14px;
  min-width: 0;
  padding: 20px;

  border: 1px solid var(--card-border);
  border-radius: 14px;
  background: var(--card-bg);
}

.head {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 12px;
}

.title {
  color: var(--text-secondary);
  font-size: 13px;
}

.figures {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 4px 12px;
  margin-top: 4px;
  font-weight: 500;
}

.last {
  color: var(--text-primary);
  font-family: var(--font-display);
  font-size: 28px;
  font-weight: 600;
}

.good {
  color: var(--success);
}

.warn {
  color: var(--warning);
}

.metric {
  align-self: flex-start;
  background: var(--header-bg);
}

.plot-wrap {
  padding-left: 52px;
}

.plot {
  position: relative;
  height: 240px;
}

.gridline {
  position: absolute;
  left: 0;
  right: 0;
  border-top: 1px dashed rgba(255, 255, 255, 0.08);
}

.y-label {
  position: absolute;
  left: -52px;
  width: 44px;
  transform: translateY(-50%);
  color: var(--text-secondary);
  font-size: 11px;
  text-align: right;
  font-variant-numeric: tabular-nums;
}

svg {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: visible;
}

.area {
  fill: var(--accent);
  fill-opacity: 0.12;
}

.line {
  fill: none;
  stroke: var(--accent);
  stroke-width: 2.5;
  stroke-linejoin: round;
  vector-effect: non-scaling-stroke;
}

.dot {
  position: absolute;
  width: 6px;
  height: 6px;
  margin: -3px 0 0 -3px;
  border-radius: 50%;
  background: var(--accent);
}

.dot.pr,
.key-dot.pr {
  width: 12px;
  height: 12px;
  margin: -6px 0 0 -6px;
  border: 2px solid var(--accent-text);
  background: var(--bg-base);
}

.x-axis {
  position: relative;
  height: 20px;
  margin-top: 8px;
}

.x-label {
  position: absolute;
  transform: translateX(-50%);
  color: var(--text-secondary);
  font-size: 11px;
}

.key {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 6px;
  color: var(--text-secondary);
  font-size: 11px;
}

.key-dot {
  display: inline-block;
  border-radius: 50%;
}

.key-dot.pr {
  margin: 0;
}

.empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 240px;
  padding: 16px;

  border: 1px dashed rgba(255, 255, 255, 0.12);
  border-radius: 10px;
  color: var(--text-secondary);
  text-align: center;
}
</style>
