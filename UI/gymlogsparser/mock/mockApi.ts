/**
 * Dev-only mock backend (#66): a Vite plugin that answers every `/api/*` call the UI
 * makes from in-memory fixtures, so the frontend runs without .NET, PostgreSQL or OIDC.
 * Enabled only by `vite --mode mock` (dev server); `vite build` never loads it.
 * Writes change the in-memory state and are lost on restart.
 */
import type { IncomingMessage, ServerResponse } from "node:http";
import { randomUUID } from "node:crypto";
import type { Plugin } from "vite";
import type { BodyweightEntry } from "../src/types/bodyweight.ts";
import type {
  DashboardData,
  DashboardDayEntry,
  DashboardExercise,
} from "../src/types/dashboard.ts";
import type {
  ExerciseDefinition,
  ExerciseProgressPoint,
  ExerciseSummary,
} from "../src/types/exercise.ts";
import type { MuscleTarget, MuscleTargetUpdate } from "../src/types/muscleTargets.ts";
import type { WorkoutLog, WorkoutSummary } from "../src/types/workout.ts";
import {
  addDays,
  cannedParse,
  createBodyweight,
  createCatalog,
  createTargets,
  createWorkouts,
  dayNumber,
  mockUser,
  todayIso,
  weekday,
} from "./fixtures.ts";

const RANGE_DAYS: Record<string, number> = { "3M": 91, "6M": 182, "1Y": 365, "5Y": 1826, "10Y": 3653 };
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

function createState() {
  const catalog = createCatalog();
  return {
    signedIn: true,
    catalog,
    workouts: createWorkouts(catalog),
    bodyweight: createBodyweight(),
    targets: createTargets(),
  };
}

type State = ReturnType<typeof createState>;

class HttpError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

const clone = <T>(value: T): T => structuredClone(value);
const round2 = (n: number) => Math.round(n * 100) / 100;
const epley = (weight: number, reps: number) =>
  reps <= 0 || weight <= 0 ? 0 : reps === 1 ? weight : weight * (1 + reps / 30);

/** DOTS (male), as Infrastructure/Helpers/DotsCalculator.cs. */
function dots(lifted: number, bodyweight: number): number {
  if (lifted <= 0 || bodyweight <= 0) return 0;
  const c = [-0.000001093, 0.0007391293, -0.1918759221, 24.0900756, -307.75076];
  const bw = Math.min(Math.max(bodyweight, 40), 210);
  const denominator = c[0] * bw ** 4 + c[1] * bw ** 3 + c[2] * bw ** 2 + c[3] * bw + c[4];
  return lifted * (500 / denominator);
}

/** Linear interpolation, clamped at both ends (BodyweightHistory.GetInterpolated). */
function bodyweightOn(entries: BodyweightEntry[], date: string): number | null {
  const points = [...entries].sort((a, b) => a.date.localeCompare(b.date));
  if (points.length === 0) return null;
  if (date <= points[0].date) return points[0].weightKg;
  const last = points[points.length - 1];
  if (date >= last.date) return last.weightKg;
  for (let i = 1; i < points.length; i++) {
    const prev = points[i - 1];
    const next = points[i];
    if (date > next.date) continue;
    const fraction = (dayNumber(date) - dayNumber(prev.date)) / (dayNumber(next.date) - dayNumber(prev.date));
    return prev.weightKg + (next.weightKg - prev.weightKg) * fraction;
  }
  return last.weightKg;
}

/** Same rules as Infrastructure/Services/DashboardService.cs. */
function dashboard(state: State, to: string, days: number): DashboardData {
  const weeks = Math.max(53, Math.floor((days - 1 + 6) / 7) + 1);
  const from = addDays(to, -weekday(to) - (weeks - 1) * 7);
  const dated = state.workouts
    .filter((w): w is WorkoutLog & { date: string; id: string } => !!w.date && !!w.id && w.date <= to)
    .sort((a, b) => a.date.localeCompare(b.date));
  const inWindow = dated.filter((w) => w.date >= from);

  // Newest name/muscle group wins; warm-up activities are not training volume.
  const exercises = new Map<string, DashboardExercise>();
  for (const w of [...inWindow].reverse()) {
    for (const e of w.exercises) {
      if (e.muscleGroup === "Warm-up" || exercises.has(e.exerciseId)) continue;
      exercises.set(e.exerciseId, {
        exerciseId: e.exerciseId,
        name: e.name,
        muscleGroup: e.muscleGroup,
        category: e.category,
      });
    }
  }

  const entries = new Map<string, DashboardDayEntry & { date: string }>();
  type SetRow = { date: string; workoutId: string; weight: number | null; reps: number | null };
  const strength = new Map<string, SetRow[]>();
  for (const w of dated) {
    for (const e of w.exercises) {
      if (e.category === "Strength") {
        const rows = strength.get(e.exerciseId) ?? [];
        for (const s of e.sets) {
          if (s.warmup === true) continue;
          rows.push({ date: w.date, workoutId: w.id, weight: s.weight, reps: s.reps });
        }
        strength.set(e.exerciseId, rows);
      } else if (w.date >= from && exercises.has(e.exerciseId) && e.cardio.length > 0) {
        const key = `${w.date}|${e.exerciseId}`;
        const entry = entries.get(key) ?? {
          date: w.date, exerciseId: e.exerciseId, workoutIds: [], sets: 0, minutes: 0,
          topSet: 0, estimatedOneRepMax: 0, volume: 0, pr: false,
        };
        if (!entry.workoutIds.includes(w.id)) entry.workoutIds.push(w.id);
        entry.sets += e.cardio.length;
        entry.minutes = Math.round((entry.minutes + e.cardio.reduce((m, c) => m + (c.durationSeconds ?? 0), 0) / 60) * 10) / 10;
        entries.set(key, entry);
      }
    }
  }

  for (const [exerciseId, rows] of strength) {
    let best = 0;
    let hadSession = false;
    const byDate = new Map<string, SetRow[]>();
    for (const r of rows) byDate.set(r.date, [...(byDate.get(r.date) ?? []), r]);
    for (const [date, day] of [...byDate].sort(([a], [b]) => a.localeCompare(b))) {
      const weighted = day.filter((s) => (s.weight ?? 0) > 0 && (s.reps ?? 0) > 0) as { weight: number; reps: number }[];
      const e1rm = weighted.reduce((m, s) => Math.max(m, epley(s.weight, s.reps)), 0);
      const pr = hadSession && e1rm > best;
      if (e1rm > 0) {
        best = Math.max(best, e1rm);
        hadSession = true;
      }
      if (date < from || !exercises.has(exerciseId)) continue;
      entries.set(`${date}|${exerciseId}`, {
        date,
        exerciseId,
        workoutIds: [...new Set(day.map((s) => s.workoutId))],
        sets: day.length,
        minutes: 0,
        topSet: weighted.reduce((m, s) => Math.max(m, s.weight), 0),
        estimatedOneRepMax: round2(e1rm),
        volume: round2(weighted.reduce((sum, s) => sum + s.weight * s.reps, 0)),
        pr,
      });
    }
  }

  const titles = new Map<string, string>();
  for (const w of inWindow) {
    if (!w.title?.trim()) continue;
    const existing = titles.get(w.date);
    if (!existing) titles.set(w.date, w.title);
    else if (!existing.split(" + ").includes(w.title)) titles.set(w.date, `${existing} + ${w.title}`);
  }

  const used = new Set([...entries.values()].map((e) => e.exerciseId));
  const daysByDate = new Map<string, DashboardDayEntry[]>();
  for (const { date, ...entry } of entries.values()) {
    daysByDate.set(date, [...(daysByDate.get(date) ?? []), entry]);
  }

  return {
    from,
    to,
    exercises: [...exercises.values()]
      .filter((e) => used.has(e.exerciseId))
      .sort((a, b) => a.name.localeCompare(b.name)),
    days: [...daysByDate]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([date, list]) => ({
        date,
        title: titles.get(date) ?? null,
        entries: list.sort((a, b) => a.exerciseId.localeCompare(b.exerciseId)),
      })),
  };
}

function summaries(state: State): WorkoutSummary[] {
  return [...state.workouts]
    .sort((a, b) => (b.date ?? "").localeCompare(a.date ?? ""))
    .map((w) => ({
      id: w.id!,
      date: w.date,
      title: w.title,
      exerciseCount: w.exercises.length,
      totalSets: w.exercises.reduce((n, e) => n + e.sets.length, 0),
      hasCardio: w.exercises.some((e) => e.category === "Cardio"),
      muscleGroups: [...new Set(w.exercises.map((e) => e.muscleGroup).filter((m): m is string => !!m?.trim()))],
    }));
}

function exerciseSummaries(state: State): ExerciseSummary[] {
  const seen = new Map<string, ExerciseSummary>();
  for (const w of state.workouts) {
    for (const e of w.exercises) {
      if (e.category !== "Strength" || seen.has(e.exerciseId)) continue;
      seen.set(e.exerciseId, { exerciseId: e.exerciseId, name: e.name, muscleGroup: e.muscleGroup });
    }
  }
  return [...seen.values()].sort((a, b) => a.name.localeCompare(b.name));
}

function progress(state: State, exerciseId: string): ExerciseProgressPoint[] {
  const points: ExerciseProgressPoint[] = [];
  for (const w of state.workouts) {
    const occurrences = w.exercises.filter((e) => e.exerciseId === exerciseId);
    if (occurrences.length === 0) continue;
    const working = occurrences
      .flatMap((e) => e.sets)
      .filter((s) => s.warmup !== true && (s.weight ?? 0) > 0 && (s.reps ?? 0) > 0) as { weight: number; reps: number }[];
    const interpolated = w.date ? bodyweightOn(state.bodyweight, w.date) : null;
    const bodyweight = interpolated === null ? null : Math.round(interpolated * 10) / 10;
    const e1rm = working.reduce((m, s) => Math.max(m, epley(s.weight, s.reps)), 0);
    points.push({
      workoutId: w.id!,
      date: w.date,
      maxWeight: working.reduce((m, s) => Math.max(m, s.weight), 0),
      totalVolume: round2(working.reduce((sum, s) => sum + s.weight * s.reps, 0)),
      estimatedOneRepMax: round2(e1rm),
      bodyweight,
      dots: working.length === 0 ? 0 : bodyweight === null ? null : Math.round(dots(e1rm, bodyweight) * 10) / 10,
    });
  }
  if (points.length === 0) throw new HttpError(404, `No workouts with the exercise "${exerciseId}".`);
  return points.sort((a, b) => (a.date ?? "").localeCompare(b.date ?? ""));
}

function findWorkout(state: State, id: string): WorkoutLog {
  const workout = state.workouts.find((w) => w.id === id);
  if (!workout) throw new HttpError(404, "There is no workout with that ID.");
  return workout;
}

function validateBodyweight(body: unknown): BodyweightEntry {
  const entry = body as Partial<BodyweightEntry> | null;
  if (!entry || typeof entry.date !== "string" || !ISO_DATE.test(entry.date) || entry.date > todayIso())
    throw new HttpError(400, "The date must be between 2000-01-01 and today, as yyyy-MM-dd.");
  if (typeof entry.weightKg !== "number" || entry.weightKg < 20 || entry.weightKg > 400)
    throw new HttpError(400, "The weight must be between 20 and 400 kg.");
  return { date: entry.date, weightKg: entry.weightKg };
}

function normalizeDefinition(body: unknown, id: string): ExerciseDefinition {
  const def = body as ExerciseDefinition | null;
  if (!def || !def.name?.trim() || !def.muscleGroup) throw new HttpError(400, "Send an exercise with a name and a muscle group.");
  return {
    ...def,
    id,
    name: def.name.trim(),
    aliases: def.aliases ?? [],
    muscles: [
      { muscleGroup: def.muscleGroup, share: 1 },
      ...(def.muscles ?? []).filter((m) => m.muscleGroup !== def.muscleGroup),
    ],
  };
}

const kebab = (name: string) =>
  name
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/ł/gi, "l")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");

type Result = { status: number; body?: unknown; redirect?: string; delay?: number };

function route(state: State, method: string, path: string, query: URLSearchParams, body: unknown): Result {
  const ok = (b: unknown, status = 200): Result => ({ status, body: clone(b) });
  const seg = path.split("/").filter(Boolean).map(decodeURIComponent);
  const is = (m: string, pattern: string) => {
    const p = pattern.split("/").filter(Boolean);
    return m === method && p.length === seg.length && p.every((part, i) => part.startsWith(":") || part === seg[i]);
  };

  // Auth: login and logout only flip a flag and redirect like the real API.
  if (is("GET", "auth/login")) {
    state.signedIn = true;
    const returnUrl = query.get("returnUrl") ?? "/";
    return { status: 302, redirect: returnUrl.startsWith("/") && !returnUrl.startsWith("//") ? returnUrl : "/" };
  }
  if (is("POST", "auth/logout")) {
    state.signedIn = false;
    return { status: 302, redirect: "/signed-out" };
  }
  if (is("GET", "auth/signed-out")) return { status: 302, redirect: "/signed-out" };
  if (!state.signedIn) return { status: 401 };
  if (is("GET", "auth/me")) return ok(mockUser);

  // Workouts
  if (is("GET", "workouts")) return ok(summaries(state));
  if (is("POST", "workouts")) {
    const id = randomUUID();
    state.workouts.push({ ...(body as WorkoutLog), id });
    return ok(id, 201);
  }
  if (is("GET", "workouts/:id")) {
    const w = clone(findWorkout(state, seg[1]));
    for (const e of w.exercises) e.sets.sort((a, b) => (a.setNumber ?? 0) - (b.setNumber ?? 0));
    return ok(w);
  }
  if (is("PUT", "workouts/:id")) {
    const w = findWorkout(state, seg[1]);
    Object.assign(w, clone(body as WorkoutLog), { id: w.id });
    return { status: 204 };
  }
  if (is("DELETE", "workouts/:id")) {
    findWorkout(state, seg[1]);
    state.workouts = state.workouts.filter((w) => w.id !== seg[1]);
    return { status: 204 };
  }

  // AI parse: a canned result after a short "thinking" delay.
  if (is("POST", "ai/parse-workout")) {
    const req = body as { text?: string; barbellWeightsArePerSide?: boolean } | null;
    if (!req?.text?.trim()) throw new HttpError(400, "Workout text is required.");
    return {
      status: 200,
      delay: 1200,
      body: {
        workout: cannedParse(state.catalog, !!req.barbellWeightsArePerSide),
        usage: { cacheHit: false, cacheHitTokens: 0, cacheMissTokens: 2412, inputTokens: 2412, outputTokens: 538 },
      },
    };
  }

  // Exercises and catalog
  if (is("GET", "exercises")) return ok(exerciseSummaries(state));
  if (is("GET", "exercises/definitions")) return ok(state.catalog);
  if (is("POST", "exercises/definitions")) {
    const req = body as ExerciseDefinition | null;
    const id = req?.id?.trim() || kebab(req?.name ?? "");
    if (!id) throw new HttpError(400, "Send an exercise with a name.");
    if (state.catalog.some((d) => d.id === id)) throw new HttpError(409, `An exercise with the ID "${id}" already exists.`);
    const def = normalizeDefinition(body, id);
    state.catalog.push(def);
    return ok(def, 201);
  }
  if (is("PUT", "exercises/definitions/:id")) {
    const index = state.catalog.findIndex((d) => d.id === seg[2]);
    if (index < 0) throw new HttpError(404, `There is no exercise with the ID "${seg[2]}".`);
    state.catalog[index] = normalizeDefinition(body, seg[2]);
    return ok(state.catalog[index]);
  }
  if (is("GET", "exercises/:id/progress")) return ok(progress(state, seg[1]));

  // Dashboard and targets
  if (is("GET", "dashboard")) {
    const to = query.get("to") ?? todayIso();
    const range = query.get("range") || "1Y";
    if (!ISO_DATE.test(to)) throw new HttpError(400, "Send `to` as yyyy-MM-dd.");
    const days = RANGE_DAYS[range];
    if (!days) throw new HttpError(400, `Unknown range '${range}'. Use one of: ${Object.keys(RANGE_DAYS).join(", ")}.`);
    return ok(dashboard(state, to, days));
  }
  if (is("GET", "muscle-targets")) return ok(state.targets);
  if (is("PUT", "muscle-targets")) {
    const updates = body as MuscleTargetUpdate[] | null;
    if (!Array.isArray(updates)) throw new HttpError(400, "Targets are required.");
    for (const u of updates) {
      const target: MuscleTarget | undefined = state.targets.find((t) => t.muscleGroup.toLowerCase() === u.muscleGroup?.toLowerCase());
      if (!target) throw new HttpError(400, `"${u.muscleGroup}" has no target.`);
      if (!Number.isInteger(u.minSets) || !Number.isInteger(u.maxSets) || u.minSets < 0 || u.minSets > u.maxSets || u.maxSets > 100)
        throw new HttpError(400, `${target.muscleGroup}: the range must be whole sets with 0 ≤ min ≤ max ≤ 100.`);
      target.minSets = u.minSets;
      target.maxSets = u.maxSets;
    }
    return ok(state.targets);
  }

  // Bodyweight
  const sortedBodyweight = () => [...state.bodyweight].sort((a, b) => b.date.localeCompare(a.date));
  if (is("GET", "bodyweight-entries")) return ok(sortedBodyweight());
  if (is("POST", "bodyweight-entries")) {
    const entry = validateBodyweight(body);
    if (state.bodyweight.some((e) => e.date === entry.date))
      throw new HttpError(409, `You already have a measurement on ${entry.date}. Edit that one or pick another date.`);
    state.bodyweight.push(entry);
    return ok(entry, 201);
  }
  if (is("PUT", "bodyweight-entries/:date") || is("DELETE", "bodyweight-entries/:date")) {
    const index = state.bodyweight.findIndex((e) => e.date === seg[1]);
    if (index < 0) throw new HttpError(404, `You have no measurement on ${seg[1]}.`);
    if (method === "DELETE") {
      state.bodyweight.splice(index, 1);
      return { status: 204 };
    }
    const entry = validateBodyweight(body);
    if (entry.date !== seg[1] && state.bodyweight.some((e) => e.date === entry.date))
      throw new HttpError(409, `You already have a measurement on ${entry.date}. Edit that one or pick another date.`);
    state.bodyweight[index] = entry;
    return ok(entry);
  }

  throw new HttpError(404, `The mock API has no ${method} /api/${seg.join("/")}.`);
}

async function readBody(req: IncomingMessage): Promise<unknown> {
  const chunks: Buffer[] = [];
  for await (const chunk of req) chunks.push(chunk as Buffer);
  const text = Buffer.concat(chunks).toString("utf8");
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    throw new HttpError(400, "The request body must be JSON.");
  }
}

function send(res: ServerResponse, result: Result) {
  if (result.redirect) {
    res.writeHead(result.status, { Location: result.redirect });
    res.end();
    return;
  }
  if (result.body === undefined) {
    res.writeHead(result.status);
    res.end();
    return;
  }
  res.writeHead(result.status, { "Content-Type": "application/json; charset=utf-8" });
  res.end(JSON.stringify(result.body));
}

export function mockApi(): Plugin {
  const state = createState();
  return {
    name: "gymlogs-mock-api",
    apply: "serve",
    configureServer(server) {
      server.config.logger.info(
        `  mock API: ${state.workouts.length} workouts, ${state.catalog.length} exercises, ${state.bodyweight.length} bodyweight entries (in memory)`,
      );
      server.middlewares.use((req, res, next) => {
        const url = new URL(req.url ?? "/", "http://mock");
        if (!url.pathname.startsWith("/api/")) return next();
        const method = req.method ?? "GET";
        readBody(req)
          .then((body) => route(state, method, url.pathname.slice("/api/".length), url.searchParams, body))
          .catch((e: unknown): Result => {
            if (e instanceof HttpError) return { status: e.status, body: { error: e.message } };
            server.config.logger.error(`mock API: ${String(e)}`);
            return { status: 500, body: { error: "The mock API failed; see the dev server log." } };
          })
          .then((result) => setTimeout(() => send(res, result), result.delay ?? 150));
      });
    },
  };
}
