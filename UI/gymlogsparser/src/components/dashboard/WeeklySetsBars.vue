<script setup lang="ts">
import { computed } from "vue";

import { useDashboardStore } from "@/stores/dashboard";
import {
  CARDIO,
  TARGET_MAX,
  TARGET_MIN,
  targetStatus,
} from "@/utils/dashboard";

const store = useDashboardStore();

/** Value at the right edge of the track; the 10–20 band sits inside it. */
const SCALE_MAX = 24;

const band = {
  left: `${((TARGET_MIN / SCALE_MAX) * 100).toFixed(2)}%`,
  width: `${(((TARGET_MAX - TARGET_MIN) / SCALE_MAX) * 100).toFixed(2)}%`,
};

const bars = computed(() =>
  store.groups
    .filter((g) => g !== CARDIO)
    .map((group) => {
      const value = store.groupTotal(group, store.toDay - 27, store.toDay) / 4;
      const status = targetStatus(value);
      return {
        group,
        value,
        label: value.toFixed(1),
        width: `${Math.min(100, (value / SCALE_MAX) * 100).toFixed(1)}%`,
        status,
        short: status.replace(" target", ""),
        active: store.muscle === group,
      };
    })
    .sort((a, b) => b.value - a.value),
);
</script>

<template>
  <section aria-label="Weekly sets per body part" class="card">
    <div class="head">
      <h2>Weekly sets per body part</h2>
      <span class="hint">
        Average of the last 4 weeks · shaded band = {{ TARGET_MIN }}–{{
          TARGET_MAX
        }}
        sets target · click a row to filter
      </span>
    </div>

    <p v-if="bars.length === 0" class="hint empty">No strength sets logged yet.</p>

    <div v-else class="rows">
      <button
        v-for="bar in bars"
        :key="bar.group"
        type="button"
        class="row"
        :class="{ active: bar.active }"
        :aria-pressed="bar.active"
        @click="store.toggleMuscle(bar.group)"
      >
        <span class="label">{{ bar.group }}</span>
        <span class="track">
          <span class="band" :style="band" />
          <span class="fill" :style="{ width: bar.width }" />
        </span>
        <span class="value">{{ bar.label }}</span>
        <span
          class="status"
          :class="bar.status === 'On target' ? 'good' : 'warn'"
        >
          <span class="long">{{ bar.status }}</span>
          <span class="short">{{ bar.short }}</span>
        </span>
      </button>
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

.head {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: baseline;
  gap: 8px;
}

h2 {
  margin: 0;
  color: var(--text-primary);
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 600;
}

.hint {
  color: var(--text-secondary);
  font-size: 12px;
}

.empty {
  margin: 16px 0 0;
}

.rows {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(420px, 100%), 1fr));
  gap: 4px 32px;
  margin-top: 16px;
}

.row {
  display: grid;
  grid-template-columns: 96px minmax(0, 1fr) 44px 92px;
  align-items: center;
  gap: 12px;
  min-height: 38px;
  padding: 0 8px;

  border: 0;
  border-radius: 8px;
  background: transparent;
  color: #c9ccd3;

  text-align: left;
}

.row:hover {
  background: var(--glass);
}

.row.active {
  background: rgba(255, 122, 48, 0.08);
  color: var(--accent-text);
}

.label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.track {
  position: relative;
  height: 10px;
  border-radius: 5px;
  background: var(--track);
}

.band {
  position: absolute;
  top: -3px;
  bottom: -3px;
  background: rgba(134, 198, 163, 0.12);
  border-left: 1px solid rgba(134, 198, 163, 0.35);
  border-right: 1px solid rgba(134, 198, 163, 0.35);
}

.fill {
  position: absolute;
  top: 0;
  bottom: 0;
  left: 0;
  border-radius: 5px;
  background: var(--bar-idle);
}

.active .fill {
  background: var(--accent);
}

.value {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.status {
  font-size: 12px;
}

.short {
  display: none;
}

.good {
  color: var(--success);
}

.warn {
  color: var(--warning);
}

@media (max-width: 520px) {
  .row {
    grid-template-columns: 76px minmax(0, 1fr) 36px 44px;
    gap: 8px;
  }

  .long {
    display: none;
  }

  .short {
    display: inline;
  }
}
</style>
