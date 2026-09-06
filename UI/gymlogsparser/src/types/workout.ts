export type ExerciseCategory = "Strength" | "Cardio";

export type WeightEntryMode = "Total" | "PerSide";

export interface WorkoutSet {
  setNumber: number | null;
  weight: number | null;
  weightEntryMode: WeightEntryMode;
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
