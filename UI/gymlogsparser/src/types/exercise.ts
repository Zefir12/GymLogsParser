export type ExerciseCategory = "Strength" | "Cardio";

/** One entry of the stored exercise catalog (`/api/exercises/definitions`). */
export interface ExerciseDefinition {
  /** Stable kebab-case ID. Empty on create means "derive it from the name". */
  id: string;
  name: string;
  category: ExerciseCategory;
  muscleGroup: string;
  barbell: boolean;
  /** Other names written in logs; the parser maps them to this exercise. */
  aliases: string[];
}

export interface ExerciseSummary {
  exerciseId: string;
  name: string;
  muscleGroup: string | null;
}

export interface ExerciseProgressPoint {
  workoutId: string;
  /** ISO date string (yyyy-MM-dd), or null if the workout had no date set. */
  date: string | null;
  maxWeight: number;
  totalVolume: number;
  estimatedOneRepMax: number;
  /** Bodyweight (kg) interpolated from history for that date, null if no date. */
  bodyweight: number | null;
  /** DOTS score of the session's estimated 1RM at that bodyweight, null if no date. */
  dots: number | null;
}
