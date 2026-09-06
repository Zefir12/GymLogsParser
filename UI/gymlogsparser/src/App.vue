```vue
<script setup lang="ts">
import { computed, nextTick, ref } from "vue";
import { storeToRefs } from "pinia";

import { useWorkoutStore } from "@/stores/workout";
import { exercises } from "@/data/exercises";

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
  cardioCount,
  strengthCount,
  barbellWeightsArePerSide,
} = storeToRefs(store);

const textarea = ref<HTMLTextAreaElement | null>(null);

const canParse = computed(
  () => rawText.value.trim().length > 0 && !loading.value,
);

const strengthExercises = computed(() =>
  exercises.filter((exercise) => exercise.category === "Strength"),
);

const cardioExercises = computed(() =>
  exercises.filter((exercise) => exercise.category === "Cardio"),
);

function exerciseChanged(exerciseIndex: number) {
  const exercise = workout.value.exercises[exerciseIndex];

  const definition = exercises.find((item) => item.id === exercise.exerciseId);

  if (!definition) {
    return;
  }

  exercise.name = definition.name;

  exercise.muscleGroup = definition.muscleGroup;

  exercise.category = definition.category;

  /*
   * Changing an exercise from strength to
   * cardio should never leave old strength
   * sets attached to it.
   */
  if (definition.category === "Cardio") {
    exercise.sets = [];
  }

  /*
   * And vice versa.
   */
  if (definition.category === "Strength") {
    exercise.cardio = [];
  }
}

function addCardioEntry(exerciseIndex: number) {
  const exercise = workout.value.exercises[exerciseIndex];

  exercise.cardio.push({
    activity: exercise.name,
    durationSeconds: null,
    distanceKm: null,
    speedKmh: null,
    inclinePercent: null,
    notes: null,
  });
}

function removeCardioEntry(exerciseIndex: number, cardioIndex: number) {
  workout.value.exercises[exerciseIndex].cardio.splice(cardioIndex, 1);
}

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

function formatDuration(seconds: number | null) {
  if (seconds === null || seconds === undefined) {
    return "";
  }

  const minutes = Math.floor(seconds / 60);

  const remainingSeconds = seconds % 60;

  if (remainingSeconds === 0) {
    return `${minutes} min`;
  }

  return `${minutes}m ${remainingSeconds}s`;
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
      <!-- =====================================================
           LEFT: RAW LOG
           ===================================================== -->

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

          <span class="shortcut"> AI </span>
        </div>

        <textarea
          ref="textarea"
          v-model="rawText"
          class="workout-input"
          spellcheck="false"
          placeholder="24.06.2022
plecy bola kregosłup
start 17:16

incline 5% 1km 5:36
lawka 0x6, 10kg x6, 20kgx10, 22.5kgx6
deadlift 15kgx4(warmup), 25kgx4, 25kgx3
siady 0x6, 5kgx6, 10kgx8

legpress 2setsx14repsx15kg
hamstring 2setsx15repsx23kg

koniec 18:46"
        />

        <!-- BARBELL INTERPRETATION -->

        <div class="parser-options">
          <label class="checkbox-option">
            <input v-model="barbellWeightsArePerSide" type="checkbox" />

            <span class="checkbox-ui" />

            <span class="checkbox-copy">
              <strong> Barbell weights are per side </strong>

              <small>
                40 kg = 40 kg each side + 20 kg bar → 100 kg total
              </small>
            </span>
          </label>
        </div>

        <div class="input-footer">
          <span>
            {{ rawText.length.toLocaleString() }}
            characters
          </span>

          <button
            class="button button-primary"
            type="button"
            :disabled="!canParse"
            @click="parseWorkout"
          >
            <span v-if="loading" class="spinner" />

            <span v-else> Parse workout </span>
          </button>
        </div>
      </section>

      <!-- =====================================================
           RIGHT: REVIEW
           ===================================================== -->

      <section class="panel editor-panel">
        <div class="panel-header editor-header">
          <div>
            <div class="eyebrow">02 / REVIEW</div>

            <h2>Workout structure</h2>

            <p>Everything here is editable before saving.</p>
          </div>

          <div class="stats">
            <div class="stat">
              <strong>
                {{ exerciseCount }}
              </strong>

              <span> exercises </span>
            </div>

            <div class="stat">
              <strong>
                {{ strengthCount }}
              </strong>

              <span> strength </span>
            </div>

            <div class="stat">
              <strong>
                {{ cardioCount }}
              </strong>

              <span> cardio </span>
            </div>

            <div class="stat">
              <strong>
                {{ setCount }}
              </strong>

              <span> sets </span>
            </div>
          </div>
        </div>

        <!-- NOTICES -->

        <div v-if="error" class="notice notice-error">
          {{ error }}
        </div>

        <div v-if="success" class="notice notice-success">
          {{ success }}
        </div>

        <!-- AI USAGE -->

        <div v-if="usage" class="ai-meta">
          <span class="ai-dot" />

          DeepSeek

          <span class="separator"> · </span>

          {{ formatCacheInfo() }}

          <span class="separator"> · </span>

          {{ usage.inputTokens }} in

          <span class="separator"> · </span>

          {{ usage.outputTokens }} out
        </div>

        <!-- EMPTY -->

        <div v-if="!workout.exercises.length" class="empty-state">
          <div class="empty-icon">✦</div>

          <h3>No workout parsed yet</h3>

          <p>
            Paste your gym log on the left and let the parser build the editable
            structure here.
          </p>
        </div>

        <!-- WORKOUT -->

        <template v-else>
          <!-- WORKOUT META -->

          <div class="workout-meta">
            <label>
              <span> Title </span>

              <input
                v-model="workout.title"
                type="text"
                placeholder="Push day"
              />
            </label>

            <label>
              <span> Date </span>

              <input v-model="workout.date" type="date" />
            </label>

            <label class="full-width">
              <span> Notes </span>

              <textarea
                v-model="workout.notes"
                rows="2"
                placeholder="Workout notes..."
              />
            </label>
          </div>

          <!-- EXERCISES -->

          <div class="exercise-list">
            <article
              v-for="(exercise, exerciseIndex) in workout.exercises"
              :key="exerciseIndex"
              class="exercise-card"
            >
              <!-- EXERCISE HEADER -->

              <div class="exercise-header">
                <div class="exercise-number">
                  {{ String(exerciseIndex + 1).padStart(2, "0") }}
                </div>

                <div class="exercise-title">
                  <!-- CANONICAL EXERCISE -->

                  <select
                    v-model="exercise.exerciseId"
                    class="exercise-select"
                    @change="exerciseChanged(exerciseIndex)"
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
                      :class="{
                        cardio: exercise.category === 'Cardio',
                      }"
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
                  @click="store.removeExercise(exerciseIndex)"
                >
                  ×
                </button>
              </div>

              <!-- =================================================
                   STRENGTH
                   ================================================= -->

              <template v-if="exercise.category === 'Strength'">
                <div class="set-table">
                  <div class="set-row set-head">
                    <span> SET </span>

                    <span> WEIGHT </span>

                    <span> REPS </span>

                    <span> RIR </span>

                    <span> RPE </span>

                    <span> TYPE </span>

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

                    <div class="weight-cell">
                      <input
                        v-model.number="set.weight"
                        type="number"
                        min="0"
                        step="0.25"
                        placeholder="—"
                      />

                      <span
                        v-if="set.weightEntryMode === 'PerSide'"
                        class="weight-mode"
                        title="Original value was interpreted as weight per side"
                      >
                        /side
                      </span>

                      <span v-else class="weight-mode"> kg </span>
                    </div>

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

                <!-- SET NOTES -->

                <div
                  v-for="(set, setIndex) in exercise.sets"
                  :key="`notes-${setIndex}`"
                  class="set-notes-row"
                >
                  <span>
                    Set
                    {{ set.setNumber ?? setIndex + 1 }}
                    notes
                  </span>

                  <input
                    v-model="set.notes"
                    type="text"
                    placeholder="Set notes..."
                  />
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
              </template>

              <!-- =================================================
                   CARDIO
                   ================================================= -->

              <template v-else>
                <div class="cardio-editor">
                  <div
                    v-for="(cardio, cardioIndex) in exercise.cardio"
                    :key="cardioIndex"
                    class="cardio-entry"
                  >
                    <div class="cardio-grid">
                      <label>
                        <span> Duration </span>

                        <input
                          :value="
                            cardio.durationSeconds !== null
                              ? Math.round(cardio.durationSeconds / 60)
                              : ''
                          "
                          type="number"
                          min="0"
                          step="1"
                          placeholder="min"
                          @input="
                            cardio.durationSeconds =
                              Number(
                                ($event.target as HTMLInputElement).value,
                              ) * 60
                          "
                        />

                        <small v-if="cardio.durationSeconds !== null">
                          {{ formatDuration(cardio.durationSeconds) }}
                        </small>
                      </label>

                      <label>
                        <span> Distance </span>

                        <div class="input-with-unit">
                          <input
                            v-model.number="cardio.distanceKm"
                            type="number"
                            min="0"
                            step="0.01"
                            placeholder="—"
                          />

                          <em> km </em>
                        </div>
                      </label>

                      <label>
                        <span> Speed </span>

                        <div class="input-with-unit">
                          <input
                            v-model.number="cardio.speedKmh"
                            type="number"
                            min="0"
                            step="0.1"
                            placeholder="—"
                          />

                          <em> km/h </em>
                        </div>
                      </label>

                      <label>
                        <span> Incline </span>

                        <div class="input-with-unit">
                          <input
                            v-model.number="cardio.inclinePercent"
                            type="number"
                            min="0"
                            step="0.5"
                            placeholder="—"
                          />

                          <em> % </em>
                        </div>
                      </label>

                      <button
                        class="icon-button danger cardio-remove"
                        type="button"
                        title="Remove cardio entry"
                        @click="removeCardioEntry(exerciseIndex, cardioIndex)"
                      >
                        ×
                      </button>
                    </div>

                    <input
                      v-model="cardio.notes"
                      class="cardio-notes"
                      type="text"
                      placeholder="Cardio notes..."
                    />
                  </div>

                  <button
                    class="text-button"
                    type="button"
                    @click="addCardioEntry(exerciseIndex)"
                  >
                    + Add cardio entry
                  </button>
                </div>

                <div class="exercise-footer">
                  <input
                    v-model="exercise.notes"
                    class="exercise-notes"
                    type="text"
                    placeholder="Exercise notes..."
                  />
                </div>
              </template>
            </article>
          </div>

          <!-- ADD EXERCISE -->

          <button class="add-exercise" type="button" @click="store.addExercise">
            <span>+</span>
            Add exercise
          </button>

          <!-- SAVE -->

          <div class="save-bar">
            <div>
              <strong> Ready to save? </strong>

              <span> Review the parsed values before committing. </span>
            </div>

            <button
              class="button button-primary"
              type="button"
              :disabled="saving"
              @click="store.save"
            >
              <span v-if="saving" class="spinner" />

              <span v-else> Save workout </span>
            </button>
          </div>
        </template>
      </section>
    </section>
  </main>
</template>

<style scoped>
/* ============================================================
   LAYOUT
   ============================================================ */

.app-shell {
  min-height: 100vh;
  padding: 24px;
  background: #090a0c;
  color: #d7d9df;
}

.topbar {
  max-width: 1500px;
  margin: 0 auto 22px;

  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 9px;

  color: #f0f1f4;

  font-size: 15px;
  font-weight: 800;
  letter-spacing: -0.02em;
}

.brand-mark {
  display: grid;
  place-items: center;

  width: 26px;
  height: 26px;

  border-radius: 7px;

  background: #f1f2f5;
  color: #0a0b0d;

  font-size: 12px;
  font-weight: 900;
}

.subtitle {
  margin: 7px 0 0;

  color: #60646e;

  font-size: 11px;
}

.workspace {
  max-width: 1500px;
  margin: 0 auto;

  display: grid;
  grid-template-columns:
    minmax(350px, 0.75fr)
    minmax(650px, 1.25fr);

  gap: 18px;

  align-items: start;
}

.panel {
  min-width: 0;

  border: 1px solid #202229;
  border-radius: 12px;

  background: #0f1013;

  overflow: hidden;
}

.input-panel {
  position: sticky;
  top: 24px;

  display: flex;
  flex-direction: column;

  min-height: 650px;
}

.editor-panel {
  min-height: 650px;
}

/* ============================================================
   PANEL HEADER
   ============================================================ */

.panel-header {
  display: flex;
  justify-content: space-between;
  gap: 20px;

  padding: 20px 20px 16px;

  border-bottom: 1px solid #1d1f25;
}

.eyebrow {
  margin-bottom: 6px;

  color: #656973;

  font-size: 8px;
  font-weight: 900;
  letter-spacing: 0.12em;
}

.panel-header h1,
.panel-header h2 {
  margin: 0;

  color: #e9eaee;

  font-size: 16px;
  line-height: 1.2;
  letter-spacing: -0.025em;
}

.panel-header p {
  margin: 6px 0 0;

  color: #5e626c;

  font-size: 10px;
  line-height: 1.5;
}

.shortcut {
  display: grid;
  place-items: center;

  width: 27px;
  height: 22px;

  border: 1px solid #282b32;
  border-radius: 5px;

  color: #686c76;

  font-size: 8px;
  font-weight: 900;
  letter-spacing: 0.08em;
}

/* ============================================================
   RAW INPUT
   ============================================================ */

.workout-input {
  flex: 1;

  width: 100%;
  min-height: 460px;

  padding: 18px 20px;

  resize: vertical;

  border: 0;
  outline: 0;

  background: #0b0c0f;
  color: #cdd0d7;

  font-family: "JetBrains Mono", "SFMono-Regular", Consolas, monospace;

  font-size: 11px;
  line-height: 1.7;

  tab-size: 2;
}

.workout-input::placeholder {
  color: #393c44;
}

.input-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;

  padding: 12px 16px;

  border-top: 1px solid #1d1f25;

  color: #4f535d;

  font-size: 9px;
}

/* ============================================================
   BARBELL OPTION
   ============================================================ */

.parser-options {
  margin: 0 16px 12px;
  padding: 12px;

  border: 1px solid #252830;
  border-radius: 9px;

  background: #0d0e11;
}

.checkbox-option {
  display: flex;
  align-items: flex-start;
  gap: 10px;

  cursor: pointer;
}

.checkbox-option input {
  position: absolute;

  opacity: 0;
  pointer-events: none;
}

.checkbox-ui {
  position: relative;

  flex: 0 0 auto;

  width: 17px;
  height: 17px;

  margin-top: 1px;

  border: 1px solid #3a3d46;
  border-radius: 5px;

  background: #0a0b0d;
}

.checkbox-option input:checked + .checkbox-ui {
  border-color: #f1f2f6;
  background: #f1f2f6;
}

.checkbox-option input:checked + .checkbox-ui::after {
  content: "";

  position: absolute;

  left: 4px;
  top: 1px;

  width: 5px;
  height: 9px;

  border: solid #0b0c0f;
  border-width: 0 2px 2px 0;

  transform: rotate(45deg);
}

.checkbox-copy {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.checkbox-copy strong {
  color: #cdd0d8;

  font-size: 10px;
}

.checkbox-copy small {
  color: #5e626d;

  font-size: 9px;
  line-height: 1.45;
}

/* ============================================================
   BUTTONS
   ============================================================ */

.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;

  min-height: 32px;
  padding: 0 12px;

  border-radius: 6px;

  font-size: 10px;
  font-weight: 800;

  cursor: pointer;

  transition:
    background 120ms ease,
    border-color 120ms ease,
    opacity 120ms ease;
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.45;
}

.button-primary {
  border: 1px solid #dfe1e6;

  background: #eceef2;
  color: #0a0b0d;
}

.button-primary:hover:not(:disabled) {
  background: #ffffff;
}

.button-secondary {
  border: 1px solid #292c33;

  background: #121419;
  color: #9da1aa;
}

.button-secondary:hover {
  border-color: #3a3d46;
  color: #d2d4da;
}

.spinner {
  width: 11px;
  height: 11px;

  border: 2px solid #858993;
  border-top-color: transparent;

  border-radius: 50%;

  animation: spin 700ms linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* ============================================================
   STATS
   ============================================================ */

.editor-header {
  align-items: center;
}

.stats {
  display: flex;
  gap: 14px;
}

.stat {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 2px;
}

.stat strong {
  color: #d9dbe0;

  font-size: 13px;
  line-height: 1;
}

.stat span {
  color: #50545e;

  font-size: 7px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

/* ============================================================
   NOTICES
   ============================================================ */

.notice {
  margin: 12px 16px 0;
  padding: 9px 11px;

  border-radius: 6px;

  font-size: 10px;
}

.notice-error {
  border: 1px solid #432b2e;
  background: #1b1012;
  color: #d58c94;
}

.notice-success {
  border: 1px solid #2b4035;
  background: #101a15;
  color: #8db39e;
}

.ai-meta {
  display: flex;
  align-items: center;
  gap: 7px;

  margin: 12px 16px 0;

  color: #555963;

  font-size: 8px;
  font-family: "JetBrains Mono", monospace;
}

.ai-dot {
  width: 5px;
  height: 5px;

  border-radius: 50%;

  background: #7caa91;
}

.separator {
  color: #34373e;
}

/* ============================================================
   EMPTY
   ============================================================ */

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  min-height: 500px;

  padding: 40px;

  text-align: center;
}

.empty-icon {
  margin-bottom: 12px;

  color: #555963;

  font-size: 20px;
}

.empty-state h3 {
  margin: 0;

  color: #aeb1b8;

  font-size: 12px;
}

.empty-state p {
  max-width: 280px;

  margin: 7px 0 0;

  color: #555963;

  font-size: 9px;
  line-height: 1.6;
}

/* ============================================================
   WORKOUT META
   ============================================================ */

.workout-meta {
  display: grid;
  grid-template-columns: 1fr 170px;
  gap: 10px;

  padding: 16px;

  border-bottom: 1px solid #1d1f25;
}

.workout-meta label {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.workout-meta .full-width {
  grid-column: 1 / -1;
}

.workout-meta label > span {
  color: #5b5f68;

  font-size: 8px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.workout-meta input,
.workout-meta textarea {
  width: 100%;

  border: 1px solid #292c33;
  border-radius: 6px;

  outline: 0;

  background: #0b0c0f;
  color: #cdd0d7;

  font: inherit;
  font-size: 10px;
}

.workout-meta input {
  height: 31px;
  padding: 0 9px;
}

.workout-meta textarea {
  padding: 8px 9px;
  resize: vertical;
}

.workout-meta input:focus,
.workout-meta textarea:focus {
  border-color: #444750;
}

/* ============================================================
   EXERCISE LIST
   ============================================================ */

.exercise-list {
  display: flex;
  flex-direction: column;
  gap: 10px;

  padding: 16px;
}

.exercise-card {
  overflow: hidden;

  border: 1px solid #252830;
  border-radius: 8px;

  background: #111216;
}

.exercise-header {
  display: flex;
  align-items: center;
  gap: 10px;

  padding: 10px 11px;

  border-bottom: 1px solid #202229;
}

.exercise-number {
  display: grid;
  place-items: center;

  width: 25px;
  height: 25px;

  flex: 0 0 auto;

  border: 1px solid #292c33;
  border-radius: 5px;

  color: #5c606a;

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
  color: #d7d9df;

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
  padding: 0 6px;

  border: 1px solid #2d3038;
  border-radius: 4px;

  color: #777b86;

  font-size: 7px;
  font-weight: 900;

  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.category-badge.cardio {
  border-color: #303a36;
  color: #8aab9b;
}

.muscle-input {
  width: 110px;

  border: 0;
  outline: 0;

  background: transparent;
  color: #626670;

  font-size: 8px;
}

.icon-button {
  display: grid;
  place-items: center;

  width: 26px;
  height: 26px;

  flex: 0 0 auto;

  border: 1px solid #292c33;
  border-radius: 5px;

  background: transparent;

  color: #60646e;

  cursor: pointer;
}

.icon-button:hover {
  border-color: #3a3d46;
}

.icon-button.danger:hover {
  border-color: #573238;
  color: #d58c94;
}

/* ============================================================
   SET TABLE
   ============================================================ */

.set-table {
  width: 100%;
}

.set-row {
  display: grid;

  grid-template-columns:
    42px
    minmax(85px, 1fr)
    minmax(55px, 0.65fr)
    minmax(50px, 0.6fr)
    minmax(50px, 0.6fr)
    minmax(90px, 0.9fr)
    30px;

  align-items: center;

  min-width: 0;

  border-bottom: 1px solid #1b1d22;
}

.set-row > * {
  min-width: 0;
}

.set-head {
  min-height: 25px;

  background: #0d0e11;
}

.set-head span {
  padding: 0 7px;

  color: #4f535d;

  font-size: 7px;
  font-weight: 900;
  letter-spacing: 0.06em;
}

.set-row:not(.set-head) {
  min-height: 42px;
}

.set-row input,
.set-row select {
  width: calc(100% - 10px);
  height: 28px;

  margin: 0 5px;

  border: 1px solid #292c33;
  border-radius: 5px;

  outline: 0;

  background: #0b0c0f;
  color: #bfc2c9;

  font-size: 9px;
}

.set-row input {
  padding: 0 7px;
}

.set-row select {
  padding: 0 5px;
}

.set-row input:focus,
.set-row select:focus {
  border-color: #444750;
}

.set-number {
  text-align: center;
}

.weight-cell {
  position: relative;

  display: flex;
  align-items: center;
}

.weight-cell input {
  padding-right: 25px;
}

.weight-mode {
  position: absolute;
  right: 11px;

  color: #4e525c;

  font-size: 7px;
  pointer-events: none;
}

.remove-set {
  display: grid;
  place-items: center;

  width: 22px;
  height: 22px;

  margin: auto;

  border: 0;
  background: transparent;

  color: #4d515a;

  font-size: 14px;

  cursor: pointer;
}

.remove-set:hover {
  color: #d58c94;
}

/* ============================================================
   SET NOTES
   ============================================================ */

.set-notes-row {
  display: grid;
  grid-template-columns: 60px 1fr;
  gap: 8px;

  padding: 6px 10px;

  border-bottom: 1px solid #1b1d22;
}

.set-notes-row span {
  display: flex;
  align-items: center;

  color: #474b54;

  font-size: 7px;
  font-weight: 800;
  text-transform: uppercase;
}

.set-notes-row input {
  width: 100%;
  height: 25px;

  padding: 0 7px;

  border: 1px solid #24272e;
  border-radius: 5px;

  outline: 0;

  background: #0b0c0f;
  color: #8f929a;

  font-size: 8px;
}

.set-notes-row input:focus {
  border-color: #3d4048;
}

/* ============================================================
   EXERCISE FOOTER
   ============================================================ */

.exercise-footer {
  display: flex;
  align-items: center;
  gap: 10px;

  padding: 9px 10px;
}

.text-button {
  flex: 0 0 auto;

  border: 0;
  background: transparent;

  color: #747882;

  font-size: 8px;
  font-weight: 800;

  cursor: pointer;
}

.text-button:hover {
  color: #c6c8ce;
}

.exercise-notes {
  flex: 1;
  min-width: 0;

  height: 27px;

  padding: 0 8px;

  border: 1px solid #24272e;
  border-radius: 5px;

  outline: 0;

  background: #0b0c0f;
  color: #9da0a8;

  font-size: 8px;
}

.exercise-notes:focus {
  border-color: #3d4048;
}

/* ============================================================
   CARDIO
   ============================================================ */

.cardio-editor {
  padding: 10px;

  border-bottom: 1px solid #1b1d22;
}

.cardio-entry {
  padding: 9px;

  border: 1px solid #22252c;
  border-radius: 6px;

  background: #0d0e11;
}

.cardio-entry + .cardio-entry {
  margin-top: 7px;
}

.cardio-grid {
  display: grid;
  grid-template-columns:
    repeat(4, minmax(90px, 1fr))
    28px;

  gap: 8px;

  align-items: end;
}

.cardio-grid label {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.cardio-grid label > span {
  color: #575b65;

  font-size: 7px;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.cardio-grid label > small {
  color: #4d515a;
  font-size: 7px;
}

.cardio-grid input {
  width: 100%;
  height: 28px;

  padding: 0 7px;

  border: 1px solid #292c33;
  border-radius: 5px;

  outline: 0;

  background: #0a0b0e;
  color: #c1c4ca;

  font-size: 9px;
}

.cardio-grid input:focus {
  border-color: #444750;
}

.input-with-unit {
  position: relative;
}

.input-with-unit input {
  padding-right: 34px;
}

.input-with-unit em {
  position: absolute;
  right: 8px;
  top: 50%;

  transform: translateY(-50%);

  color: #484c55;

  font-size: 7px;
  font-style: normal;
}

.cardio-remove {
  margin-bottom: 1px;
}

.cardio-notes {
  width: 100%;

  height: 27px;

  margin-top: 8px;
  padding: 0 8px;

  border: 1px solid #24272e;
  border-radius: 5px;

  outline: 0;

  background: #0a0b0e;
  color: #9699a1;

  font-size: 8px;
}

.cardio-notes:focus {
  border-color: #3d4048;
}

/* ============================================================
   ADD EXERCISE
   ============================================================ */

.add-exercise {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 7px;

  width: calc(100% - 32px);
  height: 38px;

  margin: 0 16px 16px;

  border: 1px dashed #292c33;
  border-radius: 7px;

  background: transparent;

  color: #676b75;

  font-size: 9px;
  font-weight: 800;

  cursor: pointer;
}

.add-exercise:hover {
  border-color: #41444c;
  color: #a1a4ac;
}

.add-exercise span {
  font-size: 14px;
  font-weight: 400;
}

/* ============================================================
   SAVE BAR
   ============================================================ */

.save-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 15px;

  padding: 14px 16px;

  border-top: 1px solid #202229;

  background: #0d0e11;
}

.save-bar > div {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.save-bar strong {
  color: #cdd0d7;

  font-size: 9px;
}

.save-bar span {
  color: #50545d;

  font-size: 8px;
}

/* ============================================================
   RESPONSIVE
   ============================================================ */

@media (max-width: 1100px) {
  .workspace {
    grid-template-columns: 1fr;
  }

  .input-panel {
    position: static;
    min-height: auto;
  }

  .workout-input {
    min-height: 350px;
  }
}

@media (max-width: 700px) {
  .app-shell {
    padding: 10px;
  }

  .topbar {
    align-items: center;
  }

  .subtitle {
    display: none;
  }

  .editor-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .stats {
    width: 100%;
    justify-content: space-between;
  }

  .workout-meta {
    grid-template-columns: 1fr;
  }

  .workout-meta .full-width {
    grid-column: auto;
  }

  .set-table {
    overflow-x: auto;
  }

  .set-row {
    min-width: 620px;
  }

  .cardio-grid {
    grid-template-columns: 1fr 1fr;
  }

  .cardio-remove {
    position: absolute;
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
``` A couple of important points about this version: * **Do not add `weightUnit`
back anywhere.** The frontend now displays kg implicitly. * The
`weightEntryMode` is still displayed as `/side` when the parser determined the
original value was per-side. The actual `weight` remains the normalized total. *
Cardio has no sets at all. * The exercise dropdown uses the same `exercises`
catalog that the parser is supposed to use. * `exercise.notes` and individual
`set.notes` are now editable. * Your existing `store.addExercise()` must create
`category: "Strength"` and `cardio: []`, as in the store code I gave previously.
* Your store must expose `cardioCount`, `strengthCount`, and
`barbellWeightsArePerSide` through its returned object. Otherwise the
destructuring at the top of this file will fail. One correction to my previous
code: **the frontend exercise catalog should not be duplicated manually if your
backend is already going to expose the catalog.** This `App.vue` works with the
static `src/data/exercises.ts` version I gave above; later we can switch that to
`GET /api/exercises` without changing the editor structure.
