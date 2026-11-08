<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { RouterLink } from "vue-router";

import { useHevyImportStore } from "@/stores/hevyImport";
import { useExerciseCatalogStore } from "@/stores/exercises";
import { HEVY_MAX_CSV_LENGTH } from "@/types/import";
import { MUSCLE_GROUPS } from "@/utils/muscleGroups";
import type { ExerciseDefinition } from "@/types/exercise";

const store = useHevyImportStore();
const { fileName, preview, result, mappings, busy, error } = storeToRefs(store);
const catalog = useExerciseCatalogStore();

const fileInput = ref<HTMLInputElement | null>(null);
const fileError = ref<string | null>(null);

onMounted(() => catalog.ensureLoaded());

/** Catalog exercises grouped by primary muscle group, in the app's group order. */
const catalogGroups = computed(() => {
  const byGroup = new Map<string, ExerciseDefinition[]>();
  for (const definition of catalog.definitions) {
    const list = byGroup.get(definition.muscleGroup) ?? [];
    list.push(definition);
    byGroup.set(definition.muscleGroup, list);
  }
  const order = (group: string) => {
    const index = (MUSCLE_GROUPS as readonly string[]).indexOf(group);
    return index < 0 ? MUSCLE_GROUPS.length : index;
  };
  return [...byGroup.entries()]
    .sort(([a], [b]) => order(a) - order(b) || a.localeCompare(b))
    .map(([group, list]) => ({
      group,
      exercises: [...list].sort((a, b) => a.name.localeCompare(b.name)),
    }));
});

const exercises = computed(() => preview.value?.exercises ?? []);
const mappedCount = computed(
  () => exercises.value.filter((e) => mappings.value[e.title]).length,
);
const unmappedSets = computed(() =>
  exercises.value
    .filter((e) => !mappings.value[e.title])
    .reduce((sum, e) => sum + e.sets, 0),
);

async function onFile(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  fileError.value = null;
  if (!file) return;

  const text = await file.text();
  if (text.length > HEVY_MAX_CSV_LENGTH) {
    fileError.value = "The file is larger than 10 MB. Split the export into smaller files.";
    return;
  }
  await store.load(file.name, text);
}

function startOver() {
  store.reset();
  fileError.value = null;
  if (fileInput.value) fileInput.value.value = "";
}

const plural = (n: number, one: string, many: string) =>
  `${n.toLocaleString("en-GB")} ${n === 1 ? one : many}`;
</script>

<template>
  <div class="import-view">
    <header class="view-header">
      <h1>Import from Hevy</h1>
      <p class="subtitle">
        Export your workouts from Hevy as CSV (Settings → Export &amp; import
        data → Export workouts) and pick the file. You see what will be
        imported before anything is saved, and importing the same file again
        skips the workouts you already have.
      </p>
    </header>

    <section class="card">
      <label class="field">
        <span class="field-label">Hevy CSV export</span>
        <input
          ref="fileInput"
          type="file"
          accept=".csv,text/csv"
          :disabled="busy"
          @change="onFile"
        />
      </label>
      <p v-if="busy && !preview" class="status-text">Reading {{ fileName }}…</p>
      <p v-if="fileError" class="error-text" role="alert">{{ fileError }}</p>
      <p v-if="error" class="error-text" role="alert">{{ error }}</p>
    </section>

    <section v-if="result" class="card" aria-label="Import result">
      <h2>Done</h2>
      <p class="result-line">
        Imported {{ plural(result.workouts, "workout", "workouts") }} with
        {{ plural(result.sets, "set", "sets") }}.
      </p>
      <ul class="skipped">
        <li v-if="result.duplicateWorkouts > 0">
          {{ plural(result.duplicateWorkouts, "workout was", "workouts were") }}
          already in your log.
        </li>
        <li v-if="result.unmappedSets > 0">
          {{ plural(result.unmappedSets, "set", "sets") }} of unmapped exercises
          skipped.
        </li>
        <li v-if="result.errorCount > 0">
          {{ plural(result.errorCount, "bad row", "bad rows") }} skipped (listed
          below).
        </li>
      </ul>
      <div class="actions">
        <RouterLink to="/edit" class="ghost-button">See workouts</RouterLink>
        <button type="button" class="ghost-button" @click="startOver">
          Import another file
        </button>
      </div>
    </section>

    <template v-if="preview && !result">
      <section class="card" aria-label="Preview">
        <h2>{{ fileName }}</h2>
        <dl class="stats">
          <div>
            <dt>Workouts in file</dt>
            <dd>{{ preview.workoutsInFile }}</dd>
          </div>
          <div>
            <dt>New</dt>
            <dd>{{ preview.workouts }}</dd>
          </div>
          <div>
            <dt>Already in your log</dt>
            <dd>{{ preview.duplicateWorkouts }}</dd>
          </div>
          <div>
            <dt>Bad rows</dt>
            <dd :class="{ warn: preview.errorCount > 0 }">
              {{ preview.errorCount }}
            </dd>
          </div>
        </dl>
        <p
          v-if="preview.workouts === 0 && preview.duplicateWorkouts > 0"
          class="status-text"
        >
          Everything in this file is already in your log.
        </p>
      </section>

      <section class="card" aria-labelledby="mapping-heading">
        <h2 id="mapping-heading">Exercises</h2>
        <p class="hint">
          {{ mappedCount }} of {{ exercises.length }} Hevy exercises are matched
          to your catalog. Pick a catalog exercise for the rest, or leave them on
          “Skip”: their sets ({{ unmappedSets }}) won't be imported. Missing an
          exercise? Add it on
          <RouterLink to="/exercises">Exercises</RouterLink> first, then pick
          the file again.
        </p>
        <ul class="mapping-list">
          <li v-for="exercise in exercises" :key="exercise.title" class="mapping">
            <div class="mapping-title">
              <span class="hevy-name">{{ exercise.title }}</span>
              <span class="meta">
                {{ plural(exercise.sets, "set", "sets") }}
                <span
                  v-if="exercise.autoMatched && mappings[exercise.title] === exercise.exerciseId"
                  class="badge"
                  >matched</span
                >
              </span>
            </div>
            <label class="sr-only" :for="`map-${exercise.title}`">
              Catalog exercise for {{ exercise.title }}
            </label>
            <select
              :id="`map-${exercise.title}`"
              v-model="mappings[exercise.title]"
              :class="{ unmapped: !mappings[exercise.title] }"
            >
              <option value="">Skip</option>
              <optgroup
                v-for="group in catalogGroups"
                :key="group.group"
                :label="group.group"
              >
                <option
                  v-for="definition in group.exercises"
                  :key="definition.id"
                  :value="definition.id"
                >
                  {{ definition.name }}
                </option>
              </optgroup>
            </select>
          </li>
        </ul>
        <p v-if="catalog.error" class="error-text">{{ catalog.error }}</p>

        <div class="actions">
          <button
            type="button"
            class="primary-button"
            :disabled="busy || preview.workouts === 0"
            @click="store.runImport()"
          >
            {{ busy ? "Importing…" : "Import" }}
          </button>
          <button type="button" class="ghost-button" :disabled="busy" @click="startOver">
            Cancel
          </button>
        </div>
      </section>
    </template>

    <section
      v-if="(result ?? preview)?.errorCount"
      class="card"
      aria-labelledby="errors-heading"
    >
      <h2 id="errors-heading">Bad rows</h2>
      <p class="hint">
        These rows were not imported; the rest of the file was.
        <template
          v-if="(result ?? preview)!.errorCount > (result ?? preview)!.errors.length"
        >
          Showing the first {{ (result ?? preview)!.errors.length }} of
          {{ (result ?? preview)!.errorCount }}.
        </template>
      </p>
      <ul class="errors">
        <li v-for="row in (result ?? preview)!.errors" :key="row.line">
          <span class="line">Line {{ row.line }}</span> {{ row.error }}
        </li>
      </ul>
    </section>
  </div>
</template>

<style scoped>
.import-view {
  max-width: 760px;
  margin: 0 auto;

  display: flex;
  flex-direction: column;
  gap: var(--card-gap-lg);
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

.card h2 {
  margin: 0 0 var(--card-gap-md);
  font-size: 1rem;
  color: var(--text-primary);
  overflow-wrap: anywhere;
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

input[type="file"],
select {
  min-width: 0;
  max-width: 100%;

  color: var(--text-primary);
  color-scheme: dark;
  font: inherit;
  font-size: 16px; /* stops iOS zooming into the field */
}

select {
  height: 38px;
  padding: 0 var(--card-pad-xs);

  background: var(--glass-soft);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-control);
}

select:hover {
  border-color: var(--glass-border-strong);
}

select.unmapped {
  border-color: var(--warning-border);
  color: var(--warning);
}

input:focus-visible,
select:focus-visible,
button:focus-visible,
a:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.stats {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--card-gap-md);
  margin: 0;
}

.stats dt {
  font-size: 0.75rem;
  color: var(--text-tertiary);
}

.stats dd {
  margin: 4px 0 0;
  font-size: 1.25rem;
  color: var(--text-primary);
  font-variant-numeric: tabular-nums;
}

.stats dd.warn {
  color: var(--warning);
}

.hint {
  margin: 0 0 var(--card-gap-md);
  font-size: 0.85rem;
  color: var(--text-tertiary);
}

.hint a {
  color: var(--accent-text);
}

.mapping-list,
.errors,
.skipped {
  margin: 0;
  padding: 0;
  list-style: none;
}

.mapping {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 18em);
  align-items: center;
  gap: var(--card-gap-md);

  padding: var(--card-pad-xs) 0;
  border-bottom: 1px solid var(--row-divider);
}

.mapping:last-child {
  border-bottom: 0;
}

.mapping-title {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.hevy-name {
  color: var(--text-primary);
  overflow-wrap: anywhere;
}

.meta {
  font-size: 0.75rem;
  color: var(--text-tertiary);
}

.badge {
  margin-left: 6px;
  padding: 1px 6px;

  border: 1px solid var(--success-border);
  border-radius: var(--radius-pill);
  background: var(--success-bg);
  color: var(--success);
  font-size: 0.7rem;
}

.result-line {
  margin: 0 0 var(--card-gap-sm);
  color: var(--text-primary);
}

.skipped li,
.errors li {
  padding: 2px 0;
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.errors .line {
  color: var(--warning);
  font-variant-numeric: tabular-nums;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--card-gap-sm);
  margin-top: var(--card-gap-lg);
}

.primary-button,
.ghost-button {
  display: inline-flex;
  align-items: center;
  min-height: 38px;
  padding: 0 var(--card-pad);

  border-radius: var(--radius-control);
  font: inherit;
  font-size: 0.85rem;
  text-decoration: none;
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

.ghost-button:disabled {
  color: var(--text-quiet);
  cursor: default;
}

.status-text {
  margin: var(--card-gap-sm) 0 0;
  color: var(--text-tertiary);
  font-size: 0.9rem;
}

.error-text {
  margin: var(--card-gap-sm) 0 0;
  color: var(--danger);
  font-size: 0.85rem;
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

  .stats {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .mapping {
    grid-template-columns: minmax(0, 1fr);
    gap: var(--card-gap-sm);
  }

  .mapping select {
    width: 100%;
  }

  .actions > * {
    flex: 1 1 auto;
    justify-content: center;
  }
}
</style>
