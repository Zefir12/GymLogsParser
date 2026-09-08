<script setup lang="ts">
import { computed, ref } from "vue";

import { useWorkoutStore } from "@/stores/workout";
import { exercises } from "@/data/exercises";

import StrengthSetTable from "./StrengthSetTable.vue";
import CardioEntryEditor from "./CardioEntryEditor.vue";

const props = defineProps<{ exercise: any; index: number }>();

const store = useWorkoutStore();

const exerciseSearch = ref("");
const exerciseMenuOpen = ref(false);

const filteredExercises = computed(() => {
  const query = exerciseSearch.value.trim().toLowerCase();

  if (!query) {
    return exercises;
  }

  return exercises.filter((exercise) => {
    return (
      exercise.name.toLowerCase().includes(query) ||
      exercise.category.toLowerCase().includes(query) ||
      exercise.muscleGroup?.toLowerCase().includes(query)
    );
  });
});

const selectedExercise = computed(() => {
  return exercises.find((item) => item.id === props.exercise.exerciseId);
});

function exerciseChanged(exerciseId: string) {
  const definition = exercises.find((item) => item.id === exerciseId);

  if (!definition) {
    return;
  }

  props.exercise.exerciseId = definition.id;
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

  exerciseSearch.value = "";
  exerciseMenuOpen.value = false;
}

function openExerciseMenu() {
  exerciseMenuOpen.value = true;
}

function closeExerciseMenu() {
  setTimeout(() => {
    exerciseMenuOpen.value = false;
  }, 100);
}

function selectExercise(exerciseId: string) {
  exerciseChanged(exerciseId);
}
</script>

<template>
  <article class="exercise-card">
    <div class="exercise-header">
      <div class="exercise-number">
        {{ String(index + 1).padStart(2, "0") }}
      </div>

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

      <div class="exercise-title">
        <div class="exercise-picker">
          <input
            v-model="exerciseSearch"
            class="exercise-select"
            type="text"
            :placeholder="selectedExercise?.name || 'Select exercise'"
            @focus="openExerciseMenu"
            @blur="closeExerciseMenu"
          />

          <div v-if="exerciseMenuOpen" class="exercise-dropdown">
            <button
              v-for="item in filteredExercises"
              :key="item.id"
              type="button"
              class="exercise-option"
              :class="{ selected: item.id === exercise.exerciseId }"
              @mousedown.prevent="selectExercise(item.id)"
            >
              <span class="exercise-option-name">
                {{ item.name }}
              </span>

              <span
                class="exercise-option-meta"
                :class="{ cardio: item.category === 'Cardio' }"
              >
                {{ item.category }}
              </span>
            </button>

            <div v-if="filteredExercises.length === 0" class="exercise-empty">
              No exercises found
            </div>
          </div>
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
  background: #64410ab5;
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
}

.exercise-picker {
  position: relative;
  width: 100%;
  max-width: 360px;
}

.exercise-select {
  width: 100%;
  height: 28px;
  padding: 0 8px;
  color-scheme: dark;
  border: 1px solid transparent;
  border-radius: var(--radius-xs);
  outline: 0;
  background: transparent;
  color: var(--text-primary);
  font-size: 11px;
  font-weight: 800;
  box-sizing: border-box;
}

.exercise-select:focus {
  border-color: var(--glass-border);
  background: rgba(0, 0, 0, 0.12);
}

.exercise-select::placeholder {
  color: var(--text-primary);
  opacity: 1;
}

.exercise-dropdown {
  position: absolute;
  z-index: 50;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  max-height: 260px;
  overflow-y: auto;
  padding: 4px;
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);
  background: #16181c;
  box-shadow: 0 12px 30px rgba(0, 0, 0, 0.35);
}

.exercise-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  min-height: 32px;
  padding: 6px 8px;
  gap: 8px;
  border: 0;
  border-radius: var(--radius-xs);
  background: transparent;
  color: #f2f3f5;
  text-align: left;
  cursor: pointer;
}

.exercise-option:hover,
.exercise-option.selected {
  background: rgba(255, 255, 255, 0.08);
}

.exercise-option-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 10px;
  font-weight: 700;
}

.exercise-option-meta {
  flex: 0 0 auto;
  color: var(--accent-text);
  font-size: 7px;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.exercise-option-meta.cardio {
  color: var(--success);
}

.exercise-empty {
  padding: 12px 8px;
  color: var(--text-tertiary);
  font-size: 9px;
  text-align: center;
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
  width: 50px;
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
