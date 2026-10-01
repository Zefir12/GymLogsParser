export type ExerciseCategory = "Strength" | "Cardio";

export type WeightEntryMode = "Total" | "PerSide";

export interface WorkoutSet {
  setNumber: number | null;
  weight: number | null;
  weightEntryMode: WeightEntryMode | null;
  reps: number | null;
  rir: number | null;
  rpe: number | null;
  warmup: boolean | null;
  notes: string | null;
}

export interface CardioEntry {
  activity: string;
  durationSeconds: number | null;
  distanceKm: number | null;
  speedKmh: number | null;
  inclinePercent: number | null;
  notes: string | null;
}

export interface WorkoutExercise {
  exerciseId: string;
  name: string;
  muscleGroup: string | null;
  notes: string | null;
  category: ExerciseCategory;
  sets: WorkoutSet[];
  cardio: CardioEntry[];
}

export interface WorkoutLog {
  date: string | null;
  title: string | null;
  notes: string | null;
  exercises: WorkoutExercise[];
}

export interface AiUsage {
  cacheHit: boolean;
  cacheHitTokens: number;
  cacheMissTokens: number;
  inputTokens: number;
  outputTokens: number;
}

export interface ParseWorkoutResponse {
  workout: WorkoutLog;
  usage: AiUsage;
}

export interface WorkoutExercise {
  exerciseId: string;
  name: string;
  muscleGroup: string | null;
  notes: string | null;
  category: ExerciseCategory;
  sets: WorkoutSet[];
  cardio: CardioEntry[];
}

export interface WorkoutLog {
  /** Present on GetById/List responses; absent (ignored) when POSTing a new workout. */
  id?: string;
  date: string | null; // "YYYY-MM-DD" (DateOnly)
  title: string | null;
  notes: string | null;
  startTime: string | null; // "HH:mm:ss" (TimeOnly) or null
  endTime: string | null;
  persons: string[];
  exercises: WorkoutExercise[];
}

/** One row for the workouts list screen (GET /api/workouts). */
export interface WorkoutSummary {
  id: string;
  date: string | null;
  title: string | null;
  exerciseCount: number;
  totalSets: number;
  hasCardio: boolean;
  muscleGroups: string[];
}
