<script setup lang="ts">
import { computed, onMounted, ref } from "vue";

import { useExerciseCatalogStore } from "@/stores/exercises";
import type { ExerciseCategory, ExerciseDefinition } from "@/types/exercise";

const catalog = useExerciseCatalogStore();

const search = ref("");

/** ID of the exercise being edited; null while the form adds a new one. */
const editingId = ref<string | null>(null);
const form = ref<ExerciseDefinition>(emptyForm());
const aliasInput = ref("");

const saving = ref(false);
const formError = ref<string | null>(null);
const notice = ref<string | null>(null);

function emptyForm(): ExerciseDefinition {
  return {
    id: "",
    name: "",
    category: "Strength",
    muscleGroup: "",
    barbell: false,
    aliases: [],
  };
}

const categories: ExerciseCategory[] = ["Strength", "Cardio"];

const muscleGroups = computed(() =>
  [...new Set(catalog.definitions.map((d) => d.muscleGroup))].sort((a, b) =>
    a.localeCompare(b),
  ),
);

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase();
  const list = [...catalog.definitions].sort((a, b) =>
    a.name.localeCompare(b.name),
  );
  if (!q) return list;

  return list.filter(
    (d) =>
      d.name.toLowerCase().includes(q) ||
      d.id.includes(q) ||
      d.muscleGroup.toLowerCase().includes(q) ||
      d.aliases.some((a) => a.toLowerCase().includes(q)),
  );
});

/** What the server will use as the ID when the field is left empty. */
const idPreview = computed(() =>
  form.value.name
    .toLowerCase()
    .replace(/ł/g, "l")
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 100),
);

function addAliases(raw: string) {
  for (const part of raw.split(",")) {
    const alias = part.trim().replace(/\s+/g, " ");
    if (!alias) continue;
    const exists = form.value.aliases.some(
      (a) => a.toLowerCase() === alias.toLowerCase(),
    );
    if (!exists) form.value.aliases.push(alias);
  }
}

function commitAliasInput() {
  addAliases(aliasInput.value);
  aliasInput.value = "";
}

function onAliasKeydown(event: KeyboardEvent) {
  if (event.key === "Enter" || event.key === ",") {
    event.preventDefault();
    commitAliasInput();
  } else if (
    event.key === "Backspace" &&
    aliasInput.value === "" &&
    form.value.aliases.length > 0
  ) {
    form.value.aliases.pop();
  }
}

function removeAlias(index: number) {
  form.value.aliases.splice(index, 1);
}

function startEdit(definition: ExerciseDefinition) {
  editingId.value = definition.id;
  form.value = { ...definition, aliases: [...definition.aliases] };
  aliasInput.value = "";
  formError.value = null;
  notice.value = null;
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function resetForm() {
  editingId.value = null;
  form.value = emptyForm();
  aliasInput.value = "";
  formError.value = null;
}

async function save() {
  // An alias still in the text box counts: people rarely press Enter last.
  commitAliasInput();

  saving.value = true;
  formError.value = null;
  notice.value = null;

  try {
    if (editingId.value) {
      const updated = await catalog.update(editingId.value, form.value);
      notice.value = `Saved "${updated.name}".`;
    } else {
      const created = await catalog.create(form.value);
      notice.value = `Added "${created.name}" (${created.id}).`;
    }
    resetForm();
  } catch (err) {
    formError.value =
      err instanceof Error ? err.message : "Could not save the exercise.";
  } finally {
    saving.value = false;
  }
}

onMounted(() => catalog.reload());
</script>

<template>
  <div class="exercises-view">
    <header class="page-header">
      <h1>Exercises</h1>
      <p class="subtitle">
        The catalog the parser matches your logs against. Add the names you
        actually write as aliases; new exercises and aliases are used from the
        next parse on.
      </p>
    </header>

    <form class="panel" @submit.prevent="save">
      <h2>{{ editingId ? "Edit exercise" : "New exercise" }}</h2>

      <div class="grid">
        <label class="field">
          <span>Name</span>
          <input
            v-model="form.name"
            type="text"
            maxlength="200"
            placeholder="Smith Machine Squat"
            required
          />
        </label>

        <label class="field">
          <span>ID</span>
          <input
            v-if="!editingId"
            v-model="form.id"
            class="mono"
            type="text"
            maxlength="100"
            :placeholder="idPreview || 'derived from the name'"
          />
          <input v-else :value="form.id" class="mono" type="text" disabled />
        </label>

        <label class="field">
          <span>Category</span>
          <select v-model="form.category">
            <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
          </select>
        </label>

        <label class="field">
          <span>Muscle group</span>
          <input
            v-model="form.muscleGroup"
            type="text"
            maxlength="100"
            list="muscle-groups"
            placeholder="Legs"
            required
          />
          <datalist id="muscle-groups">
            <option v-for="g in muscleGroups" :key="g" :value="g" />
          </datalist>
        </label>
      </div>

      <label class="checkbox">
        <input v-model="form.barbell" type="checkbox" />
        <span>Barbell (per-side weights get converted to total load)</span>
      </label>

      <div class="field">
        <span>Aliases</span>
        <div class="alias-box">
          <span
            v-for="(alias, i) in form.aliases"
            :key="alias"
            class="chip"
          >
            {{ alias }}
            <button
              type="button"
              class="chip-remove"
              :title="`Remove ${alias}`"
              @click="removeAlias(i)"
            >
              ×
            </button>
          </span>
          <input
            v-model="aliasInput"
            type="text"
            class="alias-input"
            placeholder="e.g. smith, przysiad smith (Enter or comma to add)"
            @keydown="onAliasKeydown"
            @blur="commitAliasInput"
          />
        </div>
      </div>

      <p v-if="formError" class="error-text">{{ formError }}</p>
      <p v-else-if="notice" class="notice-text">{{ notice }}</p>

      <div class="actions">
        <button type="submit" class="button button-primary" :disabled="saving">
          {{ saving ? "Saving…" : editingId ? "Save changes" : "Add exercise" }}
        </button>
        <button
          v-if="editingId"
          type="button"
          class="button"
          :disabled="saving"
          @click="resetForm"
        >
          Cancel
        </button>
      </div>
    </form>

    <section class="panel">
      <div class="list-header">
        <h2>Catalog ({{ catalog.definitions.length }})</h2>
        <input
          v-model="search"
          type="search"
          class="search"
          placeholder="Search name, ID, muscle group or alias…"
        />
      </div>

      <p v-if="catalog.error" class="error-text">{{ catalog.error }}</p>
      <p
        v-else-if="catalog.loading && catalog.definitions.length === 0"
        class="status-text"
      >
        Loading exercises…
      </p>
      <p v-else-if="filtered.length === 0" class="status-text">
        No exercises match.
      </p>

      <ul v-else class="list">
        <li
          v-for="d in filtered"
          :key="d.id"
          class="row"
          :class="{ editing: d.id === editingId }"
        >
          <div class="row-main">
            <div class="row-title">
              <span class="name">{{ d.name }}</span>
              <span class="badge" :class="{ cardio: d.category === 'Cardio' }">
                {{ d.category }}
              </span>
              <span v-if="d.barbell" class="badge quiet">Barbell</span>
            </div>
            <div class="row-meta">
              <span class="mono">{{ d.id }}</span>
              <span>· {{ d.muscleGroup }}</span>
            </div>
            <div v-if="d.aliases.length > 0" class="row-aliases">
              <span v-for="a in d.aliases" :key="a" class="chip small">
                {{ a }}
              </span>
            </div>
          </div>
          <button type="button" class="button" @click="startEdit(d)">
            Edit
          </button>
        </li>
      </ul>
    </section>
  </div>
</template>

<style scoped>
.exercises-view {
  max-width: 900px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.page-header h1 {
  margin: 0 0 4px;
  font-size: 1.5rem;
  color: var(--text-primary);
}

.subtitle {
  margin: 0;
  font-size: 0.9rem;
  color: var(--text-tertiary);
}

.panel {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 18px;
  background: var(--glass-soft);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);
}

.panel h2 {
  margin: 0;
  font-size: 1rem;
  color: var(--text-primary);
}

.grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
  font-size: 0.75rem;
  color: var(--text-tertiary);
}

.field input,
.field select,
.search {
  height: 34px;
  padding: 0 10px;
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);
  background: rgba(0, 0, 0, 0.18);
  color: var(--text-primary);
  font-size: 0.9rem;
  color-scheme: dark;
  outline: 0;
  box-sizing: border-box;
  width: 100%;
}

.field input:focus,
.field select:focus,
.search:focus {
  border-color: var(--glass-border-strong);
}

.field input:disabled {
  color: var(--text-tertiary);
}

.mono {
  font-family: "JetBrains Mono", monospace;
}

.checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.alias-box {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  min-height: 34px;
  padding: 4px 6px;
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);
  background: rgba(0, 0, 0, 0.18);
  box-sizing: border-box;
}

.alias-box:focus-within {
  border-color: var(--glass-border-strong);
}

.field .alias-input {
  flex: 1 1 180px;
  width: auto;
  height: 26px;
  padding: 0 4px;
  border: 0;
  background: transparent;
}

.chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  border: 1px solid var(--accent-border);
  border-radius: var(--radius-xs);
  background: var(--accent-soft);
  color: var(--accent-text);
  font-size: 0.8rem;
}

.chip.small {
  padding: 1px 6px;
  font-size: 0.72rem;
}

.chip-remove {
  padding: 0;
  border: 0;
  background: none;
  color: inherit;
  font-size: 0.9rem;
  line-height: 1;
  cursor: pointer;
}

.actions {
  display: flex;
  gap: 8px;
}

.button {
  min-height: 32px;
  padding: 0 14px;
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);
  background: var(--glass);
  color: var(--text-secondary);
  font-size: 0.85rem;
  font-weight: 700;
  cursor: pointer;
  flex: 0 0 auto;
}

.button:hover:not(:disabled) {
  border-color: var(--glass-border-strong);
  color: var(--text-primary);
}

.button:disabled {
  opacity: 0.6;
  cursor: default;
}

.button-primary {
  border-color: var(--accent-border);
  background: var(--accent-soft);
  color: var(--accent-text);
}

.list-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.search {
  max-width: 320px;
}

.list {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 4px;
  border-top: 1px solid var(--glass-border);
}

.row.editing {
  background: var(--glass);
}

.row-main {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.row-title {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.name {
  color: var(--text-primary);
  font-weight: 700;
}

.badge {
  padding: 1px 6px;
  border: 1px solid var(--accent-border);
  border-radius: var(--radius-xs);
  color: var(--accent-text);
  font-size: 0.65rem;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.badge.cardio {
  border-color: var(--success-border);
  color: var(--success);
}

.badge.quiet {
  border-color: var(--glass-border);
  color: var(--text-tertiary);
}

.row-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  color: var(--text-tertiary);
  font-size: 0.78rem;
  overflow-wrap: anywhere;
}

.row-aliases {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.status-text {
  margin: 0;
  color: var(--text-tertiary);
  font-size: 0.9rem;
}

.error-text {
  margin: 0;
  color: var(--danger);
  font-size: 0.9rem;
}

.notice-text {
  margin: 0;
  color: var(--success);
  font-size: 0.9rem;
}

@media (max-width: 600px) {
  .grid {
    grid-template-columns: 1fr;
  }

  .search {
    max-width: none;
  }
}
</style>
