import { ref } from "vue";
import { defineStore } from "pinia";

import {
  addBodyweightEntry,
  deleteBodyweightEntry,
  getBodyweightEntries,
  updateBodyweightEntry,
} from "@/api/client";
import type { BodyweightEntry } from "@/types/bodyweight";

const newestFirst = (a: BodyweightEntry, b: BodyweightEntry) =>
  b.date.localeCompare(a.date);

/**
 * The signed-in user's bodyweight measurements (#46), newest first. They feed
 * bodyweight and DOTS on Progress and the parser's bodyweight hint. Writes throw on
 * failure (with the API's message) so the screen can show it next to the form.
 */
export const useBodyweightStore = defineStore("bodyweight", () => {
  const entries = ref<BodyweightEntry[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);

  async function load() {
    loading.value = true;
    error.value = null;
    try {
      entries.value = (await getBodyweightEntries()).sort(newestFirst);
    } catch (err) {
      error.value =
        err instanceof Error ? err.message : "Could not load your measurements.";
    } finally {
      loading.value = false;
    }
  }

  async function add(entry: BodyweightEntry) {
    const saved = await addBodyweightEntry(entry);
    entries.value = [...entries.value, saved].sort(newestFirst);
  }

  async function update(date: string, entry: BodyweightEntry) {
    const saved = await updateBodyweightEntry(date, entry);
    entries.value = [
      ...entries.value.filter((e) => e.date !== date),
      saved,
    ].sort(newestFirst);
  }

  async function remove(date: string) {
    await deleteBodyweightEntry(date);
    entries.value = entries.value.filter((e) => e.date !== date);
  }

  return { entries, loading, error, load, add, update, remove };
});
