<script setup lang="ts">
import { nextTick } from "vue";
import { storeToRefs } from "pinia";

import { useWorkoutStore } from "@/stores/workout";

import AppHeader from "@/components/AppHeader.vue";
import WorkoutInputPanel from "@/components/WorkoutInputPanel.vue";
import WorkoutEditorPanel from "@/components/WorkoutEditorPanel.vue";

const store = useWorkoutStore();
const { rawText, workout, loading, barbellWeightsArePerSide } =
  storeToRefs(store);

async function parseWorkout() {
  await store.parse();
  await nextTick();

  if (workout.value.exercises.length > 0) {
    document.querySelector(".editor-panel")?.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
  }
}
</script>

<template>
  <main class="app-shell">
    <AppHeader @new-workout="store.newWorkout" />

    <section class="workspace">
      <WorkoutInputPanel
        v-model:raw-text="rawText"
        v-model:barbell-weights-are-per-side="barbellWeightsArePerSide"
        :loading="loading"
        @parse="parseWorkout"
      />

      <WorkoutEditorPanel />
    </section>
  </main>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
  padding: 24px;

  color: var(--text-secondary);
}

.workspace {
  max-width: 1500px;
  margin: 0 auto;

  display: grid;
  grid-template-columns:
    minmax(350px, 0.75fr)
    minmax(650px, 1.25fr);

  gap: 18px;

  align-items: start;
}

@media (max-width: 1100px) {
  .workspace {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 700px) {
  .app-shell {
    padding: 10px;
  }
}
</style>

<style>
@import "@/styles/tokens.css";
</style>
