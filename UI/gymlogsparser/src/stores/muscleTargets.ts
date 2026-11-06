import { computed, ref } from "vue";
import { defineStore } from "pinia";

import { getMuscleTargets, updateMuscleTargets } from "@/api/client";
import type { MuscleTarget, MuscleTargetUpdate } from "@/types/muscleTargets";

/**
 * Weekly target ranges per muscle group (sets/wk), shown on the dashboard's weekly
 * sets per body part and the body-part tiles. Groups without a target (Cardio,
 * Warm-up, anything unknown) get no band and no status.
 */
export const useMuscleTargetsStore = defineStore("muscleTargets", () => {
  const targets = ref<MuscleTarget[]>([]);
  const error = ref<string | null>(null);

  let pending: Promise<void> | null = null;
  let loaded = false;

  async function fetchAll() {
    error.value = null;
    try {
      targets.value = await getMuscleTargets();
      loaded = true;
    } catch (err) {
      error.value =
        err instanceof Error ? err.message : "Could not load the targets.";
    } finally {
      pending = null;
    }
  }

  /** Loads the targets once; later calls reuse them (or the request in flight). */
  function ensureLoaded(): Promise<void> {
    if (loaded) return Promise.resolve();
    pending ??= fetchAll();
    return pending;
  }

  /** Saves the ranges and takes the server's full list. Throws on failure. */
  async function save(updates: MuscleTargetUpdate[]) {
    targets.value = await updateMuscleTargets(updates);
    loaded = true;
    error.value = null;
  }

  const byGroup = computed(
    () => new Map(targets.value.map((t) => [t.muscleGroup, t])),
  );

  function targetOf(group: string): MuscleTarget | undefined {
    return byGroup.value.get(group);
  }

  return { targets, error, ensureLoaded, save, targetOf };
});
