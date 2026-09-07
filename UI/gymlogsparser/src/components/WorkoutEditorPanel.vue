<script setup lang="ts">
import { storeToRefs } from "pinia";

import { useWorkoutStore } from "@/stores/workout";

import WorkoutStats from "./WorkoutStats.vue";
import NoticeBanner from "./NoticeBanner.vue";
import AiUsageMeta from "./AiUsageMeta.vue";
import EmptyState from "./EmptyState.vue";
import WorkoutMetaForm from "./WorkoutMetaForm.vue";
import ExerciseCard from "./ExerciseCard.vue";

const store = useWorkoutStore();

const {
  workout,
  saving,
  error,
  success,
  usage,
  exerciseCount,
  setCount,
  cardioCount,
  strengthCount,
} = storeToRefs(store);
</script>

<template>
  <section class="panel editor-panel">
    <div class="panel-header editor-header">
      <div>
        <div class="eyebrow">02 / REVIEW</div>
        <h2>Workout structure</h2>
        <p>Everything here is editable before saving.</p>
      </div>

      <WorkoutStats
        :exercise-count="exerciseCount"
        :strength-count="strengthCount"
        :cardio-count="cardioCount"
        :set-count="setCount"
      />
    </div>

    <NoticeBanner v-if="error" variant="error" :message="error" />
    <NoticeBanner v-if="success" variant="success" :message="success" />
    <AiUsageMeta v-if="usage" :usage="usage" />

    <EmptyState v-if="!workout.exercises.length" />

    <template v-else>
      <WorkoutMetaForm :workout="workout as any" />

      <div class="exercise-list">
        <ExerciseCard
          v-for="(exercise, exerciseIndex) in workout.exercises"
          :key="exerciseIndex"
          :exercise="exercise"
          :index="exerciseIndex"
        />
      </div>

      <button class="add-exercise" type="button" @click="store.addExercise">
        <span>+</span>
        Add exercise
      </button>

      <div class="save-bar">
        <div>
          <strong>Ready to save?</strong>
          <span>Review the parsed values before committing.</span>
        </div>

        <button
          class="button button-primary"
          type="button"
          :disabled="saving"
          @click="store.save"
        >
          <span v-if="saving" class="spinner" />
          <span v-else>Save workout</span>
        </button>
      </div>
    </template>
  </section>
</template>

<style scoped>
.panel {
  min-width: 0;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);

  background: var(--glass);
  backdrop-filter: var(--blur-lg);
  -webkit-backdrop-filter: var(--blur-lg);

  box-shadow: var(--shadow-glass);

  overflow: hidden;
}

.editor-panel {
  min-height: 650px;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  gap: 20px;

  padding: 20px 20px 16px;

  border-bottom: 1px solid var(--glass-border);
}

.editor-header {
  align-items: center;
}

.eyebrow {
  margin-bottom: 6px;

  color: var(--text-quiet);

  font-size: 8px;
  font-weight: 900;
  letter-spacing: 0.12em;
}

.panel-header h2 {
  margin: 0;

  color: var(--text-primary);

  font-size: 16px;
  line-height: 1.2;
  letter-spacing: -0.025em;
}

.panel-header p {
  margin: 6px 0 0;

  color: var(--text-tertiary);

  font-size: 10px;
  line-height: 1.5;
}

.exercise-list {
  display: flex;
  flex-direction: column;
  gap: 10px;

  padding: 16px;
}

.add-exercise {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 7px;

  width: calc(100% - 32px);
  height: 39px;

  margin: 0 16px 16px;

  border: 1px dashed var(--glass-border-strong);
  border-radius: var(--radius-sm);

  background: transparent;

  color: var(--text-tertiary);

  font-size: 9px;
  font-weight: 800;

  cursor: pointer;

  transition:
    border-color 150ms var(--ease),
    color 150ms var(--ease);
}

.add-exercise:hover {
  border-color: var(--accent-border);
  color: var(--accent-text);
}

.add-exercise span {
  font-size: 14px;
  font-weight: 400;
}

.save-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 15px;

  padding: 14px 16px;

  border-top: 1px solid var(--glass-border);

  background: var(--glass-soft);
}

.save-bar > div {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.save-bar strong {
  color: var(--text-secondary);

  font-size: 9px;
}

.save-bar span {
  color: var(--text-quiet);

  font-size: 8px;
}

.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;

  min-height: 33px;
  padding: 0 14px;

  border-radius: var(--radius-xs);

  font-size: 10px;
  font-weight: 800;

  cursor: pointer;

  transition:
    opacity 150ms var(--ease),
    transform 150ms var(--ease);
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.4;
}

.button-primary {
  border: 1px solid var(--accent-border);

  background: linear-gradient(160deg, var(--accent), #d85a1a);
  color: #1a0a00;

  box-shadow: 0 6px 18px rgba(255, 122, 48, 0.28);
}

.button-primary:hover:not(:disabled) {
  transform: translateY(-1px);
}

.spinner {
  width: 11px;
  height: 11px;

  border: 2px solid rgba(26, 10, 0, 0.55);
  border-top-color: transparent;

  border-radius: 50%;

  animation: spin 700ms linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 700px) {
  .editor-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .save-bar {
    align-items: stretch;
    flex-direction: column;
  }

  .save-bar .button {
    width: 100%;
  }
}
</style>
