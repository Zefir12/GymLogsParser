<script setup lang="ts">
import { computed, onMounted } from "vue";
import { RouterLink } from "vue-router";

import BodyPartTiles from "@/components/dashboard/BodyPartTiles.vue";
import ExerciseTable from "@/components/dashboard/ExerciseTable.vue";
import ProgressChart from "@/components/dashboard/ProgressChart.vue";
import SegmentedControl from "@/components/dashboard/SegmentedControl.vue";
import TrainingHeatmap from "@/components/dashboard/TrainingHeatmap.vue";
import WeeklySetsBars from "@/components/dashboard/WeeklySetsBars.vue";
import { useDashboardStore } from "@/stores/dashboard";
import { useMuscleTargetsStore } from "@/stores/muscleTargets";
import type { DashboardRange } from "@/types/dashboard";
import { RANGE_LABEL } from "@/utils/dashboard";

const store = useDashboardStore();
const targets = useMuscleTargetsStore();

const ranges: { value: DashboardRange; label: string }[] = [
  { value: "3M", label: "3M" },
  { value: "6M", label: "6M" },
  { value: "1Y", label: "1Y" },
  { value: "5Y", label: "5Y" },
  { value: "10Y", label: "10Y" },
];

const summaryLine = computed(() => {
  const s = store.summary;
  return [
    `${s.sessions} session${s.sessions === 1 ? "" : "s"}`,
    `${s.streak}-week streak`,
    `${s.tonnes.toFixed(1)} t`,
    `${s.prs} PR${s.prs === 1 ? "" : "s"}`,
    RANGE_LABEL[store.range],
  ].join(" · ");
});

// Refetch on every visit so a workout logged on /log shows up straight away.
// Targets load once, in parallel; until then tiles and bars show no status.
onMounted(() => {
  store.load();
  targets.ensureLoaded();
});
</script>

<template>
  <div class="dashboard">
    <div class="page-head">
      <div>
        <h1>How each body part is doing</h1>
        <p v-if="store.data" class="summary">{{ summaryLine }}</p>
      </div>
      <SegmentedControl v-model="store.range" :options="ranges" label="Date range" />
    </div>

    <p v-if="store.error" class="status error">{{ store.error }}</p>
    <p v-else-if="!store.data" class="status">Loading your training…</p>

    <div v-else-if="store.data.days.length === 0" class="empty">
      <p>No workouts with a date in the {{ RANGE_LABEL[store.range] }} yet.</p>
      <RouterLink to="/log" class="cta">+ Log workout</RouterLink>
    </div>

    <template v-else>
      <BodyPartTiles />
      <TrainingHeatmap />
      <WeeklySetsBars />
      <div class="split">
        <ExerciseTable class="table" />
        <ProgressChart class="chart" />
      </div>
    </template>
  </div>
</template>

<style scoped>
.dashboard {
  max-width: 1360px;
  margin: 0 auto;

  display: flex;
  flex-direction: column;
  gap: var(--card-gap-lg);

  color: var(--text-primary);
  font-family: var(--font-body);
  font-size: 14px;
}

.page-head {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
}

h1 {
  margin: 0;
  font-family: var(--font-display);
  font-size: 32px;
  font-weight: 600;
  letter-spacing: -0.02em;
}

.summary {
  margin: 6px 0 0;
  color: var(--text-secondary);
}

.status {
  margin: 0;
  color: var(--text-secondary);
}

.status.error {
  color: var(--danger);
}

.empty {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 12px;
  padding: var(--card-pad);

  border: 1px dashed rgba(255, 255, 255, 0.12);
  border-radius: var(--radius-card);
  color: var(--text-secondary);
}

.empty p {
  margin: 0;
}

.cta {
  display: inline-flex;
  align-items: center;
  min-height: 44px;
  padding: 0 18px;

  border-radius: var(--radius-control);
  background: var(--accent);
  color: var(--on-accent);
  font-weight: 600;
  text-decoration: none;
}

.split {
  display: flex;
  flex-wrap: wrap;
  align-items: stretch;
  gap: var(--card-gap-lg);
}

.table {
  flex: 1 1 420px;
}

.chart {
  flex: 999 1 560px;
}

@media (max-width: 700px) {
  h1 {
    font-size: 26px;
  }
}
</style>
