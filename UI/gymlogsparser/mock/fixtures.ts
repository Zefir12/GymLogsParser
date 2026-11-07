/**
 * Fixture data for the dev-only mock API (`pnpm dev --mode mock`, `make ui-mock`).
 * Everything is generated relative to today, so the dashboard always has recent data:
 * an admin user, ~5 months of a push/pull/legs/upper split with progressive overload,
 * the exercise catalog, bodyweight measurements and the default weekly targets.
 * Never part of the production build (see vite.config.ts).
 */
import { randomUUID } from "node:crypto";
import type { BodyweightEntry } from "../src/types/bodyweight.ts";
import type { ExerciseDefinition, MuscleShare } from "../src/types/exercise.ts";
import type { MuscleTarget } from "../src/types/muscleTargets.ts";
import type {
  CardioEntry,
  WorkoutExercise,
  WorkoutLog,
  WorkoutSet,
} from "../src/types/workout.ts";

export const mockUser = {
  id: "mock-user-1",
  email: "zefir@example.com",
  name: "Zefir (mock)",
  roles: ["admin", "user"],
};

// ---------------------------------------------------------------------------
// Dates (UTC arithmetic on yyyy-MM-dd strings, so DST never shifts a day)
// ---------------------------------------------------------------------------

export function todayIso(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function addDays(iso: string, days: number): string {
  const [y, m, d] = iso.split("-").map(Number);
  return new Date(Date.UTC(y, m - 1, d + days)).toISOString().slice(0, 10);
}

/** 0 = Monday ... 6 = Sunday. */
export function weekday(iso: string): number {
  const [y, m, d] = iso.split("-").map(Number);
  return (new Date(Date.UTC(y, m - 1, d)).getUTCDay() + 6) % 7;
}

export function dayNumber(iso: string): number {
  const [y, m, d] = iso.split("-").map(Number);
  return Math.round(Date.UTC(y, m - 1, d) / 86_400_000);
}

/** Deterministic PRNG (mulberry32), so the fixtures look the same on every start. */
function rng(seed: number) {
  return () => {
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// ---------------------------------------------------------------------------
// Catalog
// ---------------------------------------------------------------------------

type CatalogRow = [
  id: string,
  name: string,
  muscleGroup: string,
  barbell: boolean,
  aliases: string[],
  secondary: [string, number][],
  category?: "Strength" | "Cardio",
];

const catalogRows: CatalogRow[] = [
  ["bench-press", "Bench Press", "Chest", true, ["wyciskanie na ławce", "bench"], [["Triceps", 0.5], ["Front Delts", 0.5]]],
  ["incline-dumbbell-press", "Incline Dumbbell Press", "Chest", false, ["skos hantle"], [["Front Delts", 0.5], ["Triceps", 0.3]]],
  ["cable-fly", "Cable Fly", "Chest", false, ["rozpiętki na wyciągu"], []],
  ["overhead-press", "Overhead Press", "Front Delts", true, ["OHP", "żołnierskie"], [["Triceps", 0.5], ["Side Delts", 0.3]]],
  ["lateral-raise", "Lateral Raise", "Side Delts", false, ["wznosy bokiem"], []],
  ["face-pull", "Face Pull", "Rear Delts", false, [], [["Upper Back", 0.3]]],
  ["triceps-pushdown", "Triceps Pushdown", "Triceps", false, ["prostowanie na wyciągu"], []],
  ["skull-crusher", "Skull Crusher", "Triceps", false, ["francuskie"], []],
  ["pull-up", "Weighted Pull-up", "Lats", false, ["podciąganie"], [["Biceps", 0.5], ["Upper Back", 0.3]]],
  ["lat-pulldown", "Lat Pulldown", "Lats", false, ["ściąganie drążka"], [["Biceps", 0.5]]],
  ["barbell-row", "Barbell Row", "Upper Back", true, ["wiosłowanie sztangą"], [["Lats", 0.5], ["Biceps", 0.3], ["Rear Delts", 0.3]]],
  ["shrug", "Barbell Shrug", "Traps", true, ["szrugsy"], []],
  ["barbell-curl", "Barbell Curl", "Biceps", true, ["uginanie sztangą"], [["Forearms", 0.3]]],
  ["hammer-curl", "Hammer Curl", "Biceps", false, ["młotki"], [["Forearms", 0.5]]],
  ["back-squat", "Back Squat", "Quadriceps", true, ["przysiad"], [["Glutes", 0.5], ["Adductors", 0.3]]],
  ["leg-press", "Leg Press", "Quadriceps", false, ["suwnica"], [["Glutes", 0.3]]],
  ["romanian-deadlift", "Romanian Deadlift", "Hamstrings", true, ["RDL"], [["Glutes", 0.5], ["Lower Back", 0.5]]],
  ["deadlift", "Deadlift", "Lower Back", true, ["martwy ciąg"], [["Glutes", 0.5], ["Hamstrings", 0.5], ["Traps", 0.3]]],
  ["leg-curl", "Lying Leg Curl", "Hamstrings", false, ["uginanie nóg"], []],
  ["hip-thrust", "Hip Thrust", "Glutes", true, [], [["Hamstrings", 0.3]]],
  ["standing-calf-raise", "Standing Calf Raise", "Calves", false, ["łydki"], []],
  ["adductor-machine", "Adductor Machine", "Adductors", false, ["przywodziciele"], []],
  ["abductor-machine", "Abductor Machine", "Abductors", false, ["odwodziciele"], []],
  ["hanging-leg-raise", "Hanging Leg Raise", "Abs", false, ["unoszenie nóg w zwisie"], []],
  ["neck-curl", "Neck Curl", "Neck", false, [], []],
  ["treadmill", "Treadmill", "Cardio", false, ["bieżnia"], [], "Cardio"],
  ["rowing-machine", "Rowing Machine", "Cardio", false, ["ergometr", "wioślarz"], [], "Cardio"],
  ["bike-warm-up", "Bike Warm-up", "Warm-up", false, ["rower rozgrzewka"], [], "Cardio"],
];

export function createCatalog(): ExerciseDefinition[] {
  return catalogRows.map(([id, name, muscleGroup, barbell, aliases, secondary, category]) => ({
    id,
    name,
    category: category ?? "Strength",
    muscleGroup,
    barbell,
    aliases,
    muscles: [
      { muscleGroup, share: 1 },
      ...secondary.map(([g, share]): MuscleShare => ({ muscleGroup: g, share })),
    ],
  }));
}

// ---------------------------------------------------------------------------
// Muscle targets (copy of MuscleTargetDefaults in Core/Models/MuscleTargets.cs)
// ---------------------------------------------------------------------------

const targetDefaults: [string, number, number][] = [
  ["Chest", 10, 20],
  ["Lats", 10, 20],
  ["Upper Back", 10, 20],
  ["Traps", 4, 12],
  ["Lower Back", 4, 10],
  ["Front Delts", 6, 12],
  ["Side Delts", 10, 20],
  ["Rear Delts", 8, 16],
  ["Biceps", 8, 16],
  ["Triceps", 8, 16],
  ["Forearms", 4, 10],
  ["Abs", 6, 12],
  ["Quadriceps", 10, 20],
  ["Hamstrings", 8, 16],
  ["Glutes", 8, 16],
  ["Calves", 8, 16],
  ["Adductors", 4, 10],
  ["Abductors", 4, 10],
  ["Neck", 0, 8],
];

export function createTargets(): MuscleTarget[] {
  return targetDefaults.map(([muscleGroup, min, max]) => ({
    muscleGroup,
    minSets: min,
    maxSets: max,
    defaultMinSets: min,
    defaultMaxSets: max,
  }));
}

// ---------------------------------------------------------------------------
// Workouts
// ---------------------------------------------------------------------------

/** [exerciseId, sets, minReps, maxReps, start weight kg (null = bodyweight), kg per week] */
type Slot = [string, number, number, number, number | null, number];

const templates: { title: string; slots: Slot[] }[] = [
  {
    title: "Push",
    slots: [
      ["bench-press", 4, 5, 8, 80, 0.6],
      ["incline-dumbbell-press", 3, 8, 12, 28, 0.25],
      ["overhead-press", 3, 6, 8, 45, 0.3],
      ["lateral-raise", 4, 12, 20, 10, 0.1],
      ["triceps-pushdown", 3, 10, 15, 25, 0.25],
      ["cable-fly", 2, 12, 15, 15, 0.1],
    ],
  },
  {
    title: "Pull",
    slots: [
      ["pull-up", 4, 6, 10, 5, 0.3],
      ["barbell-row", 4, 6, 10, 70, 0.5],
      ["lat-pulldown", 3, 10, 12, 60, 0.4],
      ["face-pull", 3, 15, 20, 20, 0.1],
      ["barbell-curl", 3, 8, 12, 30, 0.2],
      ["hammer-curl", 2, 10, 14, 14, 0.1],
      ["shrug", 3, 10, 15, 60, 0.4],
    ],
  },
  {
    title: "Legs",
    slots: [
      ["back-squat", 4, 4, 6, 100, 0.9],
      ["romanian-deadlift", 3, 6, 10, 90, 0.6],
      ["leg-press", 3, 10, 15, 180, 1.5],
      ["leg-curl", 3, 10, 15, 45, 0.3],
      ["standing-calf-raise", 4, 10, 15, 80, 0.5],
      ["adductor-machine", 2, 12, 15, 50, 0.3],
      ["hanging-leg-raise", 3, 10, 15, null, 0],
    ],
  },
  {
    title: "Upper + Deadlift",
    slots: [
      ["deadlift", 3, 3, 5, 140, 1.0],
      ["bench-press", 3, 8, 10, 70, 0.5],
      ["barbell-row", 3, 8, 12, 65, 0.4],
      ["hip-thrust", 3, 8, 12, 100, 0.8],
      ["abductor-machine", 2, 15, 20, 45, 0.3],
      ["skull-crusher", 3, 10, 12, 25, 0.2],
      ["neck-curl", 2, 15, 20, 10, 0.1],
    ],
  },
];

/** Days of the week (0 = Monday) and the template trained on them. */
const schedule: Record<number, number> = { 0: 0, 1: 1, 3: 2, 5: 3 };

const DAYS_BACK = 150;

const notes = [
  "Ciężko dziś, mało snu.",
  "Dobra forma, technika OK.",
  "Lewy bark trochę marudził.",
  null,
  null,
  null,
];

const roundTo = (value: number, step: number) => Math.round(value / step) * step;

export function createWorkouts(catalog: ExerciseDefinition[]): WorkoutLog[] {
  const random = rng(66);
  const byId = new Map(catalog.map((d) => [d.id, d]));
  const today = todayIso();
  const start = addDays(today, -DAYS_BACK);
  const workouts: WorkoutLog[] = [];

  const exercise = (id: string, sets: WorkoutSet[], cardio: CardioEntry[] = []): WorkoutExercise => {
    const def = byId.get(id)!;
    return {
      exerciseId: id,
      name: def.name,
      muscleGroup: def.muscleGroup,
      notes: random() < 0.08 ? notes[Math.floor(random() * notes.length)] : null,
      category: def.category,
      sets,
      cardio,
    };
  };

  const set = (setNumber: number, weight: number | null, reps: number, warmup: boolean): WorkoutSet => ({
    setNumber,
    weight,
    weightEntryMode: weight === null ? null : "Total",
    reps,
    rir: warmup ? null : Math.floor(random() * 3) + 1,
    rpe: null,
    warmup,
    notes: null,
  });

  for (let day = 0; day <= DAYS_BACK; day++) {
    const date = addDays(start, day);
    const template = schedule[weekday(date)];
    if (template === undefined || date > today) continue;
    // A holiday about two months ago, and the odd skipped session.
    if (day > 85 && day < 95) continue;
    if (random() < 0.1) continue;

    const week = Math.floor(day / 7);
    const deload = week % 6 === 5 ? 0.85 : 1;
    const { title, slots } = templates[template];
    const exercises: WorkoutExercise[] = [];

    if (random() < 0.6) {
      const minutes = 5 + Math.floor(random() * 4);
      exercises.push(
        exercise("bike-warm-up", [], [
          { activity: "Rower", durationSeconds: minutes * 60, distanceKm: null, speedKmh: null, inclinePercent: null, notes: null },
        ]),
      );
    }

    slots.forEach(([id, setCount, minReps, maxReps, startWeight, perWeek], index) => {
      const sets: WorkoutSet[] = [];
      const working =
        startWeight === null ? null : Math.max(2.5, roundTo((startWeight + perWeek * week) * deload, 2.5));
      if (index === 0 && working !== null) {
        sets.push(set(1, roundTo(working * 0.5, 2.5), 8, true));
        sets.push(set(2, roundTo(working * 0.75, 2.5), 4, true));
      }
      for (let i = 0; i < setCount; i++) {
        const reps = minReps + Math.floor(random() * (maxReps - minReps + 1)) - (i === setCount - 1 ? 1 : 0);
        sets.push(set(sets.length + 1, working, Math.max(1, reps), false));
      }
      exercises.push(exercise(id, sets));
    });

    if (random() < 0.35) {
      const id = random() < 0.6 ? "treadmill" : "rowing-machine";
      const minutes = 15 + Math.floor(random() * 16);
      const speed = id === "treadmill" ? roundTo(5.5 + random() * 1.5, 0.5) : null;
      exercises.push(
        exercise(id, [], [
          {
            activity: id === "treadmill" ? "Bieżnia" : "Ergometr",
            durationSeconds: minutes * 60,
            distanceKm: id === "treadmill" ? Math.round(((speed ?? 0) * minutes) / 6) / 10 : Math.round(minutes * 2.3) / 10,
            speedKmh: speed,
            inclinePercent: id === "treadmill" ? 6 + Math.floor(random() * 6) : null,
            notes: null,
          },
        ]),
      );
    }

    const startHour = 17 + Math.floor(random() * 3);
    workouts.push({
      id: randomUUID(),
      date,
      title,
      notes: random() < 0.15 ? notes[Math.floor(random() * 3)] : null,
      startTime: `${startHour}:${random() < 0.5 ? "00" : "30"}:00`,
      endTime: `${startHour + 1}:${random() < 0.5 ? "15" : "45"}:00`,
      persons: random() < 0.2 ? ["Kuba"] : [],
      exercises,
    });
  }

  return workouts;
}

// ---------------------------------------------------------------------------
// Bodyweight
// ---------------------------------------------------------------------------

/** A measurement every 3-4 days, slowly cutting from ~84.5 kg. Newest first. */
export function createBodyweight(): BodyweightEntry[] {
  const random = rng(7);
  const today = todayIso();
  const entries: BodyweightEntry[] = [];
  for (let day = DAYS_BACK; day >= 0; day -= 3 + Math.floor(random() * 2)) {
    const progress = (DAYS_BACK - day) / DAYS_BACK;
    const weightKg = Math.round((84.5 - 2.3 * progress + (random() - 0.5) * 0.8) * 10) / 10;
    entries.push({ date: addDays(today, -day), weightKg });
  }
  return entries.reverse();
}

// ---------------------------------------------------------------------------
// AI parse (canned)
// ---------------------------------------------------------------------------

export function cannedParse(catalog: ExerciseDefinition[], perSide: boolean): WorkoutLog {
  const byId = new Map(catalog.map((d) => [d.id, d]));
  const strength = (id: string, weight: number, reps: number[], warmups = 0): WorkoutExercise => {
    const def = byId.get(id)!;
    return {
      exerciseId: id,
      name: def.name,
      muscleGroup: def.muscleGroup,
      notes: null,
      category: "Strength",
      sets: reps.map((r, i): WorkoutSet => ({
        setNumber: i + 1,
        weight: i < warmups ? Math.round(weight * 0.6) : weight,
        weightEntryMode: id === "bench-press" && perSide ? "PerSide" : "Total",
        reps: r,
        rir: i < warmups ? null : 2,
        rpe: null,
        warmup: i < warmups,
        notes: null,
      })),
      cardio: [],
    };
  };
  return {
    date: todayIso(),
    title: "Push",
    notes: null,
    startTime: "18:00:00",
    endTime: "19:20:00",
    persons: [],
    exercises: [
      strength("bench-press", perSide ? 36.25 : 92.5, [8, 6, 6, 5, 5], 1),
      strength("overhead-press", 50, [8, 7, 6]),
      strength("lateral-raise", 12, [15, 14, 12, 12]),
      strength("triceps-pushdown", 30, [12, 12, 10]),
      {
        exerciseId: "treadmill",
        name: "Treadmill",
        muscleGroup: "Cardio",
        notes: "Incline walk",
        category: "Cardio",
        sets: [],
        cardio: [{ activity: "Bieżnia", durationSeconds: 1200, distanceKm: 2, speedKmh: 6, inclinePercent: 10, notes: null }],
      },
    ],
  };
}
