import { ref } from "vue";
import { defineStore } from "pinia";

import { exportHevy } from "@/api/client";

/**
 * Exporting the user's workouts as a Hevy CSV (#80), to import into embr
 * (Training → Import) or back into this app. `from`/`to` (yyyy-MM-dd) are optional;
 * the API refuses an export over the imports' 10 MB limit and asks for a shorter range.
 */
export const useHevyExportStore = defineStore("hevyExport", () => {
  const from = ref("");
  const to = ref("");
  const busy = ref(false);
  const error = ref<string | null>(null);

  /** The file, or null when the export failed (`error` says why). */
  async function download(): Promise<Blob | null> {
    busy.value = true;
    error.value = null;
    try {
      return await exportHevy(from.value || undefined, to.value || undefined);
    } catch (err) {
      error.value =
        err instanceof Error && err.message ? err.message : "Could not export your workouts.";
      return null;
    } finally {
      busy.value = false;
    }
  }

  return { from, to, busy, error, download };
});
