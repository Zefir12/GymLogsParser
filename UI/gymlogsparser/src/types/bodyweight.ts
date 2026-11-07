/** One bodyweight measurement of the signed-in user (`/api/bodyweight-entries`). */
export interface BodyweightEntry {
  /** yyyy-MM-dd; the key, one measurement per day. */
  date: string;
  weightKg: number;
}

/** Bounds the API enforces (Core/Models/BodyweightEntry.cs). */
export const BODYWEIGHT_MIN_KG = 20;
export const BODYWEIGHT_MAX_KG = 400;
