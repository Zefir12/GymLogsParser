<script setup lang="ts">
import { computed, ref } from "vue";

import MuscleTargetsModal from "@/components/dashboard/MuscleTargetsModal.vue";
import { useDashboardStore } from "@/stores/dashboard";
import { useMuscleTargetsStore } from "@/stores/muscleTargets";
import { CARDIO, formatRange, sortGroups, targetStatus } from "@/utils/dashboard";

const store = useDashboardStore();
const targets = useMuscleTargetsStore();

const editing = ref(false);

/** Smallest value at the right edge of the track; grows with the widest target range. */
const MIN_SCALE = 24;

const pct = (value: number, scale: number) =>
  `${Math.min(100, (value / scale) * 100).toFixed(2)}%`;

const trainedGroups = computed(() => store.groups.filter((g) => g !== CARDIO));

const bars = computed(() => {
  // Groups trained in the loaded data, plus every group whose target asks for
  // sets (min > 0), so a body part that's never trained shows as below target.
  const groups = sortGroups([
    ...trainedGroups.value,
    ...targets.targets.filter((t) => t.minSets > 0).map((t) => t.muscleGroup),
  ]);

  const scale = Math.max(
    MIN_SCALE,
    ...targets.targets.map((t) => Math.ceil(t.maxSets * 1.2)),
  );

  return groups
    .map((group) => {
      const value = store.groupTotal(group, store.toDay - 27, store.toDay) / 4;
      const target = targets.targetOf(group);
      const status = target ? targetStatus(value, target) : null;
      return {
        group,
        value,
        label: value.toFixed(1),
        width: pct(value, scale),
        band: target
          ? {
              left: pct(target.minSets, scale),
              width: pct(target.maxSets - target.minSets, scale),
            }
          : null,
        range: target ? formatRange(target) : "",
        title: target
          ? `${group}: ${value.toFixed(1)} sets/wk, target ${formatRange(target)}`
          : `${group}: ${value.toFixed(1)} sets/wk, no target`,
        status,
        short: status?.replace(" target", "") ?? "",
        active: store.muscle === group,
      };
    })
    .sort((a, b) => b.value - a.value);
});
</script>

<template>
  <section aria-label="Weekly sets per body part" class="card">
    <div class="head">
      <h2>Weekly sets per body part</h2>
      <span class="hint">
        Average of the last 4 weeks · shaded band = target range · secondary
        muscles count by share · click a row to filter
      </span>
      <button type="button" class="edit" @click="editing = true">
        Edit targets
      </button>
    </div>

    <p v-if="targets.error" class="hint notice">
      Targets couldn't be loaded ({{ targets.error }}), so no ranges are shown.
    </p>

    <p v-if="trainedGroups.length === 0" class="hint empty">
      No strength sets logged yet.
    </p>

    <div v-else class="rows">
      <button
        v-for="bar in bars"
        :key="bar.group"
        type="button"
        class="row"
        :class="{ active: bar.active }"
        :aria-pressed="bar.active"
        :title="bar.title"
        @click="store.toggleMuscle(bar.group)"
      >
        <span class="label">{{ bar.group }}</span>
        <span class="track">
          <span v-if="bar.band" class="band" :style="bar.band" />
          <span class="fill" :style="{ width: bar.width }" />
        </span>
        <span class="value">{{ bar.label }}</span>
        <span class="range">{{ bar.range }}</span>
        <span
          class="status"
          :class="bar.status === 'On target' ? 'good' : 'warn'"
        >
          <span class="long">{{ bar.status }}</span>
          <span class="short">{{ bar.short }}</span>
        </span>
      </button>
    </div>

    <MuscleTargetsModal v-if="editing" @close="editing = false" />
  </section>
</template>

<style scoped>
.card {
  padding: var(--card-pad);
  border: 1px solid var(--card-border);
  border-radius: var(--radius-card);
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

.edit {
  margin-left: auto;
  height: 28px;
  padding: 0 12px;

  border: 1px solid var(--card-border);
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--text-secondary);
  font-size: 12px;
}

.edit:hover {
  border-color: var(--glass-border-strong);
  color: var(--text-primary);
}

.notice {
  margin: 8px 0 0;
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
  grid-template-columns: 96px minmax(0, 1fr) 40px 40px 92px;
  align-items: center;
  gap: 12px;
  min-height: 38px;
  padding: 0 8px;

  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--bar-label);

  text-align: left;
}

.row:hover {
  background: var(--glass);
}

.row.active {
  background: rgba(var(--accent-rgb), 0.08);
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
  border-radius: var(--radius-sm);
  background: var(--track);
}

.band {
  position: absolute;
  top: -3px;
  bottom: -3px;
  background: var(--target-band-bg);
  border-left: 1px solid var(--target-band-edge);
  border-right: 1px solid var(--target-band-edge);
}

.fill {
  position: absolute;
  top: 0;
  bottom: 0;
  left: 0;
  border-radius: var(--radius-sm);
  background: var(--bar-idle);
}

.active .fill {
  background: var(--accent);
}

.value {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.range {
  color: var(--text-secondary);
  font-size: 11px;
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

  .range {
    display: none;
  }

  .long {
    display: none;
  }

  .short {
    display: inline;
  }
}
</style>
