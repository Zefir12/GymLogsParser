import { ref } from "vue";
import { defineStore } from "pinia";

import { importHevy, previewHevyImport } from "@/api/client";
import type { HevyImportSummary } from "@/types/import";

const message = (err: unknown, fallback: string) =>
  err instanceof Error && err.message ? err.message : fallback;

/**
 * Importing a Hevy CSV export (#77): the file is previewed first (exercise matches,
 * duplicates, bad rows), the user maps the unmatched exercises, then it is imported.
 * `mappings` holds the user's choices: catalog ID, or "" to skip the exercise.
 */
export const useHevyImportStore = defineStore("hevyImport", () => {
  const fileName = ref<string | null>(null);
  const csv = ref<string | null>(null);
  const preview = ref<HevyImportSummary | null>(null);
  const result = ref<HevyImportSummary | null>(null);
  const mappings = ref<Record<string, string>>({});
  const busy = ref(false);
  const error = ref<string | null>(null);

  function reset() {
    fileName.value = null;
    csv.value = null;
    preview.value = null;
    result.value = null;
    mappings.value = {};
    error.value = null;
  }

  /** Request mappings: every title, so the server uses exactly what the screen shows. */
  function requestMappings(): Record<string, string | null> {
    return Object.fromEntries(
      Object.entries(mappings.value).map(([title, id]) => [title, id || null]),
    );
  }

  async function load(name: string, text: string) {
    reset();
    fileName.value = name;
    csv.value = text;
    busy.value = true;
    try {
      const summary = await previewHevyImport({ csv: text });
      preview.value = summary;
      mappings.value = Object.fromEntries(
        summary.exercises.map((e) => [e.title, e.exerciseId ?? ""]),
      );
    } catch (err) {
      error.value = message(err, "Could not read the file.");
    } finally {
      busy.value = false;
    }
  }

  async function runImport() {
    if (csv.value === null) return;
    busy.value = true;
    error.value = null;
    try {
      result.value = await importHevy({
        csv: csv.value,
        mappings: requestMappings(),
      });
    } catch (err) {
      error.value = message(err, "Could not import the workouts.");
    } finally {
      busy.value = false;
    }
  }

  return {
    fileName,
    csv,
    preview,
    result,
    mappings,
    busy,
    error,
    reset,
    load,
    runImport,
  };
});
