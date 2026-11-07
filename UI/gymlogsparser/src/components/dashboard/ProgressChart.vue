<script setup lang="ts">
import { computed, ref, watch } from "vue";

import SegmentedControl from "@/components/dashboard/SegmentedControl.vue";
import SessionModal from "@/components/dashboard/SessionModal.vue";
import { useDashboardStore } from "@/stores/dashboard";
import type { DashboardMetric } from "@/types/dashboard";
import {
  CARDIO,
  dayDate,
  formatDay,
  formatKg,
  formatNumber,
  monthName,
  nextMonthStart,
  nextYearStart,
  signed,
  YEAR_TICK_RANGES,
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

/** Half-width in days of the centred window of the min–max band behind the line. */
const BAND_HALF_DAYS = 10;

/** 3-point moving average, so the band's edges are soft instead of stepped. */
function smooth(values: number[]) {
  return values.map((_, i) => {
    const near = values.slice(Math.max(0, i - 1), i + 2);
    return near.reduce((s, n) => s + n, 0) / near.length;
  });
}

/** Least-squares line through the points: value = a + b * day. */
function linearFit(points: { day: number; value: number }[]) {
  const n = points.length;
  const mx = points.reduce((s, p) => s + p.day, 0) / n;
  const my = points.reduce((s, p) => s + p.value, 0) / n;
  let sxy = 0;
  let sxx = 0;
  for (const p of points) {
    sxy += (p.day - mx) * (p.value - my);
    sxx += (p.day - mx) ** 2;
  }
  const b = sxx ? sxy / sxx : 0;
  return (day: number) => my + b * (day - mx);
}

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

  // Band: lowest to highest value within ±10 days of each session, smoothed.
  const near = points.map((p) =>
    points
      .filter((q) => Math.abs(q.day - p.day) <= BAND_HALF_DAYS)
      .map((q) => q.value),
  );
  const top = smooth(near.map((v) => Math.max(...v)));
  const bottom = smooth(near.map((v) => Math.min(...v)));
  const pt = (day: number, v: number) => `${x(day).toFixed(1)} ${y(v).toFixed(1)}`;
  const band =
    points.map((p, i) => `${i ? "L" : "M"}${pt(p.day, top[i])}`).join(" ") +
    " " +
    points
      .map((p, i) => `L${pt(p.day, bottom[i])}`)
      .reverse()
      .join(" ") +
    " Z";

  // Trend: straight least-squares line from the first to the last session.
  const fit = linearFit(points);
  const firstDay = points[0].day;
  const lastDay = points[points.length - 1].day;
  const trend = `M${pt(firstDay, fit(firstDay))} L${pt(lastDay, fit(lastDay))}`;
  const perMonth = (fit(lastDay) - fit(firstDay)) / Math.max(1, lastDay - firstDay) * 30;

  const dots = points.map((p) => ({
    key: p.day,
    /** Position as a fraction of the plot box, for hit testing. */
    fx: x(p.day) / SIZE,
    fy: y(p.value) / SIZE,
    left: `${(x(p.day) / 10).toFixed(2)}%`,
    top: `${(y(p.value) / 10).toFixed(2)}%`,
    pr: p.pr,
    tip: `${formatDay(p.day)} · ${formatKg(p.value)}${p.pr ? " · PR" : ""}`,
    workoutIds: p.workoutIds,
  }));

  const yTicks = [0.125, 0.375, 0.625, 0.875].map((f) => ({
    top: `${f * 100}%`,
    label: formatNumber(hi - (hi - lo) * f),
  }));

  const xTicks: { left: string; label: string }[] = [];
  if (YEAR_TICK_RANGES.includes(store.range)) {
    for (let day = nextYearStart(t0); day <= t1; day = nextYearStart(day)) {
      xTicks.push({
        left: `${(x(day) / 10).toFixed(2)}%`,
        label: String(dayDate(day).getUTCFullYear()),
      });
    }
  } else {
    for (
      let day = nextMonthStart(t0), i = 0;
      day <= t1;
      day = nextMonthStart(day), i++
    ) {
      if (store.range === "1Y" && i % 2 === 1) continue;
      xTicks.push({ left: `${(x(day) / 10).toFixed(2)}%`, label: monthName(day) });
    }
  }

  const first = values[0];
  const last = values[values.length - 1];
  const delta = last - first;

  return {
    line,
    band,
    trend,
    trendLabel: `Trend ${perMonth >= 0 ? "+" : "−"}${formatKg(Math.abs(perMonth))}/month`,
    dots,
    yTicks,
    xTicks,
    last: formatKg(last),
    delta: `${delta >= 0 ? "+" : "−"}${formatKg(Math.abs(delta))} (${signed((delta / first) * 100)}%)`,
    up: delta >= 0,
  };
});

// --- picking a session: hover/tap/keyboard, then open it in a modal ---

const plotEl = ref<HTMLElement | null>(null);
/** Index into chart.dots of the highlighted session, if any. */
const active = ref<number | null>(null);

const activeDot = computed(() =>
  chart.value && active.value !== null
    ? (chart.value.dots[active.value] ?? null)
    : null,
);

// Dots are re-laid out on every filter/metric change or reload.
watch(chart, () => (active.value = null));

const coarsePointer =
  typeof window !== "undefined" &&
  !!window.matchMedia?.("(pointer: coarse)").matches;

/**
 * The dot nearest to the pointer. Dots are tiny and can sit close together, so
 * instead of exact hits anything within a horizontal band around a dot counts,
 * wider for fingers; among those the closest one wins.
 */
function nearestDot(e: MouseEvent): number | null {
  const dots = chart.value?.dots;
  const box = plotEl.value?.getBoundingClientRect();
  if (!dots || !box) return null;

  // Some browsers send click as a plain MouseEvent, or with an empty pointerType.
  const pointerType = "pointerType" in e ? (e as PointerEvent).pointerType : "";
  const touch = pointerType === "touch" || (!pointerType && coarsePointer);
  const band = touch ? 32 : 20;
  const px = e.clientX - box.left;
  const py = e.clientY - box.top;

  let best: number | null = null;
  let bestDistance = Infinity;
  for (let i = 0; i < dots.length; i++) {
    const dot = dots[i]!;
    const dx = dot.fx * box.width - px;
    if (Math.abs(dx) > band) continue;
    const dy = dot.fy * box.height - py;
    const distance = dx * dx + dy * dy;
    if (distance < bestDistance) {
      bestDistance = distance;
      best = i;
    }
  }
  return best;
}

function onPointerMove(e: PointerEvent) {
  if (e.pointerType === "mouse") active.value = nearestDot(e);
}

function onPointerLeave(e: PointerEvent) {
  if (e.pointerType === "mouse" && document.activeElement !== plotEl.value) {
    active.value = null;
  }
}

function onPlotClick(e: MouseEvent) {
  const index = nearestDot(e);
  if (index !== null) openDot(index);
}

function onPlotFocus() {
  // Only keyboard focus highlights a dot; a mouse press focuses the plot too.
  if (active.value === null && plotEl.value?.matches(":focus-visible")) {
    active.value = (chart.value?.dots.length ?? 0) - 1;
  }
}

function onPlotKeydown(e: KeyboardEvent) {
  const count = chart.value?.dots.length ?? 0;
  if (count === 0) return;
  const current = active.value ?? count - 1;

  switch (e.key) {
    case "ArrowLeft":
    case "ArrowDown":
      active.value = Math.max(0, current - 1);
      break;
    case "ArrowRight":
    case "ArrowUp":
      active.value = Math.min(count - 1, current + 1);
      break;
    case "Home":
      active.value = 0;
      break;
    case "End":
      active.value = count - 1;
      break;
    case "Enter":
    case " ":
      openDot(current);
      break;
    default:
      return;
  }
  e.preventDefault();
}

interface OpenSession {
  exerciseId: string;
  exerciseName: string;
  workoutIds: string[];
}

const session = ref<OpenSession | null>(null);

function openDot(index: number) {
  const dot = chart.value?.dots[index];
  const exercise = store.chartExercise;
  if (!dot || !exercise || dot.workoutIds.length === 0) return;
  active.value = index;
  session.value = {
    exerciseId: exercise.exerciseId,
    exerciseName: exercise.name,
    workoutIds: dot.workoutIds,
  };
}

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
      <div
        ref="plotEl"
        class="plot"
        :class="{ pickable: activeDot }"
        tabindex="0"
        role="group"
        :aria-label="`${title}. Use the arrow keys to pick a session and Enter to open it.`"
        @pointermove="onPointerMove"
        @pointerleave="onPointerLeave"
        @click="onPlotClick"
        @focus="onPlotFocus"
        @blur="active = null"
        @keydown="onPlotKeydown"
      >
        <template v-for="tick in chart.yTicks" :key="tick.top">
          <div class="gridline" :style="{ top: tick.top }" />
          <div class="y-label" :style="{ top: tick.top }">{{ tick.label }}</div>
        </template>

        <svg
          :viewBox="`0 0 ${SIZE} ${SIZE}`"
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          <path :d="chart.band" class="band" />
          <path :d="chart.trend" class="trend" />
          <path :d="chart.line" class="line" />
        </svg>

        <div
          v-for="(dot, i) in chart.dots"
          :key="dot.key"
          class="dot"
          :class="{ pr: dot.pr, active: i === active }"
          :style="{ left: dot.left, top: dot.top }"
        />

        <div
          v-if="activeDot"
          class="tip"
          :class="{
            'tip--start': activeDot.fx < 0.15,
            'tip--end': activeDot.fx > 0.85,
            'tip--below': activeDot.fy < 0.2,
          }"
          :style="{ left: activeDot.left, top: activeDot.top }"
          aria-live="polite"
        >
          {{ activeDot.tip }}
          <span class="tip__hint">{{
            activeDot.workoutIds.length ? "Click to view or edit" : ""
          }}</span>
        </div>
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
        <span class="hint">Tap a point to open that session</span>
        <span class="key-band" /> Range (±10 days)
        <span class="key-trend" /> {{ chart.trendLabel }}
        <span class="key-dot pr" /> PR session
      </div>
    </div>

    <div v-else class="empty">{{ emptyText }}</div>

    <SessionModal
      v-if="session"
      :exercise-id="session.exerciseId"
      :exercise-name="session.exerciseName"
      :workout-ids="session.workoutIds"
      @close="session = null"
      @saved="store.load()"
    />
  </section>
</template>

<style scoped>
.card {
  display: flex;
  flex-direction: column;
  gap: 14px;
  min-width: 0;
  padding: var(--card-pad);

  border: 1px solid var(--card-border);
  border-radius: var(--radius-card);
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
  touch-action: manipulation; /* no double-tap zoom delay on the tap */
  outline: none;
  -webkit-tap-highlight-color: transparent;
}

.plot.pickable {
  cursor: pointer;
}

.plot:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 6px;
  border-radius: var(--radius-control);
}

.gridline {
  position: absolute;
  left: 0;
  right: 0;
  border-top: 1px dashed var(--card-border);
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

.band {
  fill: var(--accent);
  fill-opacity: 0.08;
}

.trend {
  fill: none;
  stroke: var(--text-secondary);
  stroke-width: 1.5;
  stroke-dasharray: 6 5;
  vector-effect: non-scaling-stroke;
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

.dot {
  pointer-events: none;
  transition: transform 120ms var(--ease);
}

.dot.active {
  transform: scale(1.8);
  box-shadow: 0 0 0 3px rgba(var(--accent-rgb), 0.25);
}

.dot.pr.active {
  transform: scale(1.3);
}

.tip {
  position: absolute;
  z-index: 1;
  transform: translate(-50%, calc(-100% - 12px));
  padding: 5px 8px;

  border: 1px solid var(--border-strong);
  border-radius: var(--radius-control);
  background: var(--surface-solid);
  box-shadow: var(--shadow-glass);

  color: var(--text-primary);
  font-size: 12px;
  white-space: nowrap;
  pointer-events: none;
}

.tip--start {
  transform: translate(-8px, calc(-100% - 12px));
}

.tip--end {
  transform: translate(calc(-100% + 8px), calc(-100% - 12px));
}

.tip--below {
  translate: 0 calc(100% + 24px);
}

.tip__hint {
  display: block;
  color: var(--text-secondary);
  font-size: 11px;
}

.tip__hint:empty {
  display: none;
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
  flex-wrap: wrap;
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

.key-band {
  display: inline-block;
  width: 14px;
  height: 10px;
  margin-left: 8px;
  border-radius: 2px;
  background: rgba(var(--accent-rgb), 0.12);
}

.key-trend {
  display: inline-block;
  width: 16px;
  margin-left: 8px;
  border-top: 1.5px dashed var(--text-secondary);
}

.key-dot.pr {
  margin: 0;
}

.hint {
  margin-right: auto;
}

@media (pointer: fine) {
  .hint {
    display: none;
  }
}

.empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 240px;
  padding: 16px;

  border: 1px dashed var(--empty-border);
  border-radius: var(--radius-control);
  color: var(--text-secondary);
  text-align: center;
}
</style>
