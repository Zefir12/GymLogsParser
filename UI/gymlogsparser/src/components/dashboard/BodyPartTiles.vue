<script setup lang="ts">
import { computed } from "vue";

import { useDashboardStore } from "@/stores/dashboard";
import { ALL, CARDIO, targetStatus } from "@/utils/dashboard";

const store = useDashboardStore();

const tiles = computed(() =>
  [ALL, ...store.groups].map((group) => {
    const today = store.toDay;
    // average per week over the last 4 weeks vs the 4 weeks before
    const current = store.groupTotal(group, today - 27, today) / 4;
    const previous = store.groupTotal(group, today - 55, today - 28) / 4;
    const delta = current - previous;

    // 12 calendar weeks, oldest first, the current one last
    const weeks = Array.from({ length: 12 }, (_, i) => {
      const start = store.currentWeekStart - (11 - i) * 7;
      return store.groupTotal(group, start, start + 6);
    });
    const max = Math.max(...weeks) || 1;

    const status = group === ALL || group === CARDIO ? null : targetStatus(current);

    return {
      group,
      label: group === ALL ? "All body parts" : group,
      value: current.toFixed(group === CARDIO ? 0 : 1),
      unit: group === CARDIO ? "min / week" : "sets / week",
      status,
      trend: `${delta >= 0 ? "▲" : "▼"} ${Math.abs(delta).toFixed(1)}`,
      trendUp: delta >= 0,
      spark: weeks.map((v) => `${Math.round((v / max) * 100)}%`),
      active: store.muscle === group,
    };
  }),
);
</script>

<template>
  <section aria-label="Body parts" class="tiles">
    <button
      v-for="tile in tiles"
      :key="tile.group"
      type="button"
      class="tile"
      :class="{ active: tile.active }"
      :aria-pressed="tile.active"
      @click="store.setMuscle(tile.group)"
    >
      <span class="tile-head">
        <span class="tile-label">{{ tile.label }}</span>
        <span
          v-if="tile.status"
          class="tile-status"
          :class="tile.status === 'On target' ? 'good' : 'warn'"
        >
          {{ tile.status }}
        </span>
      </span>

      <span class="tile-value-row">
        <span class="tile-value">{{ tile.value }}</span>
        <span class="tile-unit">{{ tile.unit }}</span>
        <span class="tile-trend" :class="tile.trendUp ? 'good' : 'warn'">
          {{ tile.trend }}
        </span>
      </span>

      <span class="spark" aria-hidden="true">
        <span
          v-for="(height, i) in tile.spark"
          :key="i"
          class="spark-bar"
          :class="{ recent: i >= 8 }"
          :style="{ height }"
        />
      </span>
      <span class="tile-caption">
        {{ tile.group === "Cardio" ? "Minutes" : "Sets" }} per week, last 12
        weeks
      </span>
    </button>
  </section>
</template>

<style scoped>
.tiles {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(240px, 100%), 1fr));
  gap: 12px;
}

.tile {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 16px;

  border: 1px solid var(--card-border);
  border-radius: 14px;
  background: var(--card-bg);
  color: var(--text-primary);

  text-align: left;
  transition: border-color 0.15s var(--ease);
}

.tile:hover {
  border-color: var(--glass-border-strong);
}

.tile.active {
  border-color: rgba(255, 122, 48, 0.6);
  background: rgba(255, 122, 48, 0.07);
}

.tile-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 8px;
  width: 100%;
}

.tile-label {
  font-weight: 600;
}

.tile-status,
.tile-trend {
  font-size: 12px;
}

.tile-trend {
  margin-left: auto;
  font-weight: 600;
}

.good {
  color: var(--success);
}

.warn {
  color: var(--warning);
}

.tile-value-row {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.tile-value {
  font-family: var(--font-display);
  font-size: 28px;
  font-weight: 600;
}

.tile-unit,
.tile-caption {
  color: var(--text-secondary);
  font-size: 12px;
}

.tile-caption {
  font-size: 11px;
}

.spark {
  display: flex;
  align-items: flex-end;
  gap: 3px;
  width: 100%;
  height: 34px;
}

.spark-bar {
  flex: 1 1 0;
  min-height: 2px;
  border-radius: 2px;
  background: var(--spark-idle);
}

.spark-bar.recent {
  background: var(--spark-recent);
}

.active .spark-bar {
  background: var(--spark-active);
}

.active .spark-bar.recent {
  background: var(--accent);
}
</style>
