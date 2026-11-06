import type { DashboardExercise, DashboardRange } from "@/types/dashboard";
import type { ExerciseDefinition, MuscleShare } from "@/types/exercise";
import { MUSCLE_GROUPS, isMuscle } from "@/utils/muscleGroups";

/** Weekly working-set target band per muscle group. */
export const TARGET_MIN = 10;
export const TARGET_MAX = 20;

export const RANGE_DAYS: Record<DashboardRange, number> = {
  "3M": 91,
  "6M": 182,
  "1Y": 365,
  "5Y": 1826,
  "10Y": 3653,
};

/** Ranges whose progress chart labels years instead of months. */
export const YEAR_TICK_RANGES: readonly DashboardRange[] = ["5Y", "10Y"];

export const RANGE_LABEL: Record<DashboardRange, string> = {
  "3M": "last 3 months",
  "6M": "last 6 months",
  "1Y": "last 12 months",
  "5Y": "last 5 years",
  "10Y": "last 10 years",
};

export const ALL = "All";
export const CARDIO = "Cardio";

/** Shown in the exercises table when "All body parts" is selected. */
export const MAIN_LIFT_IDS = [
  "barbell-bench-press",
  "overhead-press",
  "barbell-deadlift",
  "barbell-row",
  "barbell-squat",
  "romanian-deadlift",
];

/** Tile/bar order follows the muscle-group list; anything else follows alphabetically, Cardio last. */
const GROUP_ORDER: readonly string[] = MUSCLE_GROUPS.filter((g) => g !== CARDIO);

export function sortGroups(groups: Iterable<string>): string[] {
  const rank = (g: string) => {
    if (g === CARDIO) return Number.MAX_SAFE_INTEGER;
    const i = GROUP_ORDER.indexOf(g);
    return i === -1 ? GROUP_ORDER.length : i;
  };
  return [...new Set(groups)].sort(
    (a, b) => rank(a) - rank(b) || a.localeCompare(b),
  );
}

export function groupOf(exercise: DashboardExercise | undefined): string {
  if (!exercise) return "Other";
  if (exercise.category === "Cardio") return CARDIO;
  return exercise.muscleGroup || "Other";
}

/**
 * The groups one set of `exercise` counts towards: its logged group at share 1, then
 * the secondary groups of its catalog entry (e.g. bench press: Chest 1, Triceps 0.5,
 * Front Delts 0.5). Cardio, and exercises not in the catalog, only count their own group.
 */
export function muscleShares(
  exercise: DashboardExercise | undefined,
  definition: ExerciseDefinition | undefined,
): MuscleShare[] {
  const primary = groupOf(exercise);
  if (primary === CARDIO || definition?.category !== "Strength") {
    return [{ muscleGroup: primary, share: 1 }];
  }
  const secondary = (definition.muscles ?? []).filter(
    (m) => m.muscleGroup !== primary && isMuscle(m.muscleGroup) && m.share > 0,
  );
  return [{ muscleGroup: primary, share: 1 }, ...secondary];
}

/** Rounds away float noise from summing fractional sets (0.3 + 0.3 + 0.3). */
export function roundSets(value: number): number {
  return Math.round(value * 100) / 100;
}

export type TargetStatus = "Below target" | "On target" | "Above target";

export function targetStatus(setsPerWeek: number): TargetStatus {
  if (setsPerWeek < TARGET_MIN) return "Below target";
  if (setsPerWeek > TARGET_MAX) return "Above target";
  return "On target";
}

// --- dates: whole days since the epoch, in UTC so DST never shifts a day ---

const DAY_MS = 86_400_000;

export function dayNumber(iso: string): number {
  const [y, m, d] = iso.split("-").map(Number);
  return Math.round(Date.UTC(y, m - 1, d) / DAY_MS);
}

export function dayDate(day: number): Date {
  return new Date(day * DAY_MS);
}

/** Local today as yyyy-MM-dd. */
export function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

const MONTHS = [
  "Jan",
  "Feb",
  "Mar",
  "Apr",
  "May",
  "Jun",
  "Jul",
  "Aug",
  "Sep",
  "Oct",
  "Nov",
  "Dec",
];
const WEEKDAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

export function monthName(day: number): string {
  return MONTHS[dayDate(day).getUTCMonth()];
}

/** e.g. "Mon 3 Nov" */
export function formatDay(day: number): string {
  const d = dayDate(day);
  return `${WEEKDAYS[d.getUTCDay()]} ${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]}`;
}

/** Day number of 1 January of the year after `day`. */
export function nextYearStart(day: number): number {
  return Math.round(Date.UTC(dayDate(day).getUTCFullYear() + 1, 0, 1) / DAY_MS);
}

/** Day number of the Monday of the week that contains `day`. */
export function weekStart(day: number): number {
  return day - ((dayDate(day).getUTCDay() + 6) % 7);
}

/** Day number of the first of the month after `day`. */
export function nextMonthStart(day: number): number {
  const d = dayDate(day);
  return Math.round(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 1) / DAY_MS);
}

// --- numbers ---

export function formatNumber(value: number): string {
  return Math.abs(value) >= 1000
    ? Math.round(value).toLocaleString("en-US")
    : String(Math.round(value * 10) / 10);
}

export function formatKg(value: number): string {
  return `${formatNumber(value)} kg`;
}

/** "+1.5" / "−1.5" with a real minus sign. */
export function signed(value: number, digits = 1): string {
  return `${value >= 0 ? "+" : "−"}${Math.abs(value).toFixed(digits)}`;
}
