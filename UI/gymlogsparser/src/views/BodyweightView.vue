<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";

import { useBodyweightStore } from "@/stores/bodyweight";
import {
  BODYWEIGHT_MAX_KG,
  BODYWEIGHT_MIN_KG,
  type BodyweightEntry,
} from "@/types/bodyweight";
import { todayIso } from "@/utils/dashboard";

const store = useBodyweightStore();
const { entries, loading, error } = storeToRefs(store);

const today = todayIso();

const newDate = ref(today);
const newWeight = ref("");
const adding = ref(false);
const addError = ref<string | null>(null);

/** The measurement being edited, by its saved date; null when none. */
const editingDate = ref<string | null>(null);
const editDate = ref("");
const editWeight = ref("");
const savingEdit = ref(false);
const editError = ref<string | null>(null);
const editWeightInput = ref<HTMLInputElement[]>([]);

const deletingDate = ref<string | null>(null);
const listError = ref<string | null>(null);

/** Accepts "82.5" and "82,5". Returns the kg value or an English error message. */
function parseWeight(raw: string): number | string {
  const text = raw.trim().replace(",", ".");
  if (!/^\d+(\.\d{1,2})?$/.test(text)) {
    return "Enter the weight in kg with at most two decimals, for example 82.5.";
  }
  const kg = Number(text);
  if (kg < BODYWEIGHT_MIN_KG || kg > BODYWEIGHT_MAX_KG) {
    return `The weight must be between ${BODYWEIGHT_MIN_KG} and ${BODYWEIGHT_MAX_KG} kg.`;
  }
  return kg;
}

function checkDate(date: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) return "Pick a date.";
  if (date > today) return "The date can't be in the future.";
  return null;
}

function toEntry(date: string, rawWeight: string): BodyweightEntry | string {
  const dateError = checkDate(date);
  if (dateError) return dateError;
  const kg = parseWeight(rawWeight);
  return typeof kg === "string" ? kg : { date, weightKg: kg };
}

const message = (err: unknown, fallback: string) =>
  err instanceof Error && err.message ? err.message : fallback;

async function add() {
  const entry = toEntry(newDate.value, newWeight.value);
  if (typeof entry === "string") {
    addError.value = entry;
    return;
  }

  adding.value = true;
  addError.value = null;
  try {
    await store.add(entry);
    newWeight.value = "";
  } catch (err) {
    addError.value = message(err, "Could not save the measurement.");
  } finally {
    adding.value = false;
  }
}

async function startEdit(entry: BodyweightEntry) {
  editingDate.value = entry.date;
  editDate.value = entry.date;
  editWeight.value = String(entry.weightKg);
  editError.value = null;
  listError.value = null;
  await nextTick();
  editWeightInput.value[0]?.focus();
}

function cancelEdit() {
  editingDate.value = null;
  editError.value = null;
}

async function saveEdit() {
  const original = editingDate.value;
  if (original === null) return;

  const entry = toEntry(editDate.value, editWeight.value);
  if (typeof entry === "string") {
    editError.value = entry;
    return;
  }

  savingEdit.value = true;
  editError.value = null;
  try {
    await store.update(original, entry);
    editingDate.value = null;
  } catch (err) {
    editError.value = message(err, "Could not save the change.");
  } finally {
    savingEdit.value = false;
  }
}

async function remove(entry: BodyweightEntry) {
  if (
    !window.confirm(
      `Delete the measurement of ${formatKg(entry.weightKg)} kg on ${formatDate(entry.date)}?`,
    )
  ) {
    return;
  }

  deletingDate.value = entry.date;
  listError.value = null;
  try {
    await store.remove(entry.date);
    if (editingDate.value === entry.date) editingDate.value = null;
  } catch (err) {
    listError.value = message(err, "Could not delete the measurement.");
  } finally {
    deletingDate.value = null;
  }
}

function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  return new Date(y!, m! - 1, d!).toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/** "82", "82.5", "82.45": at most the two decimals the API stores. */
function formatKg(kg: number): string {
  return String(Number(kg.toFixed(2)));
}

/** Change since the previous (older) measurement, e.g. "+0.4" or "−1.2". */
const deltas = computed(() =>
  entries.value.map((entry, i) => {
    const older = entries.value[i + 1];
    if (!older) return null;
    const diff = Math.round((entry.weightKg - older.weightKg) * 100) / 100;
    if (diff === 0) return "±0";
    return `${diff > 0 ? "+" : "−"}${Math.abs(diff).toFixed(1)}`;
  }),
);

onMounted(() => store.load());
</script>

<template>
  <div class="bodyweight-view">
    <header class="view-header">
      <h1>Bodyweight</h1>
      <p class="subtitle">
        Your measurements set the bodyweight and DOTS on Progress, and the
        bodyweight the parser uses for pull-ups, dips and push-ups.
      </p>
    </header>

    <form class="card add-form" novalidate @submit.prevent="add">
      <label class="field">
        <span class="field-label">Date</span>
        <input v-model="newDate" type="date" :max="today" required />
      </label>
      <label class="field">
        <span class="field-label">Weight (kg)</span>
        <input
          v-model="newWeight"
          type="text"
          inputmode="decimal"
          autocomplete="off"
          placeholder="82.5"
          required
        />
      </label>
      <button type="submit" class="primary-button" :disabled="adding">
        {{ adding ? "Saving…" : "Add" }}
      </button>
      <p v-if="addError" class="error-text form-error" role="alert">
        {{ addError }}
      </p>
    </form>

    <p v-if="error" class="error-text">{{ error }}</p>
    <p v-else-if="loading && entries.length === 0" class="status-text">
      Loading measurements…
    </p>
    <p v-else-if="entries.length === 0" class="status-text">
      No measurements yet. Add your first one above; until then Progress shows
      no bodyweight or DOTS.
    </p>

    <section v-else class="card list" aria-label="Measurements">
      <p v-if="listError" class="error-text" role="alert">{{ listError }}</p>
      <ul>
        <li
          v-for="(entry, i) in entries"
          :key="entry.date"
          class="row"
          :class="{ editing: editingDate === entry.date }"
        >
          <form
            v-if="editingDate === entry.date"
            class="edit-form"
            novalidate
            @submit.prevent="saveEdit"
            @keydown.esc.prevent="cancelEdit"
          >
            <label class="field">
              <span class="sr-only">Date</span>
              <input v-model="editDate" type="date" :max="today" required />
            </label>
            <label class="field">
              <span class="sr-only">Weight (kg)</span>
              <input
                ref="editWeightInput"
                v-model="editWeight"
                type="text"
                inputmode="decimal"
                autocomplete="off"
                required
              />
            </label>
            <div class="actions">
              <button
                type="submit"
                class="primary-button"
                :disabled="savingEdit"
              >
                {{ savingEdit ? "Saving…" : "Save" }}
              </button>
              <button type="button" class="ghost-button" @click="cancelEdit">
                Cancel
              </button>
            </div>
            <p v-if="editError" class="error-text form-error" role="alert">
              {{ editError }}
            </p>
          </form>

          <template v-else>
            <span class="date">{{ formatDate(entry.date) }}</span>
            <span class="weight">
              {{ formatKg(entry.weightKg) }}<span class="unit">kg</span>
            </span>
            <span class="delta">{{ deltas[i] ?? "" }}</span>
            <div class="actions">
              <button
                type="button"
                class="ghost-button"
                :aria-label="`Edit the measurement on ${formatDate(entry.date)}`"
                @click="startEdit(entry)"
              >
                Edit
              </button>
              <button
                type="button"
                class="ghost-button danger"
                :disabled="deletingDate === entry.date"
                :aria-label="`Delete the measurement on ${formatDate(entry.date)}`"
                @click="remove(entry)"
              >
                Delete
              </button>
            </div>
          </template>
        </li>
      </ul>
    </section>
  </div>
</template>

<style scoped>
.bodyweight-view {
  max-width: 640px;
  margin: 0 auto;
}

.view-header {
  margin-bottom: var(--card-gap-lg);
}

.view-header h1 {
  margin: 0 0 4px;
  font-size: 1.5rem;
  color: var(--text-primary);
}

.subtitle {
  margin: 0;
  font-size: 0.9rem;
  color: var(--text-tertiary);
}

.card {
  padding: var(--card-pad);

  background: var(--card-bg);
  border: 1px solid var(--card-border);
  border-radius: var(--radius-card);
}

.add-form {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: var(--card-gap-md);

  margin-bottom: var(--card-gap-lg);
}

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.field-label {
  font-size: 0.75rem;
  color: var(--text-tertiary);
}

input {
  height: 38px;
  min-width: 0;
  padding: 0 var(--card-pad-xs);

  background: var(--glass-soft);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-control);
  color: var(--text-primary);
  color-scheme: dark;
  font: inherit;
  font-size: 16px; /* stops iOS zooming into the field */
  font-variant-numeric: tabular-nums;
}

input:hover {
  border-color: var(--glass-border-strong);
}

input:focus-visible,
button:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

input[inputmode="decimal"] {
  width: 7.5em;
}

.primary-button,
.ghost-button {
  min-height: 38px;
  padding: 0 var(--card-pad);

  border-radius: var(--radius-control);
  font: inherit;
  font-size: 0.85rem;
  cursor: pointer;
}

.primary-button {
  background: var(--accent);
  border: 1px solid var(--accent-border);
  color: var(--on-accent);
  font-weight: 600;
}

.primary-button:hover:not(:disabled) {
  background: var(--accent-strong);
}

.primary-button:disabled {
  background: var(--glass);
  border-color: var(--glass-border);
  color: var(--text-quiet);
  cursor: default;
}

.ghost-button {
  padding: 0 var(--card-pad-sm);

  background: transparent;
  border: 1px solid var(--glass-border);
  color: var(--text-secondary);
}

.ghost-button:hover:not(:disabled) {
  border-color: var(--accent-border);
  color: var(--text-primary);
}

.ghost-button.danger:hover:not(:disabled) {
  border-color: var(--danger-border);
  background: var(--danger-bg);
  color: var(--danger);
}

.ghost-button:disabled {
  color: var(--text-quiet);
  cursor: default;
}

.list {
  padding: var(--card-pad-xs) var(--card-pad);
}

.list ul {
  margin: 0;
  padding: 0;
  list-style: none;
}

.row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto 4em auto;
  align-items: center;
  gap: var(--card-gap-md);

  padding: var(--card-pad-xs) 0;
  border-bottom: 1px solid var(--card-border);
}

.row:last-child {
  border-bottom: 0;
}

.row.editing {
  display: block;
}

.edit-form {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--card-gap-sm);
}

.date {
  color: var(--text-primary);
}

.weight {
  color: var(--text-primary);
  font-variant-numeric: tabular-nums;
  text-align: right;
}

.unit {
  margin-left: 3px;
  font-size: 0.7rem;
  color: var(--text-tertiary);
}

.delta {
  color: var(--text-tertiary);
  font-size: 0.8rem;
  font-variant-numeric: tabular-nums;
}

.actions {
  display: flex;
  gap: var(--card-gap-sm);
}

.status-text {
  color: var(--text-tertiary);
  font-size: 0.9rem;
}

.error-text {
  margin: 0;
  color: var(--danger);
  font-size: 0.85rem;
}

.form-error {
  flex-basis: 100%;
}

.list > .error-text {
  padding: var(--card-pad-xs) 0;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}

@media (max-width: 560px) {
  .card {
    padding: var(--card-pad-sm);
  }

  .list {
    padding: var(--card-pad-xs) var(--card-pad-sm);
  }

  .add-form .field {
    flex: 1 1 8em;
  }

  .add-form input {
    width: 100%;
  }

  .add-form .primary-button {
    flex: 1 1 100%;
  }

  .row {
    grid-template-columns: minmax(0, 1fr) auto auto;
    row-gap: var(--card-gap-sm);
  }

  .delta {
    display: none;
  }

  .row .actions {
    grid-column: 1 / -1;
    justify-content: flex-end;
  }
}
</style>
