import { ref } from "vue";
import { defineStore } from "pinia";

import {
  createExerciseDefinition,
  getExerciseDefinitions,
  updateExerciseDefinition,
} from "@/api/client";
import type { ExerciseDefinition } from "@/types/exercise";

/** The stored exercise catalog, shared by the workout editor and the Exercises page. */
export const useExerciseCatalogStore = defineStore("exerciseCatalog", () => {
  const definitions = ref<ExerciseDefinition[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);

  let pending: Promise<void> | null = null;
  let loaded = false;

  async function fetchAll() {
    loading.value = true;
    error.value = null;
    try {
      definitions.value = await getExerciseDefinitions();
      loaded = true;
    } catch (err) {
      error.value =
        err instanceof Error ? err.message : "Could not load exercises.";
    } finally {
      loading.value = false;
      pending = null;
    }
  }

  /** Loads the catalog once; later calls reuse it (or the request in flight). */
  function ensureLoaded(): Promise<void> {
    if (loaded) return Promise.resolve();
    pending ??= fetchAll();
    return pending;
  }

  function reload(): Promise<void> {
    pending ??= fetchAll();
    return pending;
  }

  function upsert(definition: ExerciseDefinition) {
    const index = definitions.value.findIndex((d) => d.id === definition.id);
    if (index >= 0) definitions.value.splice(index, 1, definition);
    else definitions.value.push(definition);
  }

  async function create(definition: ExerciseDefinition) {
    const created = await createExerciseDefinition(definition);
    upsert(created);
    return created;
  }

  async function update(id: string, definition: ExerciseDefinition) {
    const updated = await updateExerciseDefinition(id, definition);
    upsert(updated);
    return updated;
  }

  function byId(id: string) {
    return definitions.value.find((d) => d.id === id);
  }

  return {
    definitions,
    loading,
    error,
    ensureLoaded,
    reload,
    create,
    update,
    byId,
  };
});
