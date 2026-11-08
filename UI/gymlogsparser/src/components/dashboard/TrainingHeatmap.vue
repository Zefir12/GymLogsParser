<script setup lang="ts">
import { computed, ref } from "vue";
import { useRouter } from "vue-router";

import SegmentedControl from "@/components/dashboard/SegmentedControl.vue";
import WorkoutPickerModal from "@/components/dashboard/WorkoutPickerModal.vue";
import { useDashboardStore, type WorkoutChoice } from "@/stores/dashboard";
import { dayDate, formatDay, monthName } from "@/utils/dashboard";

const store = useDashboardStore();
const router = useRouter();

const LEVELS = 5;
const legend = Array.from({ length: LEVELS }, (_, i) => i);
const weekdayLabels = ["Mon", "", "Wed", "", "Fri", "", ""];

/** "last" = the last 53 weeks; otherwise a calendar year with training. */
const yearOptions = computed(() => [
  { value: "last", label: "Last 12 mo" },
  ...store.trainingYears.map((y) => ({ value: String(y), label: String(y) })),
]);

const yearModel = computed({
  get: () => (store.heatmapYear === null ? "last" : String(store.heatmapYear)),
  set: (value: string) => {
    store.heatmapYear = value === "last" ? null : Number(value);
  },
});

const weeks = computed(() => {
  const first = store.heatmapFirstDay;
  const last = store.heatmapLastDay;
  const values = store.filteredSetsByDay;
  // shade relative to the busiest day on screen
  let max = 0;
  for (const [day, sets] of values) {
    if (day >= first && day <= last && sets > max) max = sets;
  }
  max ||= 1;

  return Array.from({ length: store.heatmapWeeks }, (_, w) => {
    const start = store.heatmapStart + w * 7;
    const days = Array.from({ length: 7 }, (_, i) => {
      const day = start + i;
      if (day < first || day > last) {
        return { day, level: -1, tip: "", trained: false };
      }

      const sets = values.get(day) ?? 0;
      const title = store.dayByNumber.get(day)?.title;
      const level = sets ? Math.min(LEVELS - 1, Math.ceil((sets / max) * (LEVELS - 1))) : 0;
      const tip = sets
        ? `${formatDay(day)}${title ? ` · ${title}` : ""} · ${sets} set${sets === 1 ? "" : "s"}`
        : `${formatDay(day)} · rest`;
      return { day, level, tip, trained: sets > 0 };
    });
    return {
      start,
      // label the column holding the month's first Monday
      label: dayDate(start).getUTCDate() <= 7 ? monthName(start) : "",
      days,
    };
  });
});

// --- clicking a training day opens its workout in the editor (#73) ---

const picker = ref<{ dateLabel: string; choices: WorkoutChoice[] } | null>(null);

function openWorkout(id: string) {
  picker.value = null;
  router.push({ name: "edit", query: { workout: id } });
}

function openDay(day: number) {
  const ids = store.workoutIdsOnDay(day);
  if (ids.length === 1) openWorkout(ids[0]!);
  else if (ids.length > 1) {
    picker.value = { dateLabel: formatDay(day), choices: store.workoutChoices(ids) };
  }
}
</script>

<template>
  <section aria-label="Training days" class="card">
    <div class="card-head">
      <h2>{{ store.filterLabel }} · training days</h2>
      <SegmentedControl
        v-if="store.trainingYears.length"
        v-model="yearModel"
        :options="yearOptions"
        label="Year"
        compact
      />
      <div class="legend">
        Less
        <span
          v-for="level in legend"
          :key="level"
          class="cell"
          :class="`heat-${level}`"
        />
        More
      </div>
    </div>

    <div class="scroller">
      <div class="grid">
        <div class="column weekdays" aria-hidden="true">
          <div class="month" />
          <div v-for="(label, i) in weekdayLabels" :key="i" class="weekday">
            {{ label }}
          </div>
        </div>
        <div v-for="week in weeks" :key="week.start" class="column">
          <div class="month">{{ week.label }}</div>
          <template v-for="d in week.days" :key="d.day">
            <button
              v-if="d.trained"
              type="button"
              class="cell day"
              :class="`heat-${d.level}`"
              :title="d.tip"
              :aria-label="`${d.tip}. Open workout`"
              @click="openDay(d.day)"
            />
            <span
              v-else
              class="cell"
              :class="d.level < 0 ? 'future' : `heat-${d.level}`"
              :title="d.tip || undefined"
            />
          </template>
        </div>
      </div>
    </div>

    <WorkoutPickerModal
      v-if="picker"
      :date-label="picker.dateLabel"
      :choices="picker.choices"
      @pick="openWorkout"
      @close="picker = null"
    />
  </section>
</template>

<style scoped>
.card {
  padding: var(--card-pad);
  border: 1px solid var(--card-border);
  border-radius: var(--radius-card);
  background: var(--card-bg);
}

.card-head {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 14px;
}

h2 {
  margin: 0;
  color: var(--text-primary);
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 600;
}

.legend {
  display: flex;
  align-items: center;
  gap: 4px;
  color: var(--text-secondary);
  font-size: 12px;
}

.legend .cell {
  width: 12px;
  height: 12px;
  border-radius: 2px;
}

.scroller {
  overflow-x: auto;
  padding-bottom: 4px;
}

.grid {
  display: flex;
  gap: 4px;
  width: max-content;
}

.column {
  display: flex;
  flex-direction: column;
  gap: 4px;
  width: 16px;
}

.weekdays {
  width: 32px;
}

.month,
.weekday {
  height: 16px;
  line-height: 16px;
  color: var(--text-secondary);
  font-size: 11px;
  white-space: nowrap;
}

.cell {
  display: block;
  width: 16px;
  height: 16px;
  border-radius: var(--radius-xs);
}

button.day {
  padding: 0;
  border: 0;
  cursor: pointer;
}

button.day:hover {
  outline: 1px solid var(--accent-border);
}

button.day:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.future {
  background: transparent;
}

.heat-0 {
  background: var(--heat-0);
}

.heat-1 {
  background: var(--heat-1);
}

.heat-2 {
  background: var(--heat-2);
}

.heat-3 {
  background: var(--heat-3);
}

.heat-4 {
  background: var(--heat-4);
}
</style>
