export interface ExerciseDefinition {
  id: string;
  name: string;
  category: "Strength" | "Cardio";
  muscleGroup: string;
  barbell: boolean;
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
