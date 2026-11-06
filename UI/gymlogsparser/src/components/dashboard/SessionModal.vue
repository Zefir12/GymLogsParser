<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from "vue";

import ExerciseEditModal from "@/components/ExerciseEditModal.vue";
import { getWorkout, updateWorkout } from "@/api/client";
import type { CardioEntry, WorkoutExercise, WorkoutLog } from "@/types/workout";
import { dayNumber, formatDay, formatKg, formatNumber } from "@/utils/dashboard";
import { formatDuration } from "@/utils/format";

/**
 * One exercise as it was done in one session, opened from a point on a dashboard
 * chart. Read-only at first; Edit swaps it for ExerciseEditModal (#29), and saving
 * writes the whole workout back through PUT /api/workouts/{id}. The parent reloads
 * the dashboard on `saved`. Mount it with v-if while it is open.
 */
const props = defineProps<{
  exerciseId: string;
  /** Shown while the workouts load. */
  exerciseName: string;
  /** Workouts the exercise was logged in that day (from the dashboard entry). */
  workoutIds: string[];
}>();

const emit = defineEmits<{
  (e: "close"): void;
  (e: "saved"): void;
}>();

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

// ---------------------------------------------------------------------------
// Data
// ---------------------------------------------------------------------------

interface Occurrence {
  workoutId: string;
  /** Position in workout.exercises. Fixed at load, so a save that changes the
   *  exercise (e.g. picks another one from the catalog) still shows the result. */
  index: number;
}

const workouts = ref<Map<string, WorkoutLog>>(new Map());
const occurrences = ref<Occurrence[]>([]);
const loading = ref(true);
const loadError = ref<string | null>(null);

async function load() {
  loading.value = true;
  loadError.value = null;
  try {
    const ids = [...new Set(props.workoutIds)];
    const loaded = await Promise.all(ids.map((id) => getWorkout(id)));
    const map = new Map<string, WorkoutLog>();
    const found: Occurrence[] = [];
    loaded
      .map((workout, i) => ({ id: ids[i]!, workout }))
      .sort((a, b) =>
        (a.workout.startTime ?? "").localeCompare(b.workout.startTime ?? ""),
      )
      .forEach(({ id, workout }) => {
        map.set(id, workout);
        workout.exercises.forEach((e, index) => {
          if (e.exerciseId === props.exerciseId) found.push({ workoutId: id, index });
        });
      });
    workouts.value = map;
    occurrences.value = found;
  } catch (err) {
    loadError.value =
      err instanceof Error ? err.message : "Could not load the workout.";
  } finally {
    loading.value = false;
  }
}

const multipleWorkouts = computed(
  () => new Set(occurrences.value.map((o) => o.workoutId)).size > 1,
);

interface OccurrenceView extends Occurrence {
  key: string;
  workout: WorkoutLog;
  exercise: WorkoutExercise;
}

const views = computed<OccurrenceView[]>(() =>
  occurrences.value.flatMap((o) => {
    const workout = workouts.value.get(o.workoutId);
    const exercise = workout?.exercises[o.index];
    return workout && exercise
      ? [{ ...o, key: `${o.workoutId}:${o.index}`, workout, exercise }]
      : [];
  }),
);

const first = computed(() => views.value[0] ?? null);

const title = computed(() => first.value?.exercise.name || props.exerciseName);

const dateLine = computed(() => {
  const w = first.value?.workout;
  if (!w?.date) return "";
  const day = formatDay(dayNumber(w.date));
  return `${day} ${w.date.slice(0, 4)}`;
});

function timeRange(w: WorkoutLog): string {
  const hm = (t: string | null) => (t ? t.slice(0, 5) : "");
  if (w.startTime && w.endTime) return `${hm(w.startTime)}–${hm(w.endTime)}`;
  return hm(w.startTime);
}

function workoutLine(w: WorkoutLog): string {
  return [w.title, timeRange(w)].filter(Boolean).join(" · ");
}

function heaviest(exercise: WorkoutExercise): number {
  return Math.max(
    0,
    ...exercise.sets.filter((s) => !s.warmup).map((s) => s.weight ?? 0),
  );
}

function cardioFigures(entry: CardioEntry): string {
  return [
    formatDuration(entry.durationSeconds),
    entry.distanceKm != null ? `${formatNumber(entry.distanceKm)} km` : "",
    entry.speedKmh != null ? `${formatNumber(entry.speedKmh)} km/h` : "",
    entry.inclinePercent != null
      ? `${formatNumber(entry.inclinePercent)}% incline`
      : "",
  ]
    .filter(Boolean)
    .join(" · ");
}

function summary(exercise: WorkoutExercise): string {
  if (exercise.category === "Cardio") {
    const seconds = exercise.cardio.reduce(
      (sum, c) => sum + (c.durationSeconds ?? 0),
      0,
    );
    const km = exercise.cardio.reduce((sum, c) => sum + (c.distanceKm ?? 0), 0);
    return [
      `${exercise.cardio.length} ${exercise.cardio.length === 1 ? "entry" : "entries"}`,
      seconds ? formatDuration(seconds) : "",
      km ? `${formatNumber(km)} km` : "",
    ]
      .filter(Boolean)
      .join(" · ");
  }
  const working = exercise.sets.filter((s) => !s.warmup);
  const warmups = exercise.sets.length - working.length;
  const volume = working.reduce(
    (sum, s) => sum + (s.weight ?? 0) * (s.reps ?? 0),
    0,
  );
  return [
    `${working.length} working set${working.length === 1 ? "" : "s"}`,
    warmups ? `${warmups} warm-up` : "",
    volume ? `${formatKg(volume)} volume` : "",
  ]
    .filter(Boolean)
    .join(" · ");
}

// ---------------------------------------------------------------------------
// Editing
// ---------------------------------------------------------------------------

const editing = ref<OccurrenceView | null>(null);
const saving = ref(false);
const saveError = ref<string | null>(null);
const savedNotice = ref(false);
/** An edit whose PUT failed, kept so it can be retried instead of lost. */
const pending = ref<{ occurrence: Occurrence; exercise: WorkoutExercise } | null>(
  null,
);
let lastEditButton: HTMLElement | null = null;

function startEdit(view: OccurrenceView, e: Event) {
  // A failed save is retried or discarded first (see the banner), so it is
  // never silently replaced by a second edit.
  if (saving.value || pending.value) return;
  lastEditButton = e.currentTarget as HTMLElement;
  saveError.value = null;
  savedNotice.value = false;
  editing.value = view;
}

function backFromEditor() {
  editing.value = null;
  // ExerciseEditModal hands focus back while this dialog is still hidden, so
  // move it once the dialog is visible again.
  nextTick(() => (lastEditButton ?? dialogEl.value)?.focus());
}

function onEditSaved(exercise: WorkoutExercise) {
  const view = editing.value;
  backFromEditor();
  if (!view) return;
  pending.value = {
    occurrence: { workoutId: view.workoutId, index: view.index },
    exercise,
  };
  persist();
}

async function persist() {
  const p = pending.value;
  const workout = p && workouts.value.get(p.occurrence.workoutId);
  if (!p || !workout) return;

  saving.value = true;
  saveError.value = null;
  savedNotice.value = false;
  try {
    const updated = clone(workout);
    updated.exercises[p.occurrence.index] = p.exercise;
    await updateWorkout(p.occurrence.workoutId, updated);
    const next = new Map(workouts.value);
    next.set(p.occurrence.workoutId, updated);
    workouts.value = next;
    pending.value = null;
    savedNotice.value = true;
    emit("saved");
  } catch (err) {
    saveError.value =
      err instanceof Error ? err.message : "Could not save the workout.";
  } finally {
    saving.value = false;
  }
}

function discardPending() {
  pending.value = null;
  saveError.value = null;
}

// ---------------------------------------------------------------------------
// Dialog: scroll lock, focus trap, focus restore, keyboard
// ---------------------------------------------------------------------------

const dialogEl = ref<HTMLElement | null>(null);
const titleId = `session-title-${Math.random().toString(36).slice(2, 9)}`;

let previousFocus: HTMLElement | null = null;
let previousOverflow = "";

onMounted(async () => {
  previousFocus = document.activeElement as HTMLElement | null;
  previousOverflow = document.body.style.overflow;
  document.body.style.overflow = "hidden";
  await nextTick();
  dialogEl.value?.focus();
  load();
});

onBeforeUnmount(() => {
  document.body.style.overflow = previousOverflow;
  if (previousFocus?.isConnected) previousFocus.focus();
});

function requestClose() {
  // A save in flight would otherwise finish with nobody left to reload the charts.
  if (saving.value) return;
  if (
    pending.value &&
    !window.confirm("Your change was not saved. Close and discard it?")
  ) {
    return;
  }
  emit("close");
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === "Escape") {
    e.preventDefault();
    e.stopPropagation();
    requestClose();
    return;
  }
  if (e.key !== "Tab" || !dialogEl.value) return;

  const items = Array.from(
    dialogEl.value.querySelectorAll<HTMLElement>(
      'button:not([disabled]), [href], [tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => el.offsetParent !== null);
  if (items.length === 0) {
    e.preventDefault();
    return;
  }
  const firstItem = items[0]!;
  const lastItem = items[items.length - 1]!;
  const active = document.activeElement;
  if (e.shiftKey && (active === firstItem || active === dialogEl.value)) {
    e.preventDefault();
    lastItem.focus();
  } else if (!e.shiftKey && active === lastItem) {
    e.preventDefault();
    firstItem.focus();
  }
}

let backdropPointerDown = false;

function onBackdropPointerDown(e: PointerEvent) {
  backdropPointerDown = e.target === e.currentTarget;
}

function onBackdropClick(e: MouseEvent) {
  if (backdropPointerDown && e.target === e.currentTarget) requestClose();
  backdropPointerDown = false;
}
</script>

<template>
  <Teleport to="body">
    <div
      v-show="!editing"
      class="modal-backdrop"
      @pointerdown="onBackdropPointerDown"
      @click="onBackdropClick"
    >
      <div
        ref="dialogEl"
        class="modal"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="titleId"
        tabindex="-1"
        @keydown="onKeydown"
      >
        <header class="modal__header">
          <div class="modal__heading">
            <span class="modal__eyebrow">{{ dateLine || "Session" }}</span>
            <h2 :id="titleId" class="modal__title">{{ title }}</h2>
          </div>
          <button
            type="button"
            class="icon-button"
            aria-label="Close"
            :disabled="saving"
            @click="requestClose"
          >
            <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
              <path
                d="M4 4l8 8M12 4l-8 8"
                stroke="currentColor"
                stroke-width="1.6"
                stroke-linecap="round"
              />
            </svg>
          </button>
        </header>

        <div class="modal__body">
          <p v-if="loading" class="status">Loading the workout…</p>

          <div v-else-if="loadError" class="status status--error" role="alert">
            <span>{{ loadError }}</span>
            <button type="button" class="ghost-button" @click="load">
              Try again
            </button>
          </div>

          <p v-else-if="views.length === 0" class="status">
            This exercise is no longer in that workout. It may have been edited
            elsewhere; the dashboard will catch up on the next reload.
          </p>

          <template v-else>
            <div
              v-if="saving || saveError || savedNotice"
              class="banner"
              :class="{
                'banner--error': saveError,
                'banner--ok': savedNotice && !saveError,
              }"
              :role="saveError ? 'alert' : 'status'"
            >
              <span v-if="saving">Saving…</span>
              <template v-else-if="saveError">
                <span>Not saved: {{ saveError }}</span>
                <span class="banner__actions">
                  <button type="button" class="ghost-button" @click="persist">
                    Try again
                  </button>
                  <button
                    type="button"
                    class="text-button"
                    @click="discardPending"
                  >
                    Discard
                  </button>
                </span>
              </template>
              <span v-else>Saved. The dashboard is reloading with the change.</span>
            </div>

            <section v-for="view in views" :key="view.key" class="occurrence">
              <div class="occurrence__head">
                <div class="occurrence__meta">
                  <span
                    v-if="multipleWorkouts || workoutLine(view.workout)"
                    class="occurrence__workout"
                    >{{ workoutLine(view.workout) || "Workout" }}</span
                  >
                  <span class="occurrence__summary">
                    <span
                      class="badge"
                      :class="{
                        'badge--cardio': view.exercise.category === 'Cardio',
                      }"
                      >{{ view.exercise.category }}</span
                    >
                    <span v-if="view.exercise.muscleGroup">{{
                      view.exercise.muscleGroup
                    }}</span>
                    <span>{{ summary(view.exercise) }}</span>
                  </span>
                </div>
                <button
                  type="button"
                  class="primary-button"
                  :disabled="saving || !!pending"
                  @click="startEdit(view, $event)"
                >
                  Edit
                </button>
              </div>

              <!-- strength -->
              <template v-if="view.exercise.category === 'Strength'">
                <table v-if="view.exercise.sets.length" class="sets">
                  <thead>
                    <tr>
                      <th scope="col" class="num">#</th>
                      <th scope="col">Weight × reps</th>
                      <th scope="col" class="opt">RIR</th>
                      <th scope="col" class="opt">RPE</th>
                      <th scope="col" class="notes">Notes</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="(set, i) in view.exercise.sets"
                      :key="i"
                      :class="{ warmup: set.warmup }"
                    >
                      <td class="num">{{ set.setNumber ?? i + 1 }}</td>
                      <td class="lift">
                        <span
                          >{{ set.weight != null ? formatKg(set.weight) : "–" }}
                          × {{ set.reps ?? "–" }}</span
                        >
                        <span
                          v-if="
                            !set.warmup &&
                            set.weight &&
                            set.weight === heaviest(view.exercise)
                          "
                          class="crown"
                          title="Heaviest working set"
                          >♛</span
                        >
                        <span v-if="set.warmup" class="tag">warm-up</span>
                      </td>
                      <td class="opt">{{ set.rir ?? "" }}</td>
                      <td class="opt">{{ set.rpe ?? "" }}</td>
                      <td class="notes">{{ set.notes ?? "" }}</td>
                    </tr>
                  </tbody>
                </table>
                <p v-else class="empty-line">No sets logged.</p>
              </template>

              <!-- cardio -->
              <template v-else>
                <ul v-if="view.exercise.cardio.length" class="cardio">
                  <li v-for="(entry, i) in view.exercise.cardio" :key="i">
                    <span class="cardio__name">{{
                      entry.activity || view.exercise.name
                    }}</span>
                    <span class="cardio__figures">{{
                      cardioFigures(entry)
                    }}</span>
                    <span v-if="entry.notes" class="cardio__notes">{{
                      entry.notes
                    }}</span>
                  </li>
                </ul>
                <p v-else class="empty-line">No entries logged.</p>
              </template>

              <p v-if="view.exercise.notes" class="exercise-notes">
                {{ view.exercise.notes }}
              </p>
            </section>
          </template>
        </div>
      </div>
    </div>

    <ExerciseEditModal
      v-if="editing"
      :exercise="editing.exercise"
      @save="onEditSaved"
      @cancel="backFromEditor"
    />
  </Teleport>
</template>

<style scoped>
.modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 900;

  display: grid;
  place-items: center;
  padding: 24px 16px;

  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);

  animation: fade 140ms var(--ease) both;
}

.modal {
  display: flex;
  flex-direction: column;

  width: min(620px, 100%);
  max-height: min(760px, calc(100dvh - 48px));

  background: var(--surface-solid);
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-card);
  box-shadow: var(--shadow-glass);

  color: var(--text);
  font-family: var(--font-body);
  font-size: 14px;

  outline: none;

  animation: pop 180ms var(--ease) both;
}

.modal__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;

  padding: var(--card-pad) var(--card-pad) var(--card-pad-sm);
  border-bottom: 1px solid var(--border);
}

.modal__heading {
  min-width: 0;
}

.modal__eyebrow {
  display: block;
  margin-bottom: 4px;

  color: var(--text-faint);
  font-size: 0.68rem;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.modal__title {
  margin: 0;

  font-family: var(--font-display);
  font-size: 1.15rem;
  font-weight: 600;
  line-height: 1.25;

  overflow-wrap: anywhere;
}

.modal__body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overscroll-behavior: contain;

  display: flex;
  flex-direction: column;
  gap: var(--card-gap-md);
  padding: var(--card-pad-sm) var(--card-pad) var(--card-pad);
}

/* --- states --- */

.status {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin: 0;
  padding: 12px 0;
  color: var(--text-dim);
}

.status--error {
  color: var(--danger);
}

.banner {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 8px 10px;

  border: 1px solid var(--border);
  border-radius: var(--radius-control);
  background: var(--surface-hover);
  color: var(--text-dim);
  font-size: 0.82rem;
}

.banner--ok {
  border-color: var(--success-border);
  background: var(--success-bg);
  color: var(--success);
}

.banner--error {
  border-color: var(--danger-border);
  background: var(--danger-bg);
  color: var(--danger);
}

.banner__actions {
  display: flex;
  align-items: center;
  gap: 6px;
}

/* --- one exercise in one workout --- */

.occurrence {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.occurrence + .occurrence {
  padding-top: var(--card-pad-sm);
  border-top: 1px solid var(--border);
}

.occurrence__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.occurrence__meta {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.occurrence__workout {
  color: var(--text);
  font-weight: 600;
  overflow-wrap: anywhere;
}

.occurrence__summary {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px 8px;
  color: var(--text-dim);
  font-size: 0.82rem;
}

.badge {
  display: inline-flex;
  align-items: center;
  height: 20px;
  padding: 0 7px;

  border: 1px solid var(--accent-border);
  border-radius: var(--radius-xs);
  background: var(--accent-soft);
  color: var(--accent-text);

  font-size: 0.66rem;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.badge--cardio {
  border-color: var(--success-border);
  background: var(--success-bg);
  color: var(--success);
}

/* --- sets table --- */

.sets {
  width: 100%;
  border-collapse: collapse;
  font-variant-numeric: tabular-nums;
}

.sets th {
  padding: 0 8px 6px;
  color: var(--text-faint);
  font-size: 0.72rem;
  font-weight: 500;
  text-align: left;
}

.sets td {
  padding: 7px 8px;
  border-top: 1px solid var(--border);
  vertical-align: top;
}

.sets .num {
  width: 28px;
  padding-left: 0;
  color: var(--text-faint);
}

.sets .opt {
  width: 44px;
  color: var(--text-dim);
}

.sets .notes {
  color: var(--text-dim);
  overflow-wrap: anywhere;
}

.lift {
  white-space: nowrap;
}

.warmup td {
  color: var(--text-faint);
}

.crown {
  margin-left: 6px;
  color: var(--accent-text);
}

.tag {
  margin-left: 6px;
  padding: 0 5px;

  border: 1px dashed var(--border-strong);
  border-radius: var(--radius-xs);
  font-size: 0.66rem;
}

/* --- cardio --- */

.cardio {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.cardio li {
  display: flex;
  flex-wrap: wrap;
  gap: 2px 10px;
  padding: 7px 0;
  border-top: 1px solid var(--border);
}

.cardio__name {
  font-weight: 500;
}

.cardio__figures,
.cardio__notes {
  color: var(--text-dim);
}

.cardio__notes {
  flex-basis: 100%;
  font-size: 0.82rem;
}

.exercise-notes {
  margin: 0;
  padding: 8px 10px;

  border-left: 2px solid var(--border-strong);
  color: var(--text-dim);
  font-size: 0.85rem;
  white-space: pre-line;
}

.empty-line {
  margin: 0;
  color: var(--text-faint);
  font-size: 0.82rem;
}

/* --- buttons --- */

.icon-button {
  flex: none;
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;

  background: transparent;
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text-faint);
}

.icon-button:hover:not(:disabled) {
  color: var(--text);
  border-color: var(--border-strong);
}

.ghost-button {
  height: 30px;
  padding: 0 12px;

  background: transparent;
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text-dim);
  font-size: 0.8rem;
}

.ghost-button:hover {
  border-color: var(--accent-border);
  color: var(--text);
}

.text-button {
  padding: 6px;

  background: none;
  border: none;
  color: var(--text-faint);
  font-size: 0.8rem;
}

.text-button:hover {
  color: var(--text);
}

.primary-button {
  flex: none;
  min-height: 36px;
  min-width: 72px;
  padding: 0 18px;

  background: var(--accent);
  border: 1px solid var(--accent-border);
  border-radius: var(--radius-md);
  color: var(--on-accent);
  font-weight: 600;
  font-size: 0.85rem;
}

.primary-button:hover:not(:disabled) {
  background: var(--accent-strong);
}

.primary-button:disabled {
  background: var(--surface-hover);
  border-color: var(--border);
  color: var(--text-faint);
}

.icon-button:focus-visible,
.ghost-button:focus-visible,
.text-button:focus-visible,
.primary-button:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 2px;
}

/* --- motion --- */

@keyframes fade {
  from {
    opacity: 0;
  }
}

@keyframes pop {
  from {
    opacity: 0;
    transform: translateY(8px) scale(0.985);
  }
}

@media (prefers-reduced-motion: reduce) {
  .modal-backdrop,
  .modal {
    animation: none;
  }
}

/* --- phones: bottom sheet, bigger tap targets --- */

@media (max-width: 560px) {
  .modal-backdrop {
    padding: 0;
    place-items: end stretch;
  }

  .modal {
    width: 100%;
    max-height: 88dvh;
    border-radius: var(--radius-card) var(--radius-card) 0 0;
    border-left: 0;
    border-right: 0;
    border-bottom: 0;
    padding-bottom: env(safe-area-inset-bottom);
  }

  .icon-button {
    width: 40px;
    height: 40px;
  }

  .primary-button {
    min-height: 42px;
  }

  .sets .opt {
    width: 36px;
  }
}
</style>
