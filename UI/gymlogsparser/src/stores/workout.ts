import { computed, ref } from "vue";
import { defineStore } from "pinia";

import { parseWorkout, saveWorkout } from "@/api/client";

import type {
  AiUsage,
  WorkoutExercise,
  WorkoutLog,
  WorkoutSet,
} from "@/types/workout";

function emptyWorkout(): WorkoutLog {
  return {
    date: null,
    title: null,
    notes: null,
    exercises: [],
  };
}

function createSet(): WorkoutSet {
  return {
    setNumber: null,
    weight: null,
    weightEntryMode: "Total",
    reps: null,
    rir: null,
    rpe: null,
    warmup: null,
    notes: null,
  };
}

export const useWorkoutStore = defineStore("workout", () => {
  const rawText = ref("");

  const workout = ref<WorkoutLog>(emptyWorkout());

  /**
   * User's interpretation setting for barbell notation.
   *
   * false:
   *   100kg = 100kg total
   *
   * true:
   *   40kg = 40kg each side + 20kg bar = 100kg
   */
  const barbellWeightsArePerSide = ref(false);

  const loading = ref(false);
  const saving = ref(false);

  const error = ref<string | null>(null);
  const success = ref<string | null>(null);

  const usage = ref<AiUsage | null>(null);

  const hasWorkout = computed(() => workout.value.exercises.length > 0);

  const exerciseCount = computed(() => workout.value.exercises.length);

  const strengthCount = computed(
    () =>
      workout.value.exercises.filter((x) => x.category === "Strength").length,
  );

  const cardioCount = computed(
    () => workout.value.exercises.filter((x) => x.category === "Cardio").length,
  );

  const setCount = computed(() =>
    workout.value.exercises.reduce(
      (total, exercise) => total + exercise.sets.length,
      0,
    ),
  );

  async function parse() {
    if (!rawText.value.trim()) {
      error.value = "Paste a workout log first.";
      return;
    }

    loading.value = true;
    error.value = null;
    success.value = null;

    try {
      const response = await parseWorkout(
        rawText.value,
        barbellWeightsArePerSide.value,
      );

      workout.value = response.workout;

      usage.value = response.usage;

      success.value = "Workout parsed.";
    } catch (err) {
      error.value =
        err instanceof Error ? err.message : "Could not parse workout.";
    } finally {
      loading.value = false;
    }
  }

  async function save() {
    saving.value = true;
    error.value = null;
    success.value = null;

    try {
      await saveWorkout(workout.value);

      success.value = "Workout saved.";
    } catch (err) {
      error.value =
        err instanceof Error ? err.message : "Could not save workout.";
    } finally {
      saving.value = false;
    }
  }

  function newWorkout() {
    rawText.value = "";
    workout.value = emptyWorkout();

    usage.value = null;
    error.value = null;
    success.value = null;
  }

  function addExercise() {
    workout.value.exercises.push({
      exerciseId: "",
      name: "",
      muscleGroup: null,
      notes: null,
      category: "Strength",
      sets: [createSet()],
      cardio: [],
    });
  }

  function removeExercise(index: number) {
    workout.value.exercises.splice(index, 1);
  }

  function addSet(exercise: WorkoutExercise) {
    const nextNumber = exercise.sets.length + 1;

    exercise.sets.push({
      ...createSet(),
      setNumber: nextNumber,
    });
  }

  function removeSet(exercise: WorkoutExercise, index: number) {
    exercise.sets.splice(index, 1);

    exercise.sets.forEach((set, setIndex) => {
      set.setNumber = setIndex + 1;
    });
  }

  return {
    rawText,
    workout,

    barbellWeightsArePerSide,

    loading,
    saving,

    error,
    success,
    usage,

    hasWorkout,
    exerciseCount,
    strengthCount,
    cardioCount,
    setCount,

    parse,
    save,
    newWorkout,

    addExercise,
    removeExercise,
    addSet,
    removeSet,
  };
});
