import { computed, ref, watch } from "vue";
import { defineStore } from "pinia";

import { getDashboard } from "@/api/client";
import type {
  DashboardData,
  DashboardDayEntry,
  DashboardExercise,
  DashboardMetric,
  DashboardRange,
} from "@/types/dashboard";
import {
  ALL,
  CARDIO,
  MAIN_LIFT_IDS,
  RANGE_DAYS,
  dayNumber,
  groupOf,
  sortGroups,
  todayIso,
  weekStart,
} from "@/utils/dashboard";

export interface ExerciseSession {
  day: number;
  entry: DashboardDayEntry;
}

export interface ExerciseRow {
  exercise: DashboardExercise;
  sessions: number;
  /** Best estimated 1RM in range, null for cardio or no weighted sets. */
  best: number | null;
  /** % change of the estimated 1RM from the first to the last session in range. */
  change: number | null;
}

/**
 * Dashboard page state. The filters (range, body part, exercise, chart metric) are
 * shared by every section, and the raw per-day data is indexed here once so the
 * sections only do cheap lookups.
 */
export const useDashboardStore = defineStore("dashboard", () => {
  const data = ref<DashboardData | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const range = ref<DashboardRange>("6M");
  const muscle = ref<string>(ALL);
  const exerciseId = ref<string | null>(null);
  const metric = ref<DashboardMetric>("e1rm");

  /** Bumped per request so a slow, older response never overwrites a newer one. */
  let requestId = 0;

  async function load() {
    const id = ++requestId;
    loading.value = true;
    error.value = null;
    try {
      const result = await getDashboard(todayIso(), range.value);
      if (id === requestId) data.value = result;
    } catch (err) {
      if (id === requestId) {
        error.value =
          err instanceof Error ? err.message : "Could not load the dashboard.";
      }
    } finally {
      if (id === requestId) loading.value = false;
    }
  }

  function setMuscle(group: string) {
    muscle.value = group;
    exerciseId.value = null;
  }

  /** Clicking the active body part again clears the filter. */
  function toggleMuscle(group: string) {
    setMuscle(muscle.value === group ? ALL : group);
  }

  // --- indexes ---

  const fromDay = computed(() => (data.value ? dayNumber(data.value.from) : 0));
  const toDay = computed(() => (data.value ? dayNumber(data.value.to) : 0));
  /** Monday of the week that contains `to`. */
  const currentWeekStart = computed(() => weekStart(toDay.value));
  /** Monday of the heatmap's first week: 53 weeks ending with the current one. */
  const heatmapStart = computed(() => currentWeekStart.value - 52 * 7);
  /** First day of the selected range. */
  const rangeStart = computed(
    () => toDay.value - RANGE_DAYS[range.value] + 1,
  );

  // The response covers at least a year; a longer range needs older days, so fetch
  // them only when the loaded window doesn't reach back far enough.
  watch(range, () => {
    if (data.value && rangeStart.value < fromDay.value) load();
  });

  const exerciseById = computed(
    () =>
      new Map((data.value?.exercises ?? []).map((e) => [e.exerciseId, e])),
  );

  const days = computed(() =>
    (data.value?.days ?? []).map((d) => ({
      day: dayNumber(d.date),
      title: d.title,
      entries: d.entries,
    })),
  );

  const dayByNumber = computed(
    () => new Map(days.value.map((d) => [d.day, d])),
  );

  const groupOfEntry = (entry: DashboardDayEntry) =>
    groupOf(exerciseById.value.get(entry.exerciseId));

  /** Per day: working sets per group (strength) and minutes (Cardio). */
  const groupTotalsByDay = computed(() => {
    const result = new Map<number, Map<string, number>>();
    for (const d of days.value) {
      const totals = new Map<string, number>();
      for (const e of d.entries) {
        const g = groupOfEntry(e);
        const v = g === CARDIO ? e.minutes : e.sets;
        totals.set(g, (totals.get(g) ?? 0) + v);
        if (g !== CARDIO) totals.set(ALL, (totals.get(ALL) ?? 0) + e.sets);
      }
      result.set(d.day, totals);
    }
    return result;
  });

  /** Sets (or Cardio minutes) of one group (or All) over [start, end]. */
  function groupTotal(group: string, start: number, end: number) {
    let sum = 0;
    for (let day = start; day <= end; day++) {
      sum += groupTotalsByDay.value.get(day)?.get(group) ?? 0;
    }
    return sum;
  }

  const groups = computed(() =>
    sortGroups((data.value?.exercises ?? []).map((e) => groupOf(e))),
  );

  const sessionsByExercise = computed(() => {
    const result = new Map<string, ExerciseSession[]>();
    for (const d of days.value) {
      for (const entry of d.entries) {
        const list = result.get(entry.exerciseId) ?? [];
        list.push({ day: d.day, entry });
        result.set(entry.exerciseId, list);
      }
    }
    return result;
  });

  // --- filter ---

  function matches(entry: DashboardDayEntry) {
    if (exerciseId.value) return entry.exerciseId === exerciseId.value;
    return muscle.value === ALL || groupOfEntry(entry) === muscle.value;
  }

  /** Sets matching the active filter, per training day. */
  const filteredSetsByDay = computed(() => {
    const result = new Map<number, number>();
    for (const d of days.value) {
      const sets = d.entries.reduce((s, e) => s + (matches(e) ? e.sets : 0), 0);
      if (sets > 0) result.set(d.day, sets);
    }
    return result;
  });

  const filterLabel = computed(() => {
    if (exerciseId.value) {
      return exerciseById.value.get(exerciseId.value)?.name ?? "Exercise";
    }
    return muscle.value === ALL ? "All training" : muscle.value;
  });

  // --- summary line ---

  const summary = computed(() => {
    let sessions = 0;
    let volume = 0;
    let prs = 0;
    for (const d of days.value) {
      if (d.day < rangeStart.value) continue;
      if (filteredSetsByDay.value.has(d.day)) sessions++;
      for (const e of d.entries) {
        if (!matches(e)) continue;
        volume += e.volume;
        if (e.pr) prs++;
      }
    }

    // Consecutive weeks with matching training, ending this week (or last week
    // if nothing has been done yet this week).
    const weekSets = (w: number) => {
      let sum = 0;
      for (let day = w; day < w + 7; day++) {
        sum += filteredSetsByDay.value.get(day) ?? 0;
      }
      return sum;
    };
    let week = currentWeekStart.value;
    if (weekSets(week) === 0) week -= 7;
    let streak = 0;
    while (week >= fromDay.value && weekSets(week) > 0) {
      streak++;
      week -= 7;
    }

    return { sessions, streak, tonnes: volume / 1000, prs };
  });

  // --- exercises table + chart ---

  const exerciseRows = computed<ExerciseRow[]>(() => {
    const inRange = (id: string) =>
      (sessionsByExercise.value.get(id) ?? []).filter(
        (s) => s.day >= rangeStart.value,
      );

    let candidates: DashboardExercise[];
    if (muscle.value === ALL) {
      candidates = MAIN_LIFT_IDS.map((id) => exerciseById.value.get(id)).filter(
        (e): e is DashboardExercise => !!e,
      );
      // No main lifts logged: fall back to the most frequent strength exercises.
      if (candidates.length === 0) {
        candidates = (data.value?.exercises ?? [])
          .filter((e) => e.category === "Strength")
          .sort((a, b) => inRange(b.exerciseId).length - inRange(a.exerciseId).length)
          .slice(0, 6);
      }
    } else {
      candidates = (data.value?.exercises ?? []).filter(
        (e) => groupOf(e) === muscle.value,
      );
    }

    return candidates
      .map((exercise) => {
        const sessions = inRange(exercise.exerciseId);
        const lifts = sessions
          .map((s) => s.entry.estimatedOneRepMax)
          .filter((v) => v > 0);
        const first = lifts[0];
        const last = lifts[lifts.length - 1];
        return {
          exercise,
          sessions: sessions.length,
          best: lifts.length > 0 ? Math.max(...lifts) : null,
          change: lifts.length > 1 ? ((last - first) / first) * 100 : null,
        };
      })
      .sort((a, b) =>
        muscle.value === ALL ? 0 : b.sessions - a.sessions,
      );
  });

  /** The exercise the chart shows: the picked one, or the first strength row. */
  const chartExercise = computed<DashboardExercise | null>(() => {
    if (exerciseId.value) return exerciseById.value.get(exerciseId.value) ?? null;
    return (
      exerciseRows.value.find((r) => r.exercise.category === "Strength")
        ?.exercise ?? null
    );
  });

  const chartPoints = computed(() => {
    const exercise = chartExercise.value;
    if (!exercise) return [];
    return (sessionsByExercise.value.get(exercise.exerciseId) ?? [])
      .filter((s) => s.day >= rangeStart.value)
      .map((s) => ({
        day: s.day,
        value:
          metric.value === "e1rm"
            ? s.entry.estimatedOneRepMax
            : metric.value === "top"
              ? s.entry.topSet
              : s.entry.volume,
        pr: s.entry.pr,
      }))
      .filter((p) => p.value > 0);
  });

  return {
    data,
    loading,
    error,
    range,
    muscle,
    exerciseId,
    metric,
    load,
    setMuscle,
    toggleMuscle,
    fromDay,
    toDay,
    currentWeekStart,
    heatmapStart,
    rangeStart,
    dayByNumber,
    groups,
    groupTotal,
    filteredSetsByDay,
    filterLabel,
    summary,
    exerciseRows,
    chartExercise,
    chartPoints,
  };
});
