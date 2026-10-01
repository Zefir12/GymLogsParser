import { api } from "@/api/api";
import type { ParseWorkoutResponse, WorkoutLog } from "@/types/workout";
import type { ExerciseProgressPoint, ExerciseSummary } from "@/types/exercise";

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

export const getExerciseProgress = (exerciseId: string) =>
  api.get<ExerciseProgressPoint[]>(
    `/exercises/${encodeURIComponent(exerciseId)}/progress`,
  );

export interface WorkoutSummary {
  id: string;
  date: string | null;
  title: string | null;
  exerciseCount: number;
  totalSets: number;
  hasCardio: boolean;
  muscleGroups: string[];
}
