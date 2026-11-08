/** Body of `POST /api/imports/hevy` and `/hevy/preview` (#77). */
export interface HevyImportRequest {
  /** The whole CSV file as text. */
  csv: string;
  /**
   * Hevy exercise title → catalog ID. A missing title uses the automatic match;
   * null skips the exercise's sets.
   */
  mappings?: Record<string, string | null>;
}

export interface HevyImportExercise {
  /** Hevy `exercise_title`, e.g. "Bench Press (Barbell)". */
  title: string;
  sets: number;
  /** Catalog ID the sets go to; null when they are skipped. */
  exerciseId: string | null;
  autoMatched: boolean;
}

export interface HevyImportRowError {
  /** 1-based line in the file (the header is line 1). */
  line: number;
  error: string;
}

/** What the import did, or (preview) would do. */
export interface HevyImportSummary {
  imported: boolean;
  workoutsInFile: number;
  workouts: number;
  sets: number;
  duplicateWorkouts: number;
  unmappedSets: number;
  exercises: HevyImportExercise[];
  /** The first 200 bad rows; `errorCount` is the total. */
  errors: HevyImportRowError[];
  errorCount: number;
}

/** The API's limit on the file's length in characters (ImportsController.MaxCsvLength). */
export const HEVY_MAX_CSV_LENGTH = 10_000_000;
