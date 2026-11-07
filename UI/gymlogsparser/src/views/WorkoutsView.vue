<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";

import {
  deleteWorkout,
  getWorkout,
  getWorkouts,
  updateWorkout,
} from "@/api/client";
import type {
  CardioEntry,
  ExerciseCategory,
  WorkoutExercise,
  WorkoutLog,
  WorkoutSet,
  WorkoutSummary,
} from "@/types/workout";
import ExerciseEditModal from "@/components/ExerciseEditModal.vue";
import { useExerciseCatalogStore } from "@/stores/exercises";
import { formatDuration } from "@/utils/format";

const catalog = useExerciseCatalogStore();

const workouts = ref<WorkoutSummary[]>([]);
const searchQuery = ref("");

const selectedId = ref<string | null>(null);
const workout = ref<WorkoutLog | null>(null);
const originalSnapshot = ref<string>("");

const loadingList = ref(false);
const loadingDetail = ref(false);
const saving = ref(false);
const deleting = ref(false);
const listError = ref<string | null>(null);
const detailError = ref<string | null>(null);

const personInput = ref("");
const isMobile = ref(window.innerWidth < 560);

function onResize() {
  isMobile.value = window.innerWidth < 560;
}

onMounted(() => {
  window.addEventListener("resize", onResize);
  loadWorkouts();
  catalog.ensureLoaded();
});

onUnmounted(() => {
  window.removeEventListener("resize", onResize);
});

const filteredWorkouts = computed(() => {
  const q = searchQuery.value.trim().toLowerCase();
  if (!q) return workouts.value;
  return workouts.value.filter((w) =>
    (w.title ?? "").toLowerCase().includes(q),
  );
});

const maxSets = computed(() =>
  Math.max(1, ...workouts.value.map((w) => w.totalSets)),
);

const isDirty = computed(() => {
  if (!workout.value) return false;
  return (
    JSON.stringify(sanitizeForSave(workout.value)) !== originalSnapshot.value
  );
});

async function loadWorkouts() {
  loadingList.value = true;
  listError.value = null;

  try {
    workouts.value = await getWorkouts();
  } catch (err) {
    listError.value =
      err instanceof Error ? err.message : "Nie udało się wczytać treningów.";
  } finally {
    loadingList.value = false;
  }
}

function selectWorkout(id: string) {
  if (id === selectedId.value) return;
  selectedId.value = id;
  loadWorkoutDetail(id);
}

function closeDetail() {
  selectedId.value = null;
  workout.value = null;
  detailError.value = null;
}

async function loadWorkoutDetail(id: string) {
  loadingDetail.value = true;
  detailError.value = null;
  workout.value = null;

  try {
    const data = await getWorkout(id);
    const sanitized = sanitizeForSave(data);
    workout.value = sanitized;
    originalSnapshot.value = JSON.stringify(sanitized);
  } catch (err) {
    detailError.value =
      err instanceof Error ? err.message : "Nie udało się wczytać treningu.";
  } finally {
    loadingDetail.value = false;
  }
}

function toNum(v: unknown): number | null {
  if (v === "" || v === null || v === undefined) return null;
  const n = typeof v === "number" ? v : Number(v);
  return Number.isNaN(n) ? null : n;
}

/** Coerces stray empty-string values left behind by cleared number inputs. */
function sanitizeExercise(ex: WorkoutExercise): WorkoutExercise {
  return {
    ...ex,
    sets: ex.sets.map((s) => ({
      ...s,
      weight: toNum(s.weight),
      reps: toNum(s.reps),
      rir: toNum(s.rir),
      rpe: toNum(s.rpe),
    })),
    cardio: ex.cardio.map((c) => ({
      ...c,
      durationSeconds: toNum(c.durationSeconds),
      distanceKm: toNum(c.distanceKm),
      speedKmh: toNum(c.speedKmh),
      inclinePercent: toNum(c.inclinePercent),
    })),
  };
}

function sanitizeForSave(source: WorkoutLog): WorkoutLog {
  return {
    ...source,
    exercises: source.exercises.map(sanitizeExercise),
  };
}

async function save() {
  if (!workout.value || !selectedId.value) return;

  saving.value = true;
  detailError.value = null;

  try {
    const payload = sanitizeForSave(workout.value);
    await updateWorkout(selectedId.value, payload);
    workout.value = payload;
    originalSnapshot.value = JSON.stringify(payload);
    await loadWorkouts();
  } catch (err) {
    detailError.value =
      err instanceof Error ? err.message : "Nie udało się zapisać treningu.";
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!selectedId.value) return;
  const ok = window.confirm(
    "Usunąć ten trening? Tej operacji nie można cofnąć.",
  );
  if (!ok) return;

  deleting.value = true;
  detailError.value = null;

  try {
    await deleteWorkout(selectedId.value);
    workouts.value = workouts.value.filter((w) => w.id !== selectedId.value);
    closeDetail();
  } catch (err) {
    detailError.value =
      err instanceof Error ? err.message : "Nie udało się usunąć treningu.";
  } finally {
    deleting.value = false;
  }
}

// --- persons ---

function addPerson() {
  const name = personInput.value.trim();
  if (!name || !workout.value) return;
  workout.value.persons.push(name);
  personInput.value = "";
}

function removePerson(index: number) {
  workout.value?.persons.splice(index, 1);
}

// --- exercises ---
// Exercises are edited in ExerciseEditModal, on a copy; the result replaces the
// exercise in the workout, which is then saved with "Zapisz zmiany" as before.

interface ExerciseEditorState {
  /** Position in workout.exercises, or null for a new exercise. */
  index: number | null;
  exercise: WorkoutExercise;
}

const exerciseEditor = ref<ExerciseEditorState | null>(null);

function openExerciseEditor(index: number) {
  const exercise = workout.value?.exercises[index];
  if (!exercise) return;
  exerciseEditor.value = { index, exercise };
}

function addExercise(category: ExerciseCategory) {
  if (!workout.value) return;

  exerciseEditor.value = {
    index: null,
    exercise: {
      exerciseId: "",
      name: "",
      muscleGroup: null,
      notes: null,
      category,
      sets: category === "Strength" ? [emptySet(1)] : [],
      cardio: category === "Cardio" ? [emptyCardio()] : [],
    },
  };
}

function onExerciseSaved(updated: WorkoutExercise) {
  const state = exerciseEditor.value;
  exerciseEditor.value = null;
  if (!workout.value || !state) return;

  const exercise = sanitizeExercise(updated);
  if (state.index === null) workout.value.exercises.push(exercise);
  else workout.value.exercises.splice(state.index, 1, exercise);
}

function closeExerciseEditor() {
  exerciseEditor.value = null;
}

function removeExercise(index: number) {
  const exercise = workout.value?.exercises[index];
  if (!exercise) return;
  const name = exercise.name || "bez nazwy";
  if (!window.confirm(`Usunąć ćwiczenie „${name}” z tego treningu?`)) return;
  workout.value?.exercises.splice(index, 1);
}

function emptySet(setNumber: number): WorkoutSet {
  return {
    setNumber,
    weight: null,
    weightEntryMode: null,
    reps: null,
    rir: null,
    rpe: null,
    warmup: false,
    notes: null,
  };
}

function emptyCardio(): CardioEntry {
  return {
    activity: "",
    durationSeconds: null,
    distanceKm: null,
    speedKmh: null,
    inclinePercent: null,
    notes: null,
  };
}

function isOffCatalog(ex: WorkoutExercise): boolean {
  return (
    catalog.definitions.length > 0 &&
    !!ex.exerciseId &&
    !catalog.byId(ex.exerciseId)
  );
}

const numberFormat = new Intl.NumberFormat("pl-PL", {
  maximumFractionDigits: 2,
});

function fmt(n: number): string {
  return numberFormat.format(n);
}

function isNum(v: unknown): v is number {
  return typeof v === "number" && !Number.isNaN(v);
}

function setsSummary(ex: WorkoutExercise): string {
  const n = ex.sets.length;
  if (n === 0) return "brak serii";

  const parts = [`${n} ${plural(n, "seria", "serie", "serii")}`];

  let top: WorkoutSet | null = null;
  for (const s of ex.sets) {
    if (isNum(s.weight) && s.weight > 0 && (!top || s.weight > (top.weight ?? 0)))
      top = s;
  }
  if (top && isNum(top.weight)) {
    parts.push(
      `maks. ${fmt(top.weight)} kg${isNum(top.reps) ? ` × ${top.reps}` : ""}`,
    );
  }

  return parts.join(" · ");
}

interface SetChip {
  label: string;
  warmup: boolean;
  more: boolean;
}

const MAX_SET_CHIPS = 8;

function setChips(ex: WorkoutExercise): SetChip[] {
  const chips: SetChip[] = ex.sets.slice(0, MAX_SET_CHIPS).map((s) => {
    const weight = isNum(s.weight) ? fmt(s.weight) : "—";
    const reps = isNum(s.reps) ? String(s.reps) : "—";
    return { label: `${weight}×${reps}`, warmup: s.warmup === true, more: false };
  });
  const rest = ex.sets.length - MAX_SET_CHIPS;
  if (rest > 0) chips.push({ label: `+${rest}`, warmup: false, more: true });
  return chips;
}

function cardioSummary(ex: WorkoutExercise): string {
  const n = ex.cardio.length;
  if (n === 0) return "brak wpisów";

  const parts = [`${n} ${plural(n, "wpis", "wpisy", "wpisów")}`];

  const seconds = ex.cardio.reduce(
    (sum, c) => sum + (isNum(c.durationSeconds) ? c.durationSeconds : 0),
    0,
  );
  if (seconds > 0) parts.push(formatDuration(seconds));

  const km = ex.cardio.reduce(
    (sum, c) => sum + (isNum(c.distanceKm) ? c.distanceKm : 0),
    0,
  );
  if (km > 0) parts.push(`${fmt(km)} km`);

  return parts.join(" · ");
}

// --- time inputs (TimeOnly comes through as "HH:mm:ss") ---

function toTimeInput(t: string | null): string {
  return t ? t.slice(0, 5) : "";
}

function onStartTimeInput(e: Event) {
  if (!workout.value) return;
  const v = (e.target as HTMLInputElement).value;
  workout.value.startTime = v ? `${v}:00` : null;
}

function onEndTimeInput(e: Event) {
  if (!workout.value) return;
  const v = (e.target as HTMLInputElement).value;
  workout.value.endTime = v ? `${v}:00` : null;
}

// --- display helpers ---

function formatDate(date: string | null): string {
  if (!date) return "Bez daty";
  const d = new Date(date);
  if (Number.isNaN(d.getTime())) return date;
  return new Intl.DateTimeFormat("pl-PL", {
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(d);
}

/** Polish plural: 1 seria, 2–4 serie (but 12–14 serii), 5+ serii. */
function plural(n: number, one: string, few: string, many: string): string {
  if (n === 1) return one;
  const lastDigit = n % 10;
  const lastTwo = n % 100;
  if (lastDigit >= 2 && lastDigit <= 4 && !(lastTwo >= 12 && lastTwo <= 14))
    return few;
  return many;
}

function pluralExercises(n: number): string {
  return plural(n, "ćwiczenie", "ćwiczenia", "ćwiczeń");
}

function barWidth(totalSets: number): number {
  if (totalSets <= 0) return 4;
  return Math.max(6, (totalSets / maxSets.value) * 100);
}
</script>

<template>
  <div class="workouts-view">
    <div class="ambient" aria-hidden="true"></div>

    <header class="page-header">
      <h1>Treningi</h1>
      <p class="subtitle">Przeglądaj zapisane sesje i wprowadzaj poprawki.</p>
    </header>

    <div class="layout">
      <section
        class="list-panel glass-panel"
        :class="{ 'list-panel--hidden-mobile': isMobile && selectedId }"
      >
        <div class="list-panel__search">
          <input
            v-model="searchQuery"
            type="text"
            placeholder="Szukaj po nazwie…"
          />
        </div>

        <p v-if="listError" class="status-text status-text--error">
          {{ listError }}
        </p>
        <p v-else-if="loadingList" class="status-text">
          Wczytywanie treningów…
        </p>
        <p v-else-if="filteredWorkouts.length === 0" class="status-text">
          {{
            workouts.length === 0
              ? "Brak zapisanych treningów. Zaloguj pierwszy trening, aby zobaczyć go tutaj."
              : "Nic nie pasuje do wyszukiwania."
          }}
        </p>

        <ul v-else class="workout-rows">
          <li
            v-for="(w, i) in filteredWorkouts"
            :key="w.id"
            :style="{ '--stagger': i }"
          >
            <button
              type="button"
              class="workout-row"
              :class="{ 'workout-row--active': w.id === selectedId }"
              @click="selectWorkout(w.id)"
            >
              <span class="workout-row__glyph">{{
                w.hasCardio ? "♞" : "♟"
              }}</span>
              <span class="workout-row__main">
                <span class="workout-row__title">{{
                  w.title || "Trening bez nazwy"
                }}</span>
                <span class="workout-row__meta"
                  >{{ formatDate(w.date) }} · {{ w.exerciseCount }}
                  {{ pluralExercises(w.exerciseCount) }}</span
                >
              </span>
              <span class="workout-row__bar-track" aria-hidden="true">
                <span
                  class="workout-row__bar"
                  :style="{ width: barWidth(w.totalSets) + '%' }"
                ></span>
              </span>
            </button>
          </li>
        </ul>
      </section>

      <section v-if="selectedId" class="detail-panel glass-panel">
        <button
          v-if="isMobile"
          type="button"
          class="back-button"
          @click="closeDetail"
        >
          ← Wróć do listy
        </button>

        <p v-if="loadingDetail" class="status-text">Wczytywanie treningu…</p>
        <p
          v-else-if="detailError && !workout"
          class="status-text status-text--error"
        >
          {{ detailError }}
        </p>

        <template v-else-if="workout">
          <div class="detail-header">
            <input
              v-model="workout.title"
              type="text"
              class="title-input"
              placeholder="Nazwa treningu"
            />

            <div class="detail-header__fields">
              <label class="field">
                <span>Data</span>
                <input v-model="workout.date" type="date" />
              </label>
              <label class="field">
                <span>Start</span>
                <input
                  :value="toTimeInput(workout.startTime)"
                  type="time"
                  @input="onStartTimeInput"
                />
              </label>
              <label class="field">
                <span>Koniec</span>
                <input
                  :value="toTimeInput(workout.endTime)"
                  type="time"
                  @input="onEndTimeInput"
                />
              </label>
            </div>
          </div>

          <div class="persons-section">
            <span class="section-label"
              ><span class="glyph">♚</span> Kto trenował</span
            >
            <div class="chips">
              <span v-for="(p, idx) in workout.persons" :key="idx" class="chip">
                {{ p }}
                <button
                  type="button"
                  aria-label="Usuń osobę"
                  @click="removePerson(idx)"
                >
                  ×
                </button>
              </span>
              <input
                v-model="personInput"
                type="text"
                class="chip-input"
                placeholder="Dodaj osobę i naciśnij Enter"
                @keydown.enter.prevent="addPerson"
              />
            </div>
          </div>

          <label class="field field--block">
            <span class="section-label"
              ><span class="glyph">♝</span> Notatki</span
            >
            <textarea
              v-model="workout.notes"
              rows="2"
              placeholder="Jak poszło?"
            ></textarea>
          </label>

          <div class="exercises-section">
            <div class="exercises-header">
              <span class="section-label"
                ><span class="glyph">♜</span> Ćwiczenia</span
              >
              <div class="add-exercise-buttons">
                <button
                  type="button"
                  class="ghost-button"
                  @click="addExercise('Strength')"
                >
                  ♟ Dodaj siłowe
                </button>
                <button
                  type="button"
                  class="ghost-button"
                  @click="addExercise('Cardio')"
                >
                  ♞ Dodaj cardio
                </button>
              </div>
            </div>

            <p v-if="workout.exercises.length === 0" class="status-text">
              Brak ćwiczeń w tym treningu. Dodaj pierwsze powyżej.
            </p>

            <ul v-else class="exercise-cards">
              <li
                v-for="(ex, exIdx) in workout.exercises"
                :key="exIdx"
                class="exercise-card"
              >
                <button
                  type="button"
                  class="exercise-card__main"
                  :aria-label="`Edytuj ćwiczenie: ${ex.name || 'bez nazwy'}`"
                  @click="openExerciseEditor(exIdx)"
                >
                  <span class="exercise-card__index">{{ exIdx + 1 }}</span>
                  <span class="exercise-card__body">
                    <span class="exercise-card__title">
                      <span class="exercise-card__name">{{
                        ex.name || "Ćwiczenie bez nazwy"
                      }}</span>
                      <span
                        v-if="isOffCatalog(ex)"
                        class="exercise-card__flag"
                        title="Tego ćwiczenia nie ma w katalogu. Otwórz je i wybierz z listy."
                        >spoza katalogu</span
                      >
                    </span>
                    <span class="exercise-card__meta">
                      <span
                        class="exercise-card__kind"
                        :class="{
                          'exercise-card__kind--cardio':
                            ex.category === 'Cardio',
                        }"
                        >{{ ex.category === "Cardio" ? "Cardio" : "Siłowe" }}</span
                      >
                      <template v-if="ex.muscleGroup">
                        · {{ ex.muscleGroup }}</template
                      >
                      ·
                      {{
                        ex.category === "Cardio"
                          ? cardioSummary(ex)
                          : setsSummary(ex)
                      }}
                    </span>
                    <span
                      v-if="ex.category === 'Strength' && ex.sets.length"
                      class="exercise-card__sets"
                    >
                      <span
                        v-for="(chip, cIdx) in setChips(ex)"
                        :key="cIdx"
                        class="set-chip"
                        :class="{
                          'set-chip--warmup': chip.warmup,
                          'set-chip--more': chip.more,
                        }"
                        >{{ chip.label }}</span
                      >
                    </span>
                    <span v-if="ex.notes" class="exercise-card__notes">{{
                      ex.notes
                    }}</span>
                  </span>
                  <span class="exercise-card__edit" aria-hidden="true">
                    <svg viewBox="0 0 16 16" width="13" height="13">
                      <path
                        d="M10.5 2.5l3 3L6 13H3v-3z"
                        fill="none"
                        stroke="currentColor"
                        stroke-width="1.3"
                        stroke-linejoin="round"
                      />
                    </svg>
                    Edytuj
                  </span>
                </button>
                <button
                  type="button"
                  class="remove-button"
                  :aria-label="`Usuń ćwiczenie: ${ex.name || 'bez nazwy'}`"
                  title="Usuń ćwiczenie"
                  @click="removeExercise(exIdx)"
                >
                  ×
                </button>
              </li>
            </ul>
          </div>

          <footer class="detail-footer">
            <span v-if="isDirty" class="dirty-indicator"
              >Masz niezapisane zmiany</span
            >
            <span v-else class="dirty-indicator dirty-indicator--muted"
              >Wszystko zapisane</span
            >

            <p
              v-if="detailError"
              class="status-text status-text--error status-text--inline"
            >
              {{ detailError }}
            </p>

            <div class="footer-actions">
              <button
                type="button"
                class="text-button"
                :disabled="deleting"
                @click="confirmDelete"
              >
                {{ deleting ? "Usuwanie…" : "Usuń trening" }}
              </button>
              <button
                type="button"
                class="save-button"
                :disabled="!isDirty || saving"
                @click="save"
              >
                {{ saving ? "Zapisywanie…" : "Zapisz zmiany" }}
              </button>
            </div>
          </footer>
        </template>
      </section>

      <section v-else class="detail-panel detail-panel--empty glass-panel">
        <p class="status-text">
          Wybierz trening z listy, aby zobaczyć szczegóły i go edytować.
        </p>
      </section>
    </div>

    <ExerciseEditModal
      v-if="exerciseEditor"
      :exercise="exerciseEditor.exercise"
      :is-new="exerciseEditor.index === null"
      @save="onExerciseSaved"
      @cancel="closeExerciseEditor"
    />
  </div>
</template>

<style scoped>
.workouts-view {
  position: relative;
  max-width: 1100px;
  margin: 0 auto;
}

/* --- bold ambient moment: faint chessboard + radial accent glow behind the header --- */
.ambient {
  position: absolute;
  inset: -40px -24px auto -24px;
  height: 260px;
  z-index: -1;
  pointer-events: none;

  background-image:
    radial-gradient(480px 220px at 15% 0%, var(--accent-dim), transparent 70%),
    repeating-conic-gradient(
      var(--glass-soft) 0% 25%,
      transparent 0% 50%
    );
  background-size:
    auto,
    28px 28px;
  mask-image: linear-gradient(to bottom, black, transparent);
}

.page-header {
  margin-bottom: 22px;
  animation: rise 0.5s ease both;
}

.page-header h1 {
  margin: 0 0 4px;
  font-family: var(--font-display);
  font-size: 1.5rem;
  font-weight: 600;
  color: var(--text);
}

.subtitle {
  margin: 0;
  font-size: 0.9rem;
  color: var(--text-faint);
}

.layout {
  display: grid;
  grid-template-columns: 320px 1fr;
  gap: var(--card-gap-md);
  align-items: start;
}

/* --- list panel --- */

.list-panel {
  padding: var(--card-pad-sm);
  animation: rise 0.5s ease 0.05s both;
}

.list-panel__search input {
  width: 100%;
  box-sizing: border-box;
  padding: 9px 12px;
  margin-bottom: 10px;

  background: var(--surface-hover);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text);
  font-family: var(--font-body);
  font-size: 0.88rem;
}

.list-panel__search input:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.workout-rows {
  list-style: none;
  margin: 0;
  padding: 0;
  border-top: 1px solid var(--border);
}

.workout-rows li {
  animation: rise 0.4s ease both;
  animation-delay: calc(var(--stagger, 0) * 45ms);
}

.workout-row {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 11px 6px;

  background: transparent;
  border: none;
  border-bottom: 1px solid var(--border);
  border-left: 2px solid transparent;
  color: var(--text);
  text-align: left;
  cursor: pointer;
  font-family: var(--font-body);
}

.workout-row:hover {
  background: var(--surface-hover);
}

.workout-row:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: -2px;
}

.workout-row--active {
  border-left-color: var(--accent);
  background: var(--accent-dim);
}

.workout-row__glyph {
  flex: none;
  width: 20px;
  text-align: center;
  font-size: 1.05rem;
  color: var(--text-dim);
}

.workout-row--active .workout-row__glyph {
  color: var(--accent-strong);
}

.workout-row__main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.workout-row__title {
  font-size: 0.9rem;
  color: var(--text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.workout-row__meta {
  font-size: 0.75rem;
  color: var(--text-faint);
}

.workout-row__bar-track {
  flex: none;
  width: 36px;
  height: 4px;
  border-radius: var(--radius-pill);
  background: var(--surface-hover);
  overflow: hidden;
}

.workout-row__bar {
  display: block;
  height: 100%;
  background: var(--accent);
  border-radius: var(--radius-pill);
}

/* --- detail panel --- */

.detail-panel {
  padding: var(--card-pad);
  animation: rise 0.5s ease 0.1s both;
}

.detail-panel--empty {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 200px;
}

.back-button {
  background: none;
  border: none;
  color: var(--text-dim);
  font-size: 0.85rem;
  padding: 0 0 14px;
  cursor: pointer;
  font-family: var(--font-body);
}

.back-button:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 2px;
}

.detail-header {
  margin-bottom: 18px;
}

.title-input {
  width: 100%;
  box-sizing: border-box;
  padding: 4px 0;
  margin-bottom: 12px;

  background: transparent;
  border: none;
  border-bottom: 1px solid transparent;
  color: var(--text);
  font-family: var(--font-display);
  font-size: 1.25rem;
  font-weight: 600;
}

.title-input:hover {
  border-bottom-color: var(--border);
}

.title-input:focus-visible {
  outline: none;
  border-bottom-color: var(--accent-border);
}

.detail-header__fields {
  display: flex;
  gap: 14px;
  flex-wrap: wrap;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.field > span:first-child {
  font-size: 0.72rem;
  color: var(--text-faint);
}

.field input,
.field textarea {
  box-sizing: border-box;
  padding: 7px 9px;

  background: var(--surface-hover);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text);
  font-family: var(--font-body);
  font-size: 0.85rem;
}

.field input:focus-visible,
.field textarea:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.field--block {
  width: 100%;
  margin: 14px 0;
}

.field--block textarea {
  resize: vertical;
  font-family: var(--font-body);
}

.section-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.8rem;
  color: var(--text-dim);
  margin-bottom: 8px;
}

.glyph {
  color: var(--text-faint);
}

.glyph--pr {
  color: var(--accent-strong);
}

.persons-section {
  margin-bottom: 14px;
}

.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 5px 6px 5px 12px;

  background: var(--surface-hover);
  border: 1px solid var(--border);
  border-radius: var(--radius-pill);
  color: var(--text);
  font-size: 0.82rem;
}

.chip button {
  display: grid;
  place-items: center;
  width: 18px;
  height: 18px;

  background: transparent;
  border: none;
  border-radius: 50%;
  color: var(--text-faint);
  cursor: pointer;
  font-size: 0.95rem;
  line-height: 1;
}

.chip button:hover {
  color: var(--text);
  background: var(--surface-solid);
}

.chip-input {
  padding: 6px 10px;
  background: transparent;
  border: 1px dashed var(--border);
  border-radius: var(--radius-pill);
  color: var(--text);
  font-size: 0.82rem;
  min-width: 170px;
}

.chip-input:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

/* --- exercises --- */

.exercises-section {
  margin-top: 8px;
}

.exercises-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
  border-top: 1px solid var(--border);
  padding-top: 16px;
}

.add-exercise-buttons {
  display: flex;
  gap: 8px;
}

.ghost-button {
  padding: 6px 12px;

  background: transparent;
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text-dim);
  font-family: var(--font-body);
  font-size: 0.8rem;
  cursor: pointer;
}

.ghost-button:hover {
  border-color: var(--accent-border);
  color: var(--text);
}

.ghost-button:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.exercise-cards {
  display: flex;
  flex-direction: column;
  gap: var(--card-gap-sm);

  margin: 12px 0 0;
  padding: 0;
  list-style: none;
}

.exercise-card {
  display: flex;
  align-items: stretch;
  gap: 6px;
}

.exercise-card__main {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: flex-start;
  gap: 12px;

  padding: var(--card-pad-sm);

  background: var(--surface-hover);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text);
  font-family: var(--font-body);
  text-align: left;
  cursor: pointer;

  transition:
    border-color 150ms var(--ease),
    background 150ms var(--ease);
}

.exercise-card__main:hover {
  border-color: var(--accent-border);
  background: var(--glass);
}

.exercise-card__main:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.exercise-card__index {
  flex: none;
  display: grid;
  place-items: center;
  width: 24px;
  height: 24px;

  border: 1px solid var(--border);
  border-radius: var(--radius-xs);
  color: var(--text-faint);
  font-size: 0.72rem;
  font-variant-numeric: tabular-nums;
}

.exercise-card__body {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.exercise-card__title {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.exercise-card__name {
  font-size: 0.92rem;
  font-weight: 600;
  overflow-wrap: anywhere;
}

.exercise-card__flag {
  padding: 1px 6px;

  border: 1px solid var(--warning-border);
  border-radius: var(--radius-xs);
  color: var(--warning);
  font-size: 0.66rem;
}

.exercise-card__meta {
  color: var(--text-faint);
  font-size: 0.76rem;
}

.exercise-card__kind {
  color: var(--accent-text);
}

.exercise-card__kind--cardio {
  color: var(--success);
}

.exercise-card__sets {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 2px;
}

.set-chip {
  padding: 2px 6px;

  border: 1px solid var(--border);
  border-radius: var(--radius-xs);
  background: var(--glass-soft);
  color: var(--text-dim);
  font-size: 0.72rem;
  font-variant-numeric: tabular-nums;
}

.set-chip--warmup {
  border-style: dashed;
  color: var(--text-faint);
}

.set-chip--more {
  border-color: transparent;
  background: transparent;
  color: var(--text-faint);
}

.exercise-card__notes {
  color: var(--text-dim);
  font-size: 0.76rem;
  font-style: italic;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.exercise-card__edit {
  flex: none;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  align-self: center;

  color: var(--text-faint);
  font-size: 0.76rem;

  transition: color 150ms var(--ease);
}

.exercise-card__main:hover .exercise-card__edit,
.exercise-card__main:focus-visible .exercise-card__edit {
  color: var(--accent-text);
}

.remove-button {
  flex: none;
  width: 32px;
  display: grid;
  place-items: center;

  background: transparent;
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text-faint);
  cursor: pointer;
  font-size: 1rem;
  line-height: 1;

  transition:
    color 150ms var(--ease),
    border-color 150ms var(--ease),
    background 150ms var(--ease);
}

.remove-button:hover {
  color: var(--danger);
  border-color: var(--danger-border);
  background: var(--danger-bg);
}

.remove-button:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

/* --- footer --- */

.detail-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 10px;

  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--border);

  position: sticky;
  bottom: -20px;
  background: var(--surface-solid);
}

.dirty-indicator {
  font-size: 0.8rem;
  color: var(--accent-strong);
}

.dirty-indicator::before {
  content: "";
  display: inline-block;
  width: 6px;
  height: 6px;
  margin-right: 6px;
  border-radius: 50%;
  background: var(--accent);
}

.dirty-indicator--muted {
  color: var(--text-faint);
}

.dirty-indicator--muted::before {
  background: var(--text-faint);
}

.status-text--inline {
  margin: 0;
}

.footer-actions {
  display: flex;
  gap: 10px;
}

.text-button {
  background: none;
  border: none;
  color: var(--text-faint);
  font-size: 0.85rem;
  cursor: pointer;
  padding: 8px 4px;
  font-family: var(--font-body);
}

.text-button:hover {
  color: var(--text-dim);
}

.text-button:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 2px;
}

.save-button {
  padding: 9px 18px;

  background: var(--accent);
  border: 1px solid var(--accent-border);
  border-radius: var(--radius-md);
  color: var(--on-accent);
  font-family: var(--font-body);
  font-weight: 600;
  font-size: 0.85rem;
  cursor: pointer;
}

.save-button:hover:not(:disabled) {
  background: var(--accent-strong);
}

.save-button:disabled {
  background: var(--surface-hover);
  border-color: var(--border);
  color: var(--text-faint);
  cursor: default;
}

.save-button:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 2px;
}

/* --- shared status text --- */

.status-text {
  color: var(--text-faint);
  font-size: 0.85rem;
}

.status-text--error {
  color: var(--text-dim);
}

/* --- motion --- */

@keyframes rise {
  from {
    opacity: 0;
    transform: translateY(8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@media (prefers-reduced-motion: reduce) {
  .page-header,
  .list-panel,
  .detail-panel,
  .workout-rows li {
    animation: none;
  }
}

/* --- responsive --- */

@media (max-width: 900px) {
  .layout {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 560px) {
  .list-panel--hidden-mobile {
    display: none;
  }

  .exercise-card__edit {
    display: none;
  }

  .detail-header__fields {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
