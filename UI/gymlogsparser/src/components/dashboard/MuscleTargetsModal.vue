<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from "vue";

import { useMuscleTargetsStore } from "@/stores/muscleTargets";
import type { MuscleTarget } from "@/types/muscleTargets";
import { formatRange } from "@/utils/dashboard";

/**
 * Edits the weekly target range (min–max sets/wk) of every muscle group. Works on a
 * draft: nothing is stored until Save. "Reset" puts a row (or every row) back to
 * its default; Save then stores it.
 */
const emit = defineEmits<{ close: [] }>();

const MAX_SETS = 100;

const targets = useMuscleTargetsStore();

interface DraftRow {
  muscleGroup: string;
  /**
   * As the inputs hold them: v-model on a number input gives a number, or "" while
   * the field is cleared.
   */
  min: string | number;
  max: string | number;
  defaultMinSets: number;
  defaultMaxSets: number;
}

const draft = ref<DraftRow[]>([]);
const saving = ref(false);
const saveError = ref<string | null>(null);
const attemptedSave = ref(false);

function toDraft(list: MuscleTarget[]): DraftRow[] {
  return list.map((t) => ({
    muscleGroup: t.muscleGroup,
    min: String(t.minSets),
    max: String(t.maxSets),
    defaultMinSets: t.defaultMinSets,
    defaultMaxSets: t.defaultMaxSets,
  }));
}

const parse = (value: string | number): number | null => {
  const text = String(value).trim();
  if (!/^\d+$/.test(text)) return null;
  const n = Number(text);
  return n <= MAX_SETS ? n : null;
};

function rowError(row: DraftRow): string | null {
  const min = parse(row.min);
  const max = parse(row.max);
  if (min === null || max === null) return `Whole sets from 0 to ${MAX_SETS}.`;
  if (min > max) return "Min is above max.";
  return null;
}

const isDefault = (row: DraftRow) =>
  parse(row.min) === row.defaultMinSets && parse(row.max) === row.defaultMaxSets;

const canSave = computed(() => draft.value.every((r) => rowError(r) === null));

const allDefault = computed(() => draft.value.every(isDefault));

const isDirty = computed(() =>
  draft.value.some((row) => {
    const stored = targets.targetOf(row.muscleGroup);
    return (
      !stored ||
      parse(row.min) !== stored.minSets ||
      parse(row.max) !== stored.maxSets
    );
  }),
);

function resetRow(row: DraftRow) {
  row.min = String(row.defaultMinSets);
  row.max = String(row.defaultMaxSets);
}

function resetAll() {
  draft.value.forEach(resetRow);
}

async function save() {
  attemptedSave.value = true;
  if (!canSave.value || saving.value) return;
  saving.value = true;
  saveError.value = null;
  try {
    await targets.save(
      draft.value.map((r) => ({
        muscleGroup: r.muscleGroup,
        minSets: parse(r.min)!,
        maxSets: parse(r.max)!,
      })),
    );
    emit("close");
  } catch (err) {
    saveError.value =
      err instanceof Error ? err.message : "Could not save the targets.";
  } finally {
    saving.value = false;
  }
}

function requestClose() {
  if (
    isDirty.value &&
    !saving.value &&
    !window.confirm("Discard your changes to the targets?")
  ) {
    return;
  }
  emit("close");
}

// --- dialog: scroll lock, focus, keyboard, backdrop ---

const dialogEl = ref<HTMLElement | null>(null);
let previousFocus: HTMLElement | null = null;
let previousOverflow = "";

onMounted(async () => {
  previousFocus = document.activeElement as HTMLElement | null;
  previousOverflow = document.body.style.overflow;
  document.body.style.overflow = "hidden";

  await targets.ensureLoaded();
  draft.value = toDraft(targets.targets);

  await nextTick();
  dialogEl.value?.focus();
});

onBeforeUnmount(() => {
  document.body.style.overflow = previousOverflow;
  if (previousFocus?.isConnected) previousFocus.focus();
});

function trapTab(e: KeyboardEvent) {
  if (!dialogEl.value) return;
  const items = Array.from(
    dialogEl.value.querySelectorAll<HTMLElement>(
      "button:not([disabled]), input:not([disabled])",
    ),
  ).filter((el) => el.offsetParent !== null);
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
  } else if (e.key === "Escape") {
    e.preventDefault();
    e.stopPropagation();
    requestClose();
  } else if (
    e.key === "Enter" &&
    !e.isComposing &&
    e.target instanceof HTMLInputElement
  ) {
    e.preventDefault();
    save();
  }
}

let backdropPointerDown = false;

function onBackdropPointerDown(e: PointerEvent) {
  backdropPointerDown = e.target === e.currentTarget;
}

function onBackdropClick(e: MouseEvent) {
  // Only a press that started and ended on the backdrop closes the dialog.
  if (backdropPointerDown && e.target === e.currentTarget) requestClose();
  backdropPointerDown = false;
}

const titleId = `targets-dialog-title-${Math.random().toString(36).slice(2, 9)}`;
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
          <div>
            <span class="modal__eyebrow">Weekly sets per body part</span>
            <h2 :id="titleId" class="modal__title">Target ranges</h2>
            <p class="intro">
              Working sets per week. Secondary muscles count by share (bench press
              adds 0.5 to Triceps).
            </p>
          </div>
          <button
            type="button"
            class="icon-button"
            aria-label="Close without saving"
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
          <p v-if="draft.length === 0 && targets.error" class="error">
            {{ targets.error }}
          </p>
          <p v-else-if="draft.length === 0" class="muted">Loading targets…</p>

          <div v-else class="rows" role="table" aria-label="Target ranges">
            <div class="row row--head" role="row">
              <span role="columnheader">Body part</span>
              <span role="columnheader">Min</span>
              <span role="columnheader">Max</span>
              <span role="columnheader">Default</span>
              <span role="columnheader"><span class="sr-only">Reset</span></span>
            </div>
            <div
              v-for="row in draft"
              :key="row.muscleGroup"
              class="row"
              :class="{ invalid: attemptedSave && rowError(row) }"
              role="row"
            >
              <span class="group" role="cell">{{ row.muscleGroup }}</span>
              <span role="cell">
                <input
                  v-model="row.min"
                  type="number"
                  inputmode="numeric"
                  min="0"
                  :max="MAX_SETS"
                  step="1"
                  :aria-label="`${row.muscleGroup} minimum sets per week`"
                />
              </span>
              <span role="cell">
                <input
                  v-model="row.max"
                  type="number"
                  inputmode="numeric"
                  min="0"
                  :max="MAX_SETS"
                  step="1"
                  :aria-label="`${row.muscleGroup} maximum sets per week`"
                />
              </span>
              <span class="muted default" role="cell">{{
                formatRange({
                  minSets: row.defaultMinSets,
                  maxSets: row.defaultMaxSets,
                })
              }}</span>
              <span role="cell">
                <button
                  v-if="!isDefault(row)"
                  type="button"
                  class="text-button"
                  :aria-label="`Reset ${row.muscleGroup} to the default ${row.defaultMinSets}–${row.defaultMaxSets}`"
                  @click="resetRow(row)"
                >
                  Reset
                </button>
              </span>
              <span
                v-if="attemptedSave && rowError(row)"
                class="row-error"
                role="cell"
                >{{ rowError(row) }}</span
              >
            </div>
          </div>
        </div>

        <footer class="modal__footer">
          <button
            type="button"
            class="ghost-button"
            :disabled="draft.length === 0 || allDefault"
            @click="resetAll"
          >
            Reset all to default
          </button>
          <div class="modal__actions">
            <span v-if="saveError" class="error">{{ saveError }}</span>
            <button type="button" class="text-button" @click="requestClose">
              Cancel
            </button>
            <button
              type="button"
              class="primary-button"
              :disabled="draft.length === 0 || saving || (attemptedSave && !canSave)"
              @click="save"
            >
              {{ saving ? "Saving…" : "Save targets" }}
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
}

.modal {
  display: flex;
  flex-direction: column;

  width: min(560px, 100%);
  max-height: min(860px, calc(100dvh - 48px));

  background: var(--surface-solid);
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-card);
  box-shadow: var(--shadow-glass);

  color: var(--text);
  font-family: var(--font-body);
  font-size: 14px;
}

.modal:focus {
  outline: none;
}

.modal__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;

  padding: var(--card-pad) var(--card-pad) var(--card-pad-sm);
  border-bottom: 1px solid var(--border);
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
}

.intro {
  margin: 6px 0 0;
  color: var(--text-secondary);
  font-size: 12px;
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
  flex-wrap: wrap;
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
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  margin-left: auto;
}

.rows {
  display: flex;
  flex-direction: column;
}

.row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 64px 64px 56px 52px;
  align-items: center;
  gap: 8px;
  min-height: 40px;
}

.row + .row {
  border-top: 1px solid var(--border);
}

.row--head {
  min-height: 28px;
  color: var(--text-faint);
  font-size: 0.72rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.group {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.default {
  font-variant-numeric: tabular-nums;
}

.row-error {
  grid-column: 1 / -1;
  padding-bottom: 6px;
  color: var(--danger);
  font-size: 12px;
}

input {
  width: 100%;
  min-width: 0;
  height: 32px;
  padding: 0 8px;

  background: var(--surface-hover);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text);
  font-size: 0.85rem;
  font-variant-numeric: tabular-nums;

  -moz-appearance: textfield;
  appearance: textfield;
}

input::-webkit-inner-spin-button,
input::-webkit-outer-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

input:hover {
  border-color: var(--border-strong);
}

input:focus-visible,
button:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.invalid input {
  border-color: var(--danger-border);
}

.muted {
  color: var(--text-secondary);
}

.error {
  margin: 0;
  color: var(--danger);
  font-size: 12px;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}

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

.icon-button:hover {
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

.ghost-button:hover:not(:disabled) {
  border-color: var(--accent-border);
  color: var(--text);
}

.ghost-button:disabled {
  color: var(--text-faint);
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

/* phones: full-height sheet */
@media (max-width: 560px) {
  .modal-backdrop {
    padding: 0;
    place-items: end stretch;
  }

  .modal {
    width: 100%;
    height: 100dvh;
    max-height: 100dvh;
    border-radius: 0;
    border-left: 0;
    border-right: 0;
    border-bottom: 0;
    padding-bottom: env(safe-area-inset-bottom);
  }

  .modal__footer {
    border-radius: 0;
  }

  .row {
    grid-template-columns: minmax(0, 1fr) 56px 56px 44px 48px;
    gap: 6px;
  }

  input {
    height: 38px;
    font-size: 16px; /* stops iOS zooming into the field */
  }
}
</style>
