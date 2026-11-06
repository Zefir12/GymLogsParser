<script setup lang="ts">
import { computed } from "vue";

import { useDashboardStore } from "@/stores/dashboard";
import { ALL, formatKg, signed } from "@/utils/dashboard";

const store = useDashboardStore();

const heading = computed(() =>
  store.muscle === ALL ? "main lifts" : store.muscle,
);

const rows = computed(() =>
  store.exerciseRows.map((row) => ({
    id: row.exercise.exerciseId,
    name: row.exercise.name,
    sessions: row.sessions,
    best: row.best !== null ? formatKg(row.best) : "–",
    change: row.change !== null ? `${signed(row.change)}%` : "–",
    changeUp: (row.change ?? 0) >= 0,
    active: store.chartExercise?.exerciseId === row.exercise.exerciseId,
  })),
);
</script>

<template>
  <section aria-label="Exercises" class="card">
    <h2>Exercises · {{ heading }}</h2>

    <p v-if="rows.length === 0" class="empty">
      No exercises logged for this body part yet.
    </p>

    <div v-else class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Exercise</th>
            <th class="num">Sessions</th>
            <th class="num">Best e1RM</th>
            <th class="num">Change</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.id" :class="{ active: row.active }">
            <td>
              <button
                type="button"
                class="pick"
                :aria-pressed="row.active"
                @click="store.exerciseId = row.id"
              >
                {{ row.name }}
              </button>
            </td>
            <td class="num">{{ row.sessions }}</td>
            <td class="num">{{ row.best }}</td>
            <td
              class="num"
              :class="row.change === '–' ? '' : row.changeUp ? 'good' : 'warn'"
            >
              {{ row.change }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.card {
  min-width: 0;
  padding: var(--card-pad);
  border: 1px solid var(--card-border);
  border-radius: var(--radius-card);
  background: var(--card-bg);
}

h2 {
  margin: 0 0 12px;
  color: var(--text-primary);
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 600;
}

.empty {
  margin: 0;
  color: var(--text-secondary);
}

.table-wrap {
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  color: var(--text-primary);
}

th {
  padding: 8px;
  color: var(--text-secondary);
  font-weight: 500;
  text-align: left;
}

td {
  padding: 8px;
}

th:first-child,
td:first-child {
  padding-left: 0;
}

th:last-child,
td:last-child {
  padding-right: 0;
}

tbody tr {
  border-top: 1px solid rgba(255, 255, 255, 0.06);
}

tbody tr.active {
  background: rgba(var(--accent-rgb), 0.07);
}

.num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.pick {
  min-height: 32px;
  padding: 0;
  border: 0;
  background: none;
  color: var(--text-primary);
  font-weight: 500;
  text-align: left;
}

.pick:hover,
.active .pick {
  color: var(--accent-text);
}

.good {
  color: var(--success);
}

.warn {
  color: var(--warning);
}
</style>
