<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";

import { getExerciseProgress, getExercises } from "@/api/client";
import type { ExerciseProgressPoint, ExerciseSummary } from "@/types/exercise";

import ExerciseProgressChart from "@/components/ExerciseProgressChart.vue";
import SearchableSelect from "@/components/SearchableSelect.vue";
import type { SearchableSelectOption } from "@/components/SearchableSelect.vue";

const exercises = ref<ExerciseSummary[]>([]);
const selectedExerciseId = ref<string>("");

const progress = ref<ExerciseProgressPoint[]>([]);

const loadingExercises = ref(false);
const loadingProgress = ref(false);
const error = ref<string | null>(null);

const selectedExercise = computed(() =>
  exercises.value.find((e) => e.exerciseId === selectedExerciseId.value),
);

const exerciseOptions = computed<SearchableSelectOption[]>(() =>
  exercises.value.map((e) => ({
    value: e.exerciseId,
    label: e.name,
    sublabel: e.muscleGroup ?? undefined,
  })),
);

const maxWeightSeries = computed(() =>
  progress.value.map((p) => ({ date: p.date, value: p.maxWeight })),
);

const volumeSeries = computed(() =>
  progress.value.map((p) => ({ date: p.date, value: p.totalVolume })),
);

const oneRepMaxSeries = computed(() =>
  progress.value.map((p) => ({ date: p.date, value: p.estimatedOneRepMax })),
);

/** Sessions with no date have no bodyweight, so they can't get a DOTS score. */
const dotsSeries = computed(() =>
  progress.value
    .filter((p) => p.dots !== null && p.dots > 0)
    .map((p) => ({ date: p.date, value: p.dots as number })),
);

const bodyweightSeries = computed(() =>
  progress.value
    .filter((p) => p.bodyweight !== null)
    .map((p) => ({ date: p.date, value: p.bodyweight as number })),
);

const bestDots = computed(() => {
  const values = dotsSeries.value.map((p) => p.value);
  return values.length > 0 ? Math.max(...values) : null;
});

const sessionCount = computed(() => progress.value.length);

const bestOneRepMax = computed(() => {
  if (progress.value.length === 0) return null;
  return Math.max(...progress.value.map((p) => p.estimatedOneRepMax));
});

async function loadExercises() {
  loadingExercises.value = true;
  error.value = null;

  try {
    exercises.value = await getExercises();

    if (exercises.value.length > 0) {
      selectedExerciseId.value = exercises.value[0].exerciseId;
    }
  } catch (err) {
    error.value =
      err instanceof Error ? err.message : "Could not load exercises.";
  } finally {
    loadingExercises.value = false;
  }
}

async function loadProgress(exerciseId: string) {
  if (!exerciseId) {
    progress.value = [];
    return;
  }

  loadingProgress.value = true;
  error.value = null;

  try {
    progress.value = await getExerciseProgress(exerciseId);
  } catch (err) {
    progress.value = [];
    error.value =
      err instanceof Error ? err.message : "Could not load progress.";
  } finally {
    loadingProgress.value = false;
  }
}

watch(selectedExerciseId, (id) => loadProgress(id));

onMounted(loadExercises);
</script>

<template>
  <div class="progress-view">
    <header class="progress-header">
      <div>
        <h1>Progress</h1>
        <p class="subtitle">
          Track strength trends across your logged workouts.
        </p>
      </div>

      <SearchableSelect
        v-if="exercises.length > 0"
        v-model="selectedExerciseId"
        :options="exerciseOptions"
        placeholder="Search exercises…"
      />
    </header>

    <p v-if="error" class="error-text">{{ error }}</p>

    <p v-else-if="loadingExercises" class="status-text">Loading exercises…</p>

    <p v-else-if="exercises.length === 0" class="status-text">
      No strength exercises logged yet. Log a workout first, then come back
      here.
    </p>

    <template v-else>
      <div class="summary-row" v-if="selectedExercise">
        <div class="summary-item">
          <span class="summary-label">Sessions logged</span>
          <span class="summary-value">{{ sessionCount }}</span>
        </div>
        <div class="summary-item" v-if="bestOneRepMax !== null">
          <span class="summary-label">Best estimated 1RM</span>
          <span class="summary-value">
            {{ bestOneRepMax.toFixed(1) }}<span class="unit">kg</span>
          </span>
        </div>
        <div class="summary-item" v-if="bestDots !== null">
          <span class="summary-label">Best DOTS</span>
          <span class="summary-value">{{ bestDots.toFixed(1) }}</span>
        </div>
        <div class="summary-item" v-if="selectedExercise.muscleGroup">
          <span class="summary-label">Muscle group</span>
          <span class="summary-value">{{ selectedExercise.muscleGroup }}</span>
        </div>
      </div>

      <p v-if="loadingProgress" class="status-text">Loading progress…</p>

      <div v-else class="charts-grid">
        <ExerciseProgressChart
          title="Max weight per session"
          unit="kg"
          :points="maxWeightSeries"
        />
        <ExerciseProgressChart
          title="Total volume (weight × reps)"
          unit="kg"
          color="var(--success)"
          :points="volumeSeries"
        />
        <ExerciseProgressChart
          title="Estimated one-rep max"
          unit="kg"
          color="var(--accent-strong)"
          :points="oneRepMaxSeries"
        />
        <ExerciseProgressChart
          title="DOTS (estimated 1RM vs bodyweight)"
          unit=""
          color="var(--accent-text)"
          :points="dotsSeries"
        />
        <ExerciseProgressChart
          title="Bodyweight"
          unit="kg"
          color="var(--text-secondary)"
          :points="bodyweightSeries"
        />
      </div>
    </template>
  </div>
</template>

<style scoped>
.progress-view {
  max-width: 900px;
  margin: 0 auto;
}

.progress-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  flex-wrap: wrap;
  gap: 14px;

  margin-bottom: 22px;
}

.progress-header h1 {
  margin: 0 0 4px;

  font-size: 1.5rem;
  color: var(--text-primary);
}

.subtitle {
  margin: 0;
  font-size: 0.9rem;
  color: var(--text-tertiary);
}

.summary-row {
  display: flex;
  gap: 14px;
  flex-wrap: wrap;

  margin-bottom: 20px;
}

.summary-item {
  display: flex;
  flex-direction: column;
  gap: 4px;

  padding: 12px 18px;

  background: var(--glass-soft);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);
}

.summary-label {
  font-size: 0.75rem;
  color: var(--text-tertiary);
}

.summary-value {
  font-size: 1.15rem;
  color: var(--text-primary);
  font-variant-numeric: tabular-nums;
}

.summary-value .unit {
  margin-left: 3px;
  font-size: 0.7rem;
  color: var(--text-tertiary);
}

.charts-grid {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.status-text {
  color: var(--text-tertiary);
  font-size: 0.9rem;
}

.error-text {
  color: var(--danger);
  font-size: 0.9rem;
}
</style>
