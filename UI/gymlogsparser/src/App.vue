<script setup lang="ts">
import { computed, nextTick, ref } from "vue";
import { storeToRefs } from "pinia";
import { useWorkoutStore } from "@/stores/workout";

const store = useWorkoutStore();

const {
  rawText,
  workout,
  loading,
  saving,
  error,
  success,
  usage,
  exerciseCount,
  setCount,
} = storeToRefs(store);

const textarea = ref<HTMLTextAreaElement | null>(null);

const canParse = computed(
  () => rawText.value.trim().length > 0 && !loading.value,
);

async function parseWorkout() {
  await store.parse();

  await nextTick();

  if (workout.value.exercises.length > 0) {
    document.querySelector(".editor-panel")?.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
  }
}

function formatCacheInfo() {
  if (!usage.value) {
    return "";
  }

  if (usage.value.cacheHit) {
    return `${usage.value.cacheHitTokens.toLocaleString()} cached tokens`;
  }

  return "No cached prefix";
}
</script>

<template>
  <main class="app-shell">
    <header class="topbar">
      <div>
        <div class="brand">
          <span class="brand-mark">G</span>
          <span>GymLog</span>
        </div>

        <p class="subtitle">
          Turn messy workout notes into structured training data.
        </p>
      </div>

      <button
        class="button button-secondary"
        type="button"
        @click="store.newWorkout"
      >
        New workout
      </button>
    </header>

    <section class="workspace">
      <section class="panel input-panel">
        <div class="panel-header">
          <div>
            <div class="eyebrow">01 / LOG</div>
            <h1>Paste your workout</h1>
            <p>
              Write it however you normally write it. The parser handles the
              structure.
            </p>
          </div>

          <span class="shortcut">AI</span>
        </div>

        <textarea
          ref="textarea"
          v-model="rawText"
          class="workout-input"
          spellcheck="false"
          placeholder="Bench press
warmup 40kg x 12
60kg x 10
70kg x 8
70kg x 8

Lat pulldown 55kg
10, 10, 8
last set RIR 1

DB curls 12.5kg
3 x 10"
        />

        <div class="input-footer">
          <span> {{ rawText.length.toLocaleString() }} characters </span>

          <button
            class="button button-primary"
            type="button"
            :disabled="!canParse"
            @click="parseWorkout"
          >
            <span v-if="loading" class="spinner" />
            <span v-else>Parse workout</span>
          </button>
        </div>
      </section>

      <section class="panel editor-panel">
        <div class="panel-header editor-header">
          <div>
            <div class="eyebrow">02 / REVIEW</div>
            <h2>Workout structure</h2>
            <p>Everything here is editable before saving.</p>
          </div>

          <div class="stats">
            <div class="stat">
              <strong>{{ exerciseCount }}</strong>
              <span>exercises</span>
            </div>

            <div class="stat">
              <strong>{{ setCount }}</strong>
              <span>sets</span>
            </div>
          </div>
        </div>

        <div v-if="error" class="notice notice-error">
          {{ error }}
        </div>

        <div v-if="success" class="notice notice-success">
          {{ success }}
        </div>

        <div v-if="usage" class="ai-meta">
          <span class="ai-dot" />
          DeepSeek
          <span class="separator">·</span>
          {{ formatCacheInfo() }}
          <span class="separator">·</span>
          {{ usage.inputTokens }} in
          <span class="separator">·</span>
          {{ usage.outputTokens }} out
        </div>

        <div v-if="!workout.exercises.length" class="empty-state">
          <div class="empty-icon">✦</div>

          <h3>No workout parsed yet</h3>

          <p>
            Paste your gym log on the left and let the parser build the editable
            structure here.
          </p>
        </div>

        <template v-else>
          <div class="workout-meta">
            <label>
              <span>Title</span>
              <input
                v-model="workout.title"
                type="text"
                placeholder="Push day"
              />
            </label>

            <label>
              <span>Date</span>
              <input v-model="workout.date" type="date" />
            </label>

            <label class="full-width">
              <span>Notes</span>
              <textarea
                v-model="workout.notes"
                rows="2"
                placeholder="Workout notes..."
              />
            </label>
          </div>

          <div class="exercise-list">
            <article
              v-for="(exercise, exerciseIndex) in workout.exercises"
              :key="exerciseIndex"
              class="exercise-card"
            >
              <div class="exercise-header">
                <div class="exercise-number">
                  {{ String(exerciseIndex + 1).padStart(2, "0") }}
                </div>

                <div class="exercise-title">
                  <input
                    v-model="exercise.name"
                    class="exercise-name"
                    type="text"
                    placeholder="Exercise name"
                  />

                  <input
                    v-model="exercise.muscleGroup"
                    class="muscle-input"
                    type="text"
                    placeholder="Muscle group"
                  />
                </div>

                <button
                  class="icon-button danger"
                  type="button"
                  title="Remove exercise"
                  @click="store.removeExercise(exerciseIndex)"
                >
                  ×
                </button>
              </div>

              <div class="set-table">
                <div class="set-row set-head">
                  <span>SET</span>
                  <span>WEIGHT</span>
                  <span>UNIT</span>
                  <span>REPS</span>
                  <span>RIR</span>
                  <span>RPE</span>
                  <span>TYPE</span>
                  <span />
                </div>

                <div
                  v-for="(set, setIndex) in exercise.sets"
                  :key="setIndex"
                  class="set-row"
                >
                  <input
                    v-model.number="set.setNumber"
                    type="number"
                    min="1"
                    class="set-number"
                  />

                  <input
                    v-model.number="set.weight"
                    type="number"
                    min="0"
                    step="0.25"
                    placeholder="—"
                  />

                  <select v-model="set.weightUnit">
                    <option :value="null">—</option>
                    <option value="kg">kg</option>
                    <option value="lb">lb</option>
                  </select>

                  <input
                    v-model.number="set.reps"
                    type="number"
                    min="0"
                    placeholder="—"
                  />

                  <input
                    v-model.number="set.rir"
                    type="number"
                    min="0"
                    step="0.5"
                    placeholder="—"
                  />

                  <input
                    v-model.number="set.rpe"
                    type="number"
                    min="1"
                    max="10"
                    step="0.5"
                    placeholder="—"
                  />

                  <select v-model="set.warmup">
                    <option :value="null">Normal</option>
                    <option :value="true">Warm-up</option>
                    <option :value="false">Working</option>
                  </select>

                  <button
                    class="remove-set"
                    type="button"
                    @click="store.removeSet(exercise, setIndex)"
                  >
                    ×
                  </button>
                </div>
              </div>

              <div class="exercise-footer">
                <button
                  class="text-button"
                  type="button"
                  @click="store.addSet(exercise)"
                >
                  + Add set
                </button>

                <input
                  v-model="exercise.notes"
                  class="exercise-notes"
                  type="text"
                  placeholder="Exercise notes..."
                />
              </div>
            </article>
          </div>

          <button class="add-exercise" type="button" @click="store.addExercise">
            <span>+</span>
            Add exercise
          </button>

          <div class="save-bar">
            <div>
              <strong>Ready to save?</strong>
              <span> Review the parsed values before committing. </span>
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
    </section>
  </main>
</template>
