<script setup lang="ts">
import { computed } from "vue";

import { useDashboardStore } from "@/stores/dashboard";
import { dayDate, formatDay, monthName } from "@/utils/dashboard";

const store = useDashboardStore();

const LEVELS = 5;
const legend = Array.from({ length: LEVELS }, (_, i) => i);
const weekdayLabels = ["Mon", "", "Wed", "", "Fri", "", ""];

const weeks = computed(() => {
  const values = store.filteredSetsByDay;
  const max = Math.max(0, ...values.values()) || 1;

  return Array.from({ length: 53 }, (_, w) => {
    const start = store.fromDay + w * 7;
    const days = Array.from({ length: 7 }, (_, i) => {
      const day = start + i;
      if (day > store.toDay) return { day, level: -1, tip: "" };

      const sets = values.get(day) ?? 0;
      const title = store.dayByNumber.get(day)?.title;
      const level = sets ? Math.min(LEVELS - 1, Math.ceil((sets / max) * (LEVELS - 1))) : 0;
      const tip = sets
        ? `${formatDay(day)}${title ? ` · ${title}` : ""} · ${sets} set${sets === 1 ? "" : "s"}`
        : `${formatDay(day)} · rest`;
      return { day, level, tip };
    });
    return {
      start,
      // label the column holding the month's first Monday
      label: dayDate(start).getUTCDate() <= 7 ? monthName(start) : "",
      days,
    };
  });
});
</script>

<template>
  <section aria-label="Training days" class="card">
    <div class="card-head">
      <h2>{{ store.filterLabel }} · training days</h2>
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
          <span
            v-for="d in week.days"
            :key="d.day"
            class="cell"
            :class="d.level < 0 ? 'future' : `heat-${d.level}`"
            :title="d.tip || undefined"
          />
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.card {
  padding: 20px;
  border: 1px solid var(--card-border);
  border-radius: 14px;
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
  border-radius: 3px;
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
  border-radius: 4px;
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
