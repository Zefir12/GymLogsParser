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
}
