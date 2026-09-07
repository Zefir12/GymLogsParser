<script setup lang="ts">
import { computed } from "vue";

import { useWorkoutStore } from "@/stores/workout";
import { exercises } from "@/data/exercises";

import StrengthSetTable from "./StrengthSetTable.vue";
import CardioEntryEditor from "./CardioEntryEditor.vue";

const props = defineProps<{ exercise: any; index: number }>();

const store = useWorkoutStore();

const strengthExercises = computed(() =>
  exercises.filter((exercise) => exercise.category === "Strength"),
);

const cardioExercises = computed(() =>
  exercises.filter((exercise) => exercise.category === "Cardio"),
);

function exerciseChanged() {
  const definition = exercises.find(
    (item) => item.id === props.exercise.exerciseId,
  );

  if (!definition) {
    return;
  }

  props.exercise.name = definition.name;
  props.exercise.muscleGroup = definition.muscleGroup;
  props.exercise.category = definition.category;

  /*
   * Changing an exercise from strength to
   * cardio should never leave old strength
   * sets attached to it, and vice versa.
   */
  if (definition.category === "Cardio") {
    props.exercise.sets = [];
  }

  if (definition.category === "Strength") {
    props.exercise.cardio = [];
  }
}
</script>

<template>
  <article class="exercise-card">
    <div class="exercise-header">
      <div class="exercise-number">
        {{ String(index + 1).padStart(2, "0") }}
      </div>

      <div class="exercise-title">
        <select
          v-model="exercise.exerciseId"
          class="exercise-select"
          @change="exerciseChanged"
        >
          <optgroup label="Strength">
            <option
              v-for="item in strengthExercises"
              :key="item.id"
              :value="item.id"
            >
              {{ item.name }}
            </option>
          </optgroup>

          <optgroup label="Cardio">
            <option
              v-for="item in cardioExercises"
              :key="item.id"
              :value="item.id"
            >
              {{ item.name }}
            </option>
          </optgroup>
        </select>

        <div class="exercise-subline">
          <span
            class="category-badge"
            :class="{ cardio: exercise.category === 'Cardio' }"
          >
            {{ exercise.category }}
          </span>

          <input
            v-model="exercise.muscleGroup"
            class="muscle-input"
            type="text"
            placeholder="Muscle group"
          />
        </div>
      </div>

      <button
        class="icon-button danger"
        type="button"
        title="Remove exercise"
        @click="store.removeExercise(index)"
      >
        ×
      </button>
    </div>

    <StrengthSetTable
      v-if="exercise.category === 'Strength'"
      :exercise="exercise"
    />

    <CardioEntryEditor v-else :exercise="exercise" />
  </article>
</template>

<style scoped>
.exercise-card {
  overflow: hidden;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-md);

  background: var(--glass-soft);
}

.exercise-header {
  display: flex;
  align-items: center;
  gap: 10px;

  padding: 10px 11px;

  border-bottom: 1px solid var(--glass-border);
}

.exercise-number {
  display: grid;
  place-items: center;

  width: 26px;
  height: 26px;

  flex: 0 0 auto;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  color: var(--text-tertiary);

  font-size: 8px;
  font-family: "JetBrains Mono", monospace;
}

.exercise-title {
  flex: 1;
  min-width: 0;

  display: flex;
  flex-direction: column;
  gap: 5px;
}

.exercise-select {
  width: 100%;
  max-width: 360px;
  height: 28px;

  border: 0;
  outline: 0;

  background: transparent;
  color: var(--text-primary);

  font-size: 11px;
  font-weight: 800;
}

.exercise-subline {
  display: flex;
  align-items: center;
  gap: 7px;
}

.category-badge {
  display: inline-flex;
  align-items: center;

  height: 19px;
  padding: 0 7px;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  color: var(--text-tertiary);

  font-size: 7px;
  font-weight: 900;

  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.category-badge:not(.cardio) {
  border-color: var(--accent-border);
  background: var(--accent-soft);
  color: var(--accent-text);
}

.category-badge.cardio {
  border-color: var(--success-border);
  background: var(--success-bg);
  color: var(--success);
}

.muscle-input {
  width: 110px;

  border: 0;
  outline: 0;

  background: transparent;
  color: var(--text-tertiary);

  font-size: 8px;
}

.icon-button {
  display: grid;
  place-items: center;

  width: 27px;
  height: 27px;

  flex: 0 0 auto;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  background: transparent;

  color: var(--text-tertiary);

  cursor: pointer;

  transition:
    border-color 150ms var(--ease),
    color 150ms var(--ease);
}

.icon-button.danger:hover {
  border-color: var(--danger-border);
  color: var(--danger);
}
</style>
