/** Response of GET /api/dashboard. Dates are ISO `yyyy-MM-dd` strings. */
export interface DashboardData {
  /** Monday that starts the first of the 53 covered weeks. */
  from: string;
  /** Last covered day (the client's today). */
  to: string;
  exercises: DashboardExercise[];
  /** Days with at least one logged exercise, oldest first. */
  days: DashboardDay[];
}

export interface DashboardExercise {
  exerciseId: string;
  name: string;
  muscleGroup: string | null;
  category: "Strength" | "Cardio";
}

export interface DashboardDay {
  date: string;
  title: string | null;
  entries: DashboardDayEntry[];
}

/** One exercise on one day; warm-up sets are already excluded. */
export interface DashboardDayEntry {
  exerciseId: string;
  /** Working sets (strength) or cardio entries. */
  sets: number;
  /** Cardio minutes (0 for strength). */
  minutes: number;
  topSet: number;
  estimatedOneRepMax: number;
  volume: number;
  pr: boolean;
}

export type DashboardRange = "3M" | "6M" | "1Y" | "5Y" | "10Y";
export type DashboardMetric = "e1rm" | "top" | "volume";
