import type { ParseWorkoutResponse, WorkoutLog } from "@/types/workout";

const API_BASE_URL =
  import.meta.env.VITE_API_URL ?? "http://localhost:5238/api";

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: {
      "Content-Type": "application/json",
      ...options?.headers,
    },
    ...options,
  });

  if (!response.ok) {
    let message = `Request failed: ${response.status}`;

    try {
      const body = await response.json();

      if (body?.error) {
        message = body.error;
      }
    } catch {
      // Keep default error.
    }

    throw new Error(message);
  }

  return response.json() as Promise<T>;
}

export async function parseWorkout(
  text: string,
  barbellWeightsArePerSide: boolean,
): Promise<ParseWorkoutResponse> {
  return request<ParseWorkoutResponse>("/ai/parse-workout", {
    method: "POST",
    body: JSON.stringify({
      text,
      barbellWeightsArePerSide,
    }),
  });
}
/**
 * Adapt this endpoint to your existing EF controller.
 */
export async function saveWorkout(workout: WorkoutLog): Promise<WorkoutLog> {
  return request<WorkoutLog>("/workouts", {
    method: "POST",
    body: JSON.stringify(workout),
  });
}
