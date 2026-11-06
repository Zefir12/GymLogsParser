export type ExerciseCategory = "Strength" | "Cardio";

/** One set of the exercise counts `share` sets towards `muscleGroup`. */
export interface MuscleShare {
  muscleGroup: string;
  /** 0.05–1; the primary group is always 1. */
  share: number;
}

/** One entry of the stored exercise catalog (`/api/exercises/definitions`). */
export interface ExerciseDefinition {
  /** Stable kebab-case ID. Empty on create means "derive it from the name". */
  id: string;
  name: string;
  category: ExerciseCategory;
  /** Primary muscle group (share 1). */
  muscleGroup: string;
  barbell: boolean;
  /** Other names written in logs; the parser maps them to this exercise. */
  aliases: string[];
  /**
   * Every group a set counts towards: the primary first (share 1), then the
   * secondary groups. On save the server ignores the primary's entry.
   */
  muscles: MuscleShare[];
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
