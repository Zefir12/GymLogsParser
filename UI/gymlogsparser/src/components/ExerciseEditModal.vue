<script setup lang="ts">
import {
  computed,
  nextTick,
  onBeforeUnmount,
  onMounted,
  ref,
  watch,
} from "vue";

import { useExerciseCatalogStore } from "@/stores/exercises";
import type { ExerciseDefinition } from "@/types/exercise";
import { muscleGroupOptions } from "@/utils/muscleGroups";
import type {
  CardioEntry,
  ExerciseCategory,
  WorkoutExercise,
  WorkoutSet,
} from "@/types/workout";

/**
 * Edits one exercise of a saved workout in a dialog. Works on a copy: nothing
 * reaches the workout until the user saves, and Cancel/Esc throws the copy away.
 * Mount it with v-if while it is open; it locks page scroll, traps focus and
 * hands focus back to whatever opened it when it closes.
 */
const props = defineProps<{
  exercise: WorkoutExercise;
  /** True when the exercise is not in the workout yet (Add exercise). */
  isNew?: boolean;
}>();

const emit = defineEmits<{
  (e: "save", exercise: WorkoutExercise): void;
  (e: "cancel"): void;
}>();

const catalog = useExerciseCatalogStore();

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

const draft = ref<WorkoutExercise>(clone(props.exercise));
const initialSnapshot = JSON.stringify(draft.value);

const isDirty = computed(
  () => props.isNew || JSON.stringify(draft.value) !== initialSnapshot,
);

const selectedDefinition = computed(() => catalog.byId(draft.value.exerciseId));

const muscleGroupChoices = computed(() => muscleGroupOptions(draft.value.muscleGroup));

/** A stored exercise whose ID is not (or no longer) in the catalog. */
const isOffCatalog = computed(
  () =>
    !catalog.loading &&
    catalog.definitions.length > 0 &&
    !!draft.value.exerciseId &&
    !selectedDefinition.value,
);

const canSave = computed(() => {
  if (!draft.value.name.trim()) return false;
  // A new exercise must come from the catalog so the dashboard and progress
  // views can group it with the same exercise in other workouts.
  if (props.isNew) return !!selectedDefinition.value;
  return true;
});

// ---------------------------------------------------------------------------
// Dialog: scroll lock, focus trap, focus restore, keyboard
// ---------------------------------------------------------------------------

const dialogEl = ref<HTMLElement | null>(null);
const searchEl = ref<HTMLInputElement | null>(null);
const changeButtonEl = ref<HTMLButtonElement | null>(null);

let previousFocus: HTMLElement | null = null;
let previousOverflow = "";

const coarsePointer =
  typeof window !== "undefined" &&
  window.matchMedia?.("(pointer: coarse)").matches;

onMounted(async () => {
  previousFocus = document.activeElement as HTMLElement | null;
  previousOverflow = document.body.style.overflow;
  document.body.style.overflow = "hidden";

  catalog.ensureLoaded();

  await nextTick();
  focusInitial();
});

onBeforeUnmount(() => {
  document.body.style.overflow = previousOverflow;
  if (previousFocus?.isConnected) previousFocus.focus();
});

function focusInitial() {
  // On touch devices focusing a field pops the keyboard over half the dialog,
  // so only move focus into the dialog itself there.
  if (coarsePointer && !pickerOpen.value) {
    dialogEl.value?.focus();
    return;
  }
  if (pickerOpen.value) {
    searchEl.value?.focus();
    return;
  }
  focusFirstEntryField() || dialogEl.value?.focus();
}

function focusFirstEntryField(): boolean {
  const field = dialogEl.value?.querySelector<HTMLElement>(
    "[data-entry-field]",
  );
  field?.focus();
  return !!field;
}

function focusables(): HTMLElement[] {
  if (!dialogEl.value) return [];
  return Array.from(
    dialogEl.value.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => el.offsetParent !== null);
}

function trapTab(e: KeyboardEvent) {
  const items = focusables();
  if (items.length === 0) {
    e.preventDefault();
    return;
  }
  const first = items[0]!;
  const last = items[items.length - 1]!;
  const active = document.activeElement;

  if (e.shiftKey && (active === first || active === dialogEl.value)) {
    e.preventDefault();
    last.focus();
  } else if (!e.shiftKey && active === last) {
    e.preventDefault();
    first.focus();
  }
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === "Tab") {
    trapTab(e);
    return;
  }

  if (e.key === "Escape") {
    e.preventDefault();
    e.stopPropagation();
    if (pickerOpen.value && draft.value.exerciseId) {
      closePicker();
      return;
    }
    requestCancel();
    return;
  }

  if (e.key !== "Enter" || e.isComposing) return;

  const target = e.target as HTMLElement;
  const modifier = e.ctrlKey || e.metaKey;

  // Ctrl/Cmd+Enter saves from anywhere, textareas included.
  if (modifier) {
    e.preventDefault();
    requestSave();
    return;
  }

  // Plain Enter saves from a text or number field. Buttons keep their own
  // Enter behaviour and textareas keep their new lines.
  if (target instanceof HTMLInputElement && target !== searchEl.value) {
    e.preventDefault();
    requestSave();
  }
}

let backdropPointerDown = false;

function onBackdropPointerDown(e: PointerEvent) {
  backdropPointerDown = e.target === e.currentTarget;
}

function onBackdropClick(e: MouseEvent) {
  // Only a press that started and ended on the backdrop closes the dialog, so
  // a text selection dragged out of a field doesn't throw the edits away.
  if (backdropPointerDown && e.target === e.currentTarget) requestCancel();
  backdropPointerDown = false;
}

function requestCancel() {
  if (
    isDirty.value &&
    !props.isNew &&
    !window.confirm("Odrzucić zmiany w tym ćwiczeniu?")
  ) {
    return;
  }
  emit("cancel");
}

const attemptedSave = ref(false);

function requestSave() {
  attemptedSave.value = true;
  if (!canSave.value) {
    if (!selectedDefinition.value && props.isNew) openPicker();
    return;
  }
  const result = clone(draft.value);
  result.name = result.name.trim();
  result.muscleGroup = result.muscleGroup?.trim() || null;
  result.notes = result.notes?.trim() || null;
  emit("save", result);
}

// ---------------------------------------------------------------------------
// Exercise picker
// ---------------------------------------------------------------------------

type CategoryFilter = "All" | ExerciseCategory;

const categoryFilters: CategoryFilter[] = ["All", "Strength", "Cardio"];

const pickerOpen = ref(props.isNew || !props.exercise.exerciseId);
const query = ref("");
const categoryFilter = ref<CategoryFilter>(
  props.isNew ? props.exercise.category : "All",
);
const highlighted = ref(0);
const listboxId = `exercise-options-${Math.random().toString(36).slice(2, 9)}`;

interface PickerOption {
  definition: ExerciseDefinition;
  /** The alias that matched the query, when the name itself did not. */
  matchedAlias: string | null;
  rank: number;
}

function normalize(value: string) {
  return value
    .toLowerCase()
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/ł/g, "l")
    .trim();
}

const options = computed<PickerOption[]>(() => {
  const q = normalize(query.value);

  const result: PickerOption[] = [];

  for (const definition of catalog.definitions) {
    if (
      categoryFilter.value !== "All" &&
      definition.category !== categoryFilter.value
    ) {
      continue;
    }

    if (!q) {
      result.push({ definition, matchedAlias: null, rank: 0 });
      continue;
    }

    const name = normalize(definition.name);
    if (name.startsWith(q)) {
      result.push({ definition, matchedAlias: null, rank: 0 });
      continue;
    }
    if (name.includes(q)) {
      result.push({ definition, matchedAlias: null, rank: 1 });
      continue;
    }

    const alias = definition.aliases.find((a) => normalize(a).includes(q));
    if (alias) {
      result.push({ definition, matchedAlias: alias, rank: 2 });
      continue;
    }

    if (
      normalize(definition.muscleGroup).includes(q) ||
      normalize(definition.id).includes(q)
    ) {
      result.push({ definition, matchedAlias: null, rank: 3 });
    }
  }

  // Stable sort keeps the catalog order (muscle group, then name) within a rank.
  return result.sort((a, b) => a.rank - b.rank);
});

watch([query, categoryFilter], () => {
  highlighted.value = 0;
});

watch(highlighted, async () => {
  await nextTick();
  document
    .getElementById(activeOptionId.value ?? "")
    ?.scrollIntoView({ block: "nearest" });
});

const activeOptionId = computed(() => {
  const option = options.value[highlighted.value];
  return option ? optionId(option.definition.id) : undefined;
});

function optionId(id: string) {
  return `${listboxId}-${id}`;
}

async function openPicker() {
  pickerOpen.value = true;
  query.value = "";
  const current = options.value.findIndex(
    (o) => o.definition.id === draft.value.exerciseId,
  );
  highlighted.value = Math.max(0, current);
  await nextTick();
  searchEl.value?.focus();
}

async function closePicker() {
  pickerOpen.value = false;
  query.value = "";
  await nextTick();
  changeButtonEl.value?.focus();
}

function onSearchKeydown(e: KeyboardEvent) {
  const count = options.value.length;

  switch (e.key) {
    case "ArrowDown":
      e.preventDefault();
      if (count) highlighted.value = (highlighted.value + 1) % count;
      break;
    case "ArrowUp":
      e.preventDefault();
      if (count) highlighted.value = (highlighted.value - 1 + count) % count;
      break;
    case "PageDown":
      e.preventDefault();
      if (count) highlighted.value = Math.min(count - 1, highlighted.value + 8);
      break;
    case "PageUp":
      e.preventDefault();
      if (count) highlighted.value = Math.max(0, highlighted.value - 8);
      break;
    case "Enter": {
      if (e.isComposing || e.ctrlKey || e.metaKey) break;
      e.preventDefault();
      e.stopPropagation();
      const option = options.value[highlighted.value];
      if (option) selectDefinition(option.definition);
      break;
    }
  }
}

// Strength sets and cardio entries set aside when the user switches the
// exercise to the other category, so switching back brings them back.
const stashedSets = ref<WorkoutSet[]>([]);
const stashedCardio = ref<CardioEntry[]>([]);

async function selectDefinition(definition: ExerciseDefinition) {
  const d = draft.value;
  const previousCategory = d.category;

  d.exerciseId = definition.id;
  d.name = definition.name;
  d.muscleGroup = definition.muscleGroup;
  d.category = definition.category;

  if (definition.category !== previousCategory) {
    if (definition.category === "Cardio") {
      if (d.sets.length) stashedSets.value = d.sets;
      d.sets = [];
      d.cardio = stashedCardio.value.length
        ? stashedCardio.value
        : [emptyCardio()];
      stashedCardio.value = [];
    } else {
      if (d.cardio.length) stashedCardio.value = d.cardio;
      d.cardio = [];
      d.sets = stashedSets.value.length ? stashedSets.value : [emptySet(1)];
      stashedSets.value = [];
    }
  }

  pickerOpen.value = false;
  query.value = "";

  await nextTick();
  if (coarsePointer) changeButtonEl.value?.focus();
  else focusFirstEntryField() || changeButtonEl.value?.focus();
}

const droppedNotice = computed(() => {
  if (draft.value.category === "Cardio" && stashedSets.value.length) {
    return `${stashedSets.value.length} ${plural(stashedSets.value.length, "seria siłowa zostanie usunięta", "serie siłowe zostaną usunięte", "serii siłowych zostanie usuniętych")} przy zapisie. Wybierz z powrotem ćwiczenie siłowe, aby je przywrócić.`;
  }
  if (draft.value.category === "Strength" && stashedCardio.value.length) {
    return `${stashedCardio.value.length} ${plural(stashedCardio.value.length, "wpis cardio zostanie usunięty", "wpisy cardio zostaną usunięte", "wpisów cardio zostanie usuniętych")} przy zapisie. Wybierz z powrotem ćwiczenie cardio, aby je przywrócić.`;
  }
  return null;
});

// ---------------------------------------------------------------------------
// Sets and cardio entries
// ---------------------------------------------------------------------------

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

const setsEl = ref<HTMLElement | null>(null);
const cardioEl = ref<HTMLElement | null>(null);

async function focusLastRow(container: HTMLElement | null) {
  await nextTick();
  if (coarsePointer) return;
  const fields = container?.querySelectorAll<HTMLElement>("[data-row-first]");
  fields?.[fields.length - 1]?.focus();
}

function renumberSets() {
  draft.value.sets.forEach((s, i) => (s.setNumber = i + 1));
}

function addSet() {
  draft.value.sets.push(emptySet(draft.value.sets.length + 1));
  focusLastRow(setsEl.value);
}

function duplicateLastSet() {
  const last = draft.value.sets[draft.value.sets.length - 1];
  if (!last) return addSet();
  draft.value.sets.push({
    ...clone(last),
    setNumber: draft.value.sets.length + 1,
    notes: null,
  });
  focusLastRow(setsEl.value);
}

function removeSet(index: number) {
  draft.value.sets.splice(index, 1);
  renumberSets();
}

function addCardio() {
  draft.value.cardio.push(emptyCardio());
  focusLastRow(cardioEl.value);
}

function removeCardio(index: number) {
  draft.value.cardio.splice(index, 1);
}

function isTopSet(index: number): boolean {
  const weights = draft.value.sets.map((s) =>
    typeof s.weight === "number" ? s.weight : -Infinity,
  );
  const max = Math.max(...weights);
  return max > 0 && weights.indexOf(max) === index;
}

// ---------------------------------------------------------------------------
// Display helpers
// ---------------------------------------------------------------------------

function plural(n: number, one: string, few: string, many: string): string {
  if (n === 1) return one;
  const lastDigit = n % 10;
  const lastTwo = n % 100;
  if (lastDigit >= 2 && lastDigit <= 4 && !(lastTwo >= 12 && lastTwo <= 14))
    return few;
  return many;
}

const categoryLabel: Record<ExerciseCategory, string> = {
  Strength: "Siłowe",
  Cardio: "Cardio",
};

const titleId = `exercise-dialog-title-${Math.random().toString(36).slice(2, 9)}`;
</script>

<template>
  <Teleport to="body">
    <div
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
            <span class="modal__eyebrow">{{
              isNew ? "Nowe ćwiczenie" : "Edycja ćwiczenia"
            }}</span>
            <h2 :id="titleId" class="modal__title">
              {{ draft.name || "Wybierz ćwiczenie" }}
            </h2>
          </div>
          <button
            type="button"
            class="icon-button"
            aria-label="Zamknij bez zapisywania"
            @click="requestCancel"
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
          <!-- which exercise -->
          <section class="block">
            <div class="block__label">Ćwiczenie</div>

            <div v-if="!pickerOpen" class="current">
              <div class="current__main">
                <span
                  class="badge"
                  :class="{ 'badge--cardio': draft.category === 'Cardio' }"
                  >{{ categoryLabel[draft.category] }}</span
                >
                <span class="current__name">{{ draft.name || "—" }}</span>
                <span
                  v-if="isOffCatalog"
                  class="badge badge--warn"
                  title="Tego ID nie ma w katalogu ćwiczeń. Wybierz ćwiczenie z listy, aby je powiązać."
                  >Spoza katalogu</span
                >
              </div>
              <button
                ref="changeButtonEl"
                type="button"
                class="ghost-button"
                @click="openPicker"
              >
                Zmień ćwiczenie
              </button>
            </div>

            <div v-else class="picker">
              <div class="picker__controls">
                <div class="picker__search">
                  <svg
                    class="picker__search-icon"
                    viewBox="0 0 16 16"
                    width="13"
                    height="13"
                    aria-hidden="true"
                  >
                    <circle
                      cx="7"
                      cy="7"
                      r="4.5"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="1.4"
                    />
                    <path
                      d="M10.5 10.5L14 14"
                      stroke="currentColor"
                      stroke-width="1.4"
                      stroke-linecap="round"
                    />
                  </svg>
                  <input
                    ref="searchEl"
                    v-model="query"
                    type="search"
                    role="combobox"
                    aria-autocomplete="list"
                    aria-expanded="true"
                    :aria-controls="listboxId"
                    :aria-activedescendant="activeOptionId"
                    aria-label="Szukaj ćwiczenia po nazwie, aliasie lub partii"
                    placeholder="Szukaj: nazwa, alias, partia…"
                    autocomplete="off"
                    spellcheck="false"
                    @keydown="onSearchKeydown"
                  />
                </div>

                <div class="segmented" role="group" aria-label="Typ ćwiczenia">
                  <button
                    v-for="f in categoryFilters"
                    :key="f"
                    type="button"
                    class="segmented__item"
                    :aria-pressed="categoryFilter === f"
                    @click="categoryFilter = f"
                  >
                    {{ f === "All" ? "Wszystkie" : categoryLabel[f] }}
                  </button>
                </div>
              </div>

              <ul
                :id="listboxId"
                class="picker__list"
                role="listbox"
                aria-label="Katalog ćwiczeń"
              >
                <li v-if="catalog.loading" class="picker__empty">
                  Wczytywanie katalogu…
                </li>
                <li v-else-if="catalog.error" class="picker__empty">
                  {{ catalog.error }}
                </li>
                <li v-else-if="options.length === 0" class="picker__empty">
                  Nic nie pasuje. Nowe ćwiczenia dodasz na stronie Ćwiczenia.
                </li>
                <li
                  v-for="(option, i) in options"
                  :id="optionId(option.definition.id)"
                  :key="option.definition.id"
                  class="option"
                  :class="{
                    'option--active': i === highlighted,
                    'option--selected':
                      option.definition.id === draft.exerciseId,
                  }"
                  role="option"
                  :aria-selected="option.definition.id === draft.exerciseId"
                  @mousedown.prevent
                  @mousemove="highlighted = i"
                  @click="selectDefinition(option.definition)"
                >
                  <span class="option__text">
                    <span class="option__name">{{
                      option.definition.name
                    }}</span>
                    <span class="option__meta">
                      {{ option.definition.muscleGroup }}
                      <template v-if="option.matchedAlias">
                        · alias „{{ option.matchedAlias }}”
                      </template>
                    </span>
                  </span>
                  <span
                    class="badge"
                    :class="{
                      'badge--cardio': option.definition.category === 'Cardio',
                    }"
                    >{{ categoryLabel[option.definition.category] }}</span
                  >
                </li>
              </ul>

              <div class="picker__footer">
                <span class="hint"
                  ><kbd>↑</kbd><kbd>↓</kbd> wybór · <kbd>Enter</kbd> zatwierdź
                  · <kbd>Esc</kbd>
                  {{ draft.exerciseId ? "wróć" : "zamknij" }}</span
                >
                <button
                  v-if="draft.exerciseId"
                  type="button"
                  class="text-button"
                  @click="closePicker"
                >
                  Zostaw obecne
                </button>
              </div>

              <p
                v-if="attemptedSave && isNew && !selectedDefinition"
                class="field-error"
                role="alert"
              >
                Wybierz ćwiczenie z listy.
              </p>
            </div>

            <p v-if="droppedNotice" class="notice" role="status">
              {{ droppedNotice }}
            </p>
          </section>

          <section class="block block--row">
            <label class="field field--grow">
              <span>Partia mięśniowa</span>
              <select v-model="draft.muscleGroup">
                <option :value="null">—</option>
                <option v-for="g in muscleGroupChoices" :key="g" :value="g">
                  {{ g }}
                </option>
              </select>
            </label>
          </section>

          <!-- strength sets -->
          <section
            v-if="draft.category === 'Strength'"
            ref="setsEl"
            class="block"
          >
            <div class="block__label">
              Serie
              <span class="block__count">{{ draft.sets.length }}</span>
            </div>

            <div class="sets" role="table" aria-label="Serie">
              <div class="set set--head" role="row">
                <span role="columnheader" class="set__num">#</span>
                <span role="columnheader" class="set__weight">Waga (kg)</span>
                <span role="columnheader" class="set__reps">Powt.</span>
                <span role="columnheader" class="set__rir">RIR</span>
                <span role="columnheader" class="set__rpe">RPE</span>
                <span role="columnheader" class="set__warm">Rozgrz.</span>
                <span role="columnheader" class="set__notes">Notatka</span>
                <span class="set__del" />
              </div>

              <div
                v-for="(s, sIdx) in draft.sets"
                :key="sIdx"
                class="set"
                :class="{
                  'set--top': isTopSet(sIdx),
                  'set--warmup': s.warmup === true,
                }"
                role="row"
              >
                <span class="set__num" role="cell">
                  <span
                    v-if="isTopSet(sIdx)"
                    class="crown"
                    title="Najcięższa seria"
                    >♛</span
                  >
                  {{ s.setNumber ?? sIdx + 1 }}
                </span>
                <input
                  v-model.number="s.weight"
                  class="set__weight"
                  type="number"
                  inputmode="decimal"
                  min="0"
                  step="0.5"
                  placeholder="—"
                  :aria-label="`Seria ${sIdx + 1}: waga w kg`"
                  data-entry-field
                  data-row-first
                />
                <input
                  v-model.number="s.reps"
                  class="set__reps"
                  type="number"
                  inputmode="numeric"
                  min="0"
                  step="1"
                  placeholder="—"
                  :aria-label="`Seria ${sIdx + 1}: powtórzenia`"
                />
                <input
                  v-model.number="s.rir"
                  class="set__rir"
                  type="number"
                  inputmode="decimal"
                  min="0"
                  step="0.5"
                  placeholder="—"
                  :aria-label="`Seria ${sIdx + 1}: RIR`"
                />
                <input
                  v-model.number="s.rpe"
                  class="set__rpe"
                  type="number"
                  inputmode="decimal"
                  min="0"
                  max="10"
                  step="0.5"
                  placeholder="—"
                  :aria-label="`Seria ${sIdx + 1}: RPE`"
                />
                <button
                  type="button"
                  class="set__warm toggle"
                  :aria-pressed="s.warmup === true"
                  :aria-label="`Seria ${sIdx + 1}: rozgrzewkowa`"
                  title="Seria rozgrzewkowa"
                  @click="s.warmup = s.warmup !== true"
                >
                  Rozgrz.
                </button>
                <input
                  v-model="s.notes"
                  class="set__notes"
                  type="text"
                  placeholder="Notatka…"
                  :aria-label="`Seria ${sIdx + 1}: notatka`"
                />
                <button
                  type="button"
                  class="set__del icon-button icon-button--sm icon-button--danger"
                  :aria-label="`Usuń serię ${sIdx + 1}`"
                  @click="removeSet(sIdx)"
                >
                  ×
                </button>
              </div>

              <p v-if="draft.sets.length === 0" class="empty-line">
                Brak serii.
              </p>
            </div>

            <div class="row-actions">
              <button type="button" class="ghost-button" @click="addSet">
                + Dodaj serię
              </button>
              <button
                v-if="draft.sets.length"
                type="button"
                class="ghost-button"
                title="Nowa seria z wagą i powtórzeniami ostatniej"
                @click="duplicateLastSet"
              >
                Powiel ostatnią
              </button>
            </div>
          </section>

          <!-- cardio -->
          <section v-else ref="cardioEl" class="block">
            <div class="block__label">
              Wpisy cardio
              <span class="block__count">{{ draft.cardio.length }}</span>
            </div>

            <div
              v-for="(c, cIdx) in draft.cardio"
              :key="cIdx"
              class="cardio"
            >
              <label class="field field--grow">
                <span>Aktywność</span>
                <input
                  v-model="c.activity"
                  type="text"
                  placeholder="np. Bieżnia"
                  data-entry-field
                  data-row-first
                />
              </label>
              <label class="field">
                <span>Czas (s)</span>
                <input
                  v-model.number="c.durationSeconds"
                  type="number"
                  inputmode="numeric"
                  min="0"
                />
              </label>
              <label class="field">
                <span>Dystans (km)</span>
                <input
                  v-model.number="c.distanceKm"
                  type="number"
                  inputmode="decimal"
                  min="0"
                  step="0.01"
                />
              </label>
              <label class="field">
                <span>Prędkość (km/h)</span>
                <input
                  v-model.number="c.speedKmh"
                  type="number"
                  inputmode="decimal"
                  min="0"
                  step="0.1"
                />
              </label>
              <label class="field">
                <span>Nachylenie (%)</span>
                <input
                  v-model.number="c.inclinePercent"
                  type="number"
                  inputmode="decimal"
                  min="0"
                  step="0.5"
                />
              </label>
              <label class="field field--wide">
                <span>Notatka</span>
                <input v-model="c.notes" type="text" placeholder="—" />
              </label>
              <button
                type="button"
                class="cardio__del icon-button icon-button--sm icon-button--danger"
                :aria-label="`Usuń wpis cardio ${cIdx + 1}`"
                @click="removeCardio(cIdx)"
              >
                ×
              </button>
            </div>

            <p v-if="draft.cardio.length === 0" class="empty-line">
              Brak wpisów.
            </p>

            <div class="row-actions">
              <button type="button" class="ghost-button" @click="addCardio">
                + Dodaj wpis
              </button>
            </div>
          </section>

          <section class="block">
            <label class="field field--grow">
              <span>Notatki do ćwiczenia</span>
              <textarea
                v-model="draft.notes"
                rows="2"
                placeholder="Jak poszło?"
              />
            </label>
          </section>
        </div>

        <footer class="modal__footer">
          <span class="hint hint--footer"
            ><kbd>Enter</kbd> zapisz · <kbd>Esc</kbd> anuluj</span
          >
          <div class="modal__actions">
            <button type="button" class="text-button" @click="requestCancel">
              Anuluj
            </button>
            <button
              type="button"
              class="primary-button"
              :disabled="attemptedSave && !canSave"
              @click="requestSave"
            >
              {{ isNew ? "Dodaj ćwiczenie" : "Zapisz ćwiczenie" }}
            </button>
          </div>
        </footer>
      </div>
    </div>
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

  width: min(760px, 100%);
  max-height: min(860px, calc(100dvh - 48px));

  background: var(--surface-solid);
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-card);
  box-shadow: var(--shadow-glass);

  color: var(--text);
  font-family: var(--font-body);

  outline: none;

  animation: pop 180ms var(--ease) both;
}

/* --- header / footer --- */

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

  padding: var(--card-pad-sm) var(--card-pad);
}

.modal__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;

  padding: var(--card-pad-sm) var(--card-pad);
  border-top: 1px solid var(--border);
  background: var(--glass-soft);
  border-radius: 0 0 var(--radius-card) var(--radius-card);
}

.modal__actions {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-left: auto;
}

/* --- blocks --- */

.block {
  padding: var(--card-pad-sm) 0;
}

.block + .block {
  border-top: 1px solid var(--border);
}

.block--row {
  display: flex;
  gap: var(--card-gap);
  flex-wrap: wrap;
}

.block__label {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 8px;

  color: var(--text-dim);
  font-size: 0.8rem;
}

.block__count {
  min-width: 18px;
  padding: 0 5px;

  border-radius: var(--radius-pill);
  background: var(--surface-hover);
  border: 1px solid var(--border);

  color: var(--text-faint);
  font-size: 0.68rem;
  text-align: center;
}

/* --- current exercise --- */

.current {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  flex-wrap: wrap;

  padding: 10px 12px;

  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  background: var(--surface-hover);
}

.current__main {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
  flex-wrap: wrap;
}

.current__name {
  font-size: 0.95rem;
  font-weight: 600;
  overflow-wrap: anywhere;
}

/* --- picker --- */

.picker {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.picker__controls {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.picker__search {
  position: relative;
  flex: 1;
  min-width: 200px;
}

.picker__search-icon {
  position: absolute;
  top: 50%;
  left: 10px;
  transform: translateY(-50%);
  color: var(--text-faint);
  pointer-events: none;
}

.picker__search input {
  width: 100%;
  height: 36px;
  padding: 0 10px 0 30px;

  background: var(--surface-hover);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text);
  font-size: 0.88rem;
}

.picker__search input:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.segmented {
  display: inline-flex;
  padding: 3px;
  gap: 2px;

  background: var(--segment-bg);
  border: 1px solid var(--border);
  border-radius: var(--radius-control);
}

.segmented__item {
  padding: 0 10px;
  height: 28px;

  background: transparent;
  border: 0;
  border-radius: calc(var(--radius-control) - 2px);
  color: var(--text-faint);
  font-size: 0.78rem;
}

.segmented__item[aria-pressed="true"] {
  background: var(--segment-on);
  color: var(--text);
}

.segmented__item:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.picker__list {
  max-height: 280px;
  overflow-y: auto;
  overscroll-behavior: contain;

  margin: 0;
  padding: 4px;
  list-style: none;

  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  background: var(--bg-base);
}

.picker__empty {
  padding: 14px 10px;
  color: var(--text-faint);
  font-size: 0.82rem;
  text-align: center;
}

.option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;

  min-height: 40px;
  padding: 6px 10px;

  border-left: 2px solid transparent;
  border-radius: var(--radius-sm);
  cursor: pointer;
}

.option--active {
  background: var(--glass-strong);
}

.option--selected {
  border-left-color: var(--accent);
}

.option--selected .option__name {
  color: var(--accent-text);
}

.option__text {
  display: flex;
  flex-direction: column;
  gap: 1px;
  min-width: 0;
}

.option__name {
  font-size: 0.88rem;
  color: var(--text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.option__meta {
  font-size: 0.72rem;
  color: var(--text-faint);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.picker__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

/* --- badges, hints, notices --- */

.badge {
  flex: none;
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

.badge--warn {
  border-color: rgba(230, 180, 80, 0.35);
  background: rgba(230, 180, 80, 0.08);
  color: var(--warning);
}

.hint {
  color: var(--text-faint);
  font-size: 0.72rem;
}

kbd {
  display: inline-block;
  min-width: 16px;
  margin: 0 1px;
  padding: 0 4px;

  border: 1px solid var(--border-strong);
  border-bottom-width: 2px;
  border-radius: var(--radius-xs);

  color: var(--text-dim);
  font-family: inherit;
  font-size: 0.66rem;
  line-height: 15px;
  text-align: center;
}

.notice {
  margin: 10px 0 0;
  padding: 8px 10px;

  border: 1px solid rgba(230, 180, 80, 0.3);
  border-radius: var(--radius-md);
  background: rgba(230, 180, 80, 0.06);

  color: var(--warning);
  font-size: 0.78rem;
}

.field-error {
  margin: 0;
  color: var(--danger);
  font-size: 0.78rem;
}

.empty-line {
  margin: 6px 0 0;
  color: var(--text-faint);
  font-size: 0.82rem;
}

/* --- fields --- */

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.field > span:first-child {
  font-size: 0.72rem;
  color: var(--text-faint);
}

.field--grow {
  flex: 1;
  width: 100%;
}

.field input,
.field textarea,
.field select,
.set input {
  width: 100%;
  min-width: 0;
  height: 34px;
  padding: 0 9px;

  background: var(--surface-hover);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text);
  font-size: 0.85rem;

  transition: border-color 150ms var(--ease);
}

.field textarea {
  height: auto;
  padding: 7px 9px;
  resize: vertical;
}

.field input:hover,
.field textarea:hover,
.field select:hover,
.set input:hover {
  border-color: var(--border-strong);
}

.field input:focus-visible,
.field textarea:focus-visible,
.field select:focus-visible,
.set input:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

input[type="number"] {
  -moz-appearance: textfield;
  appearance: textfield;
}

input[type="number"]::-webkit-inner-spin-button,
input[type="number"]::-webkit-outer-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

/* --- sets --- */

.sets {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.set {
  display: grid;
  grid-template-columns:
    34px minmax(64px, 1fr) minmax(52px, 0.8fr) minmax(48px, 0.7fr)
    minmax(48px, 0.7fr) 64px minmax(90px, 1.6fr) 28px;
  grid-template-areas: "num weight reps rir rpe warm notes del";
  gap: 6px;
  align-items: center;

  padding: 3px 4px;
  border-radius: var(--radius-sm);
}

.set--head {
  padding-top: 0;
  padding-bottom: 0;
}

.set--head span {
  color: var(--text-faint);
  font-size: 0.68rem;
}

.set--top {
  background: var(--accent-dim);
}

.set--warmup input {
  color: var(--text-dim);
}

.set__num {
  grid-area: num;
  display: flex;
  align-items: center;
  gap: 3px;

  color: var(--text-dim);
  font-size: 0.82rem;
  font-variant-numeric: tabular-nums;
}

.set__weight {
  grid-area: weight;
}
.set__reps {
  grid-area: reps;
}
.set__rir {
  grid-area: rir;
}
.set__rpe {
  grid-area: rpe;
}
.set__warm {
  grid-area: warm;
}
.set__notes {
  grid-area: notes;
}
.set__del {
  grid-area: del;
}

.set input {
  font-variant-numeric: tabular-nums;
}

.crown {
  color: var(--accent-strong);
  font-size: 0.8rem;
}

.toggle {
  height: 30px;
  padding: 0 6px;

  background: transparent;
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text-faint);
  font-size: 0.72rem;
}

.toggle:hover {
  border-color: var(--border-strong);
  color: var(--text-dim);
}

.toggle[aria-pressed="true"] {
  border-color: var(--accent-border);
  background: var(--accent-soft);
  color: var(--accent-text);
}

.toggle:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.row-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-top: 10px;
}

/* --- cardio --- */

.cardio {
  position: relative;
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr)) 28px;
  gap: 8px;
  align-items: end;

  padding: 10px;
  margin-bottom: 8px;

  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  background: var(--glass-soft);
}

.cardio .field--grow {
  grid-column: 1 / 5;
}

.cardio .field--wide {
  grid-column: 1 / 5;
}

.cardio__del {
  grid-column: 5;
  grid-row: 1;
  align-self: end;
  margin-bottom: 4px;
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
  font-size: 1rem;
  line-height: 1;
}

.icon-button:hover {
  color: var(--text);
  border-color: var(--border-strong);
}

.icon-button--sm {
  width: 26px;
  height: 26px;
}

.icon-button--danger:hover {
  color: var(--danger);
  border-color: var(--danger-border);
  background: var(--danger-bg);
}

.icon-button:focus-visible,
.ghost-button:focus-visible,
.text-button:focus-visible,
.primary-button:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 2px;
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
  padding: 8px 6px;

  background: none;
  border: none;
  color: var(--text-faint);
  font-size: 0.85rem;
}

.text-button:hover {
  color: var(--text);
}

.primary-button {
  min-height: 36px;
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

/* --- phones: full-height sheet, sets wrap onto two lines --- */

@media (max-width: 560px) {
  .modal-backdrop {
    padding: 0;
    place-items: end stretch;
  }

  .modal {
    width: 100%;
    max-height: 100dvh;
    height: 100dvh;
    border-radius: 0;
    border-left: 0;
    border-right: 0;
    border-bottom: 0;
    padding-bottom: env(safe-area-inset-bottom);
  }

  .modal__footer {
    border-radius: 0;
  }

  .hint {
    display: none;
  }

  .modal__actions {
    width: 100%;
  }

  .modal__actions .primary-button {
    flex: 1;
    min-height: 42px;
  }

  .set {
    grid-template-columns: 26px repeat(4, minmax(0, 1fr)) 28px;
    grid-template-areas:
      "num weight reps rir rpe del"
      ". warm notes notes notes notes";
    row-gap: 4px;
    padding: 6px 4px;
  }

  .set:not(.set--head) + .set {
    border-top: 1px solid var(--border);
  }

  .set--head {
    grid-template-areas: "num weight reps rir rpe del";
  }

  .set--head .set__warm,
  .set--head .set__notes {
    display: none;
  }

  .set input {
    height: 38px;
    font-size: 16px; /* stops iOS zooming into the field */
  }

  .field input,
  .field textarea,
  .field select,
  .picker__search input {
    font-size: 16px;
  }

  .toggle {
    height: 34px;
  }

  .cardio {
    grid-template-columns: repeat(2, minmax(0, 1fr)) 28px;
  }

  .cardio .field--grow,
  .cardio .field--wide {
    grid-column: 1 / 3;
  }

  .cardio__del {
    grid-column: 3;
  }

  .segmented {
    width: 100%;
  }

  .segmented__item {
    flex: 1;
  }
}
</style>
