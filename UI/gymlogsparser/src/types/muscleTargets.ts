/** Weekly target range of one muscle group (`/api/muscle-targets`), in sets per week. */
export interface MuscleTarget {
  muscleGroup: string;
  minSets: number;
  maxSets: number;
  /** What "reset to default" restores. Read-only. */
  defaultMinSets: number;
  defaultMaxSets: number;
}

/** What `PUT /api/muscle-targets` takes per group. */
export type MuscleTargetUpdate = Pick<
  MuscleTarget,
  "muscleGroup" | "minSets" | "maxSets"
>;
