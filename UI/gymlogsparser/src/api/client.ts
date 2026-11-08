import { api } from "@/api/api";
import type { ParseWorkoutResponse, WorkoutLog } from "@/types/workout";
import type {
  ExerciseDefinition,
  ExerciseProgressPoint,
  ExerciseSummary,
} from "@/types/exercise";
import type { DashboardData, DashboardRange } from "@/types/dashboard";
import type { MuscleTarget, MuscleTargetUpdate } from "@/types/muscleTargets";
import type { BodyweightEntry } from "@/types/bodyweight";
import type { HevyImportRequest, HevyImportSummary } from "@/types/import";

export const parseWorkout = (text: string, barbellWeightsArePerSide: boolean) =>
  api.post<ParseWorkoutResponse>("/ai/parse-workout", {
    text,
    barbellWeightsArePerSide,
  });

export const saveWorkout = (workout: WorkoutLog) =>
  api.post<WorkoutLog>("/workouts", workout);

export const getWorkouts = () => api.get<WorkoutSummary[]>("/workouts");

export const getWorkout = (id: string) =>
  api.get<WorkoutLog>(`/workouts/${encodeURIComponent(id)}`);

export const updateWorkout = (id: string, workout: WorkoutLog) =>
  api.put(`/workouts/${encodeURIComponent(id)}`, workout);

export const deleteWorkout = (id: string) =>
  api.delete(`/workouts/${encodeURIComponent(id)}`);

export const getExercises = () => api.get<ExerciseSummary[]>("/exercises");

export const getExerciseDefinitions = () =>
  api.get<ExerciseDefinition[]>("/exercises/definitions");

export const createExerciseDefinition = (definition: ExerciseDefinition) =>
  api.post<ExerciseDefinition>("/exercises/definitions", definition);

export const updateExerciseDefinition = (
  id: string,
  definition: ExerciseDefinition,
) =>
  api.put<ExerciseDefinition>(
    `/exercises/definitions/${encodeURIComponent(id)}`,
    definition,
  );

export const getExerciseProgress = (exerciseId: string) =>
  api.get<ExerciseProgressPoint[]>(
    `/exercises/${encodeURIComponent(exerciseId)}/progress`,
  );

/**
 * `to` is the client's local today (yyyy-MM-dd), so "this week" matches the user's
 * calendar. The response always covers at least 53 weeks, more if `range` needs it.
 */
export const getDashboard = (to: string, range: DashboardRange) =>
  api.get<DashboardData>(
    `/dashboard?to=${encodeURIComponent(to)}&range=${encodeURIComponent(range)}`,
  );

/** Every muscle group (not Cardio or Warm-up) with its weekly target range. */
export const getMuscleTargets = () => api.get<MuscleTarget[]>("/muscle-targets");

/** Stores the listed groups' ranges and returns the full list. */
export const updateMuscleTargets = (targets: MuscleTargetUpdate[]) =>
  api.put<MuscleTarget[]>("/muscle-targets", targets);

/** The signed-in user's bodyweight measurements, newest first. */
export const getBodyweightEntries = () =>
  api.get<BodyweightEntry[]>("/bodyweight-entries");

/** Adds a measurement; 409 when there is already one on that date. */
export const addBodyweightEntry = (entry: BodyweightEntry) =>
  api.post<BodyweightEntry>("/bodyweight-entries", entry);

/** Changes the measurement on `date`; `entry.date` may differ to move it. */
export const updateBodyweightEntry = (date: string, entry: BodyweightEntry) =>
  api.put<BodyweightEntry>(
    `/bodyweight-entries/${encodeURIComponent(date)}`,
    entry,
  );

export const deleteBodyweightEntry = (date: string) =>
  api.delete(`/bodyweight-entries/${encodeURIComponent(date)}`);

/** Dry run of a Hevy CSV import: what would be imported, and the exercise matches. */
export const previewHevyImport = (request: HevyImportRequest) =>
  api.post<HevyImportSummary>("/imports/hevy/preview", request);

/** Imports a Hevy CSV export; workouts the user already has are skipped. */
export const importHevy = (request: HevyImportRequest) =>
  api.post<HevyImportSummary>("/imports/hevy", request);

export interface WorkoutSummary {
  id: string;
  date: string | null;
  title: string | null;
  exerciseCount: number;
  totalSets: number;
  hasCardio: boolean;
  muscleGroups: string[];
}
