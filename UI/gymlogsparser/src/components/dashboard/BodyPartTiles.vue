<script setup lang="ts">
import { computed } from "vue";

import { useDashboardStore } from "@/stores/dashboard";
import { useMuscleTargetsStore } from "@/stores/muscleTargets";
import { ALL, CARDIO, formatRange, targetStatus } from "@/utils/dashboard";

const store = useDashboardStore();
const targets = useMuscleTargetsStore();

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

    // All, Cardio and groups without a target range get no status.
    const target = targets.targetOf(group);
    const status = target ? targetStatus(current, target) : null;

    return {
      group,
      label: group === ALL ? "All body parts" : group,
      value: current.toFixed(group === CARDIO ? 0 : 1),
      unit: group === CARDIO ? "min/wk" : "sets/wk",
      status,
      statusTitle: target
        ? `Target ${formatRange(target)} sets/wk (secondary muscles count by share)`
        : undefined,
      trend: `${delta >= 0 ? "▲" : "▼"} ${Math.abs(delta).toFixed(1)}`,
      trendUp: delta >= 0,
      spark: weeks.map((v) => `${Math.round((v / max) * 100)}%`),
      active: store.muscle === group,
    };
  }).sort((a, b) => rank(a.group) - rank(b.group) || Number(b.value) - Number(a.value)),
);

/** All first, then muscles by sets/wk (most first), Cardio (minutes) last. */
function rank(group: string) {
  return group === ALL ? -1 : group === CARDIO ? 1 : 0;
}
</script>

<template>
  <section
    aria-label="Body parts"
    class="tiles"
    :style="{ '--tile-cols': Math.ceil(tiles.length / 2) }"
  >
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
        <span class="tile-label" :title="tile.label">{{ tile.label }}</span>
        <!-- always rendered, empty for All/Cardio, so every tile lines up -->
        <span
          class="tile-status"
          :class="
            tile.status ? (tile.status === 'On target' ? 'good' : 'warn') : null
          "
          :title="tile.statusTitle"
        >
          {{ tile.status ?? "\u00a0" }}
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
        Last 12 weeks
      </span>
    </button>
  </section>
</template>

<style scoped>
.tiles {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(130px, 100%), 1fr));
  gap: var(--card-gap);
}

/* Wide screens: every tile in two rows. */
@media (min-width: 1100px) {
  .tiles {
    grid-template-columns: repeat(var(--tile-cols, 10), minmax(0, 1fr));
  }
}

.tile {
  display: flex;
  flex-direction: column;
  gap: var(--card-gap-sm);
  min-width: 0;
  padding: var(--card-pad-sm);

  border: 1px solid var(--card-border);
  border-radius: var(--radius-tile);
  background: var(--card-bg);
  color: var(--text-primary);

  text-align: left;
  transition: border-color 0.15s var(--ease);
}

.tile:hover {
  border-color: var(--glass-border-strong);
}

.tile.active {
  border-color: rgba(var(--accent-rgb), 0.6);
  background: rgba(var(--accent-rgb), 0.07);
}

/* name on one line, status always on the line below it */
.tile-head {
  display: flex;
  flex-direction: column;
  gap: 2px;
  width: 100%;
  min-width: 0;
}

.tile-label {
  overflow: hidden;
  font-weight: 600;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.tile-status,
.tile-trend {
  font-size: 11px;
  white-space: nowrap;
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
  flex-wrap: wrap;
  align-items: baseline;
  gap: 2px 6px;
  width: 100%;
}

.tile-value {
  font-family: var(--font-display);
  font-size: 22px;
  line-height: 1.1;
  font-weight: 600;
}

.tile-unit,
.tile-caption {
  color: var(--text-secondary);
  font-size: 11px;
}

.spark {
  display: flex;
  align-items: flex-end;
  gap: 2px;
  width: 100%;
  height: 22px;
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
