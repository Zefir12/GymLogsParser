/**
 * The one flat list of body parts an exercise can train: no parent categories such
 * as "Legs", "Shoulders" or "Back". Order is the display order (upper body, arms,
 * core, lower body, neck, then Cardio and Warm-up). Keep in sync with
 * Core/Models/MuscleGroups.cs, which rejects anything else in the catalog.
 */
export const MUSCLE_GROUPS = [
  "Chest",
  "Lats",
  "Upper Back",
  "Traps",
  "Lower Back",
  "Front Delts",
  "Side Delts",
  "Rear Delts",
  "Biceps",
  "Triceps",
  "Forearms",
  "Abs",
  "Quadriceps",
  "Hamstrings",
  "Glutes",
  "Calves",
  "Adductors",
  "Abductors",
  "Neck",
  "Cardio",
  "Warm-up",
] as const;

export type MuscleGroup = (typeof MUSCLE_GROUPS)[number];

/**
 * Options for a muscle-group picker: the fixed list, plus `current` when it is set
 * but not in the list (an old value), so opening an editor never silently drops it.
 */
export function muscleGroupOptions(current?: string | null): string[] {
  const list: string[] = [...MUSCLE_GROUPS];
  if (current && !list.includes(current)) list.push(current);
  return list;
}

/**
 * Groups an exercise can count towards as a secondary muscle: every real muscle
 * group, so not Cardio or Warm-up. Mirrors MuscleGroups.IsMuscle on the server.
 */
export function isMuscle(group: string | null | undefined): boolean {
  return (
    !!group &&
    group !== "Cardio" &&
    group !== "Warm-up" &&
    (MUSCLE_GROUPS as readonly string[]).includes(group)
  );
}

export const MUSCLES: readonly string[] = MUSCLE_GROUPS.filter((g) => isMuscle(g));
