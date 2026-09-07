<script setup lang="ts">
import { formatDuration } from "@/utils/format";

const props = defineProps<{ exercise: any }>();

function addCardioEntry() {
  props.exercise.cardio.push({
    activity: props.exercise.name,
    durationSeconds: null,
    distanceKm: null,
    speedKmh: null,
    inclinePercent: null,
    notes: null,
  });
}

function removeCardioEntry(cardioIndex: number) {
  props.exercise.cardio.splice(cardioIndex, 1);
}

function setDurationFromMinutes(cardioIndex: number, event: Event) {
  const minutes = Number((event.target as HTMLInputElement).value);
  props.exercise.cardio[cardioIndex].durationSeconds = minutes * 60;
}
</script>

<template>
  <div class="cardio-editor">
    <div
      v-for="(cardio, cardioIndex) in exercise.cardio"
      :key="cardioIndex"
      class="cardio-entry"
    >
      <div class="cardio-grid">
        <label>
          <span>Duration</span>

          <input
            :value="
              cardio.durationSeconds !== null
                ? Math.round(cardio.durationSeconds / 60)
                : ''
            "
            type="number"
            min="0"
            step="1"
            placeholder="min"
            @input="setDurationFromMinutes(cardioIndex as number, $event)"
          />

          <small v-if="cardio.durationSeconds !== null">
            {{ formatDuration(cardio.durationSeconds) }}
          </small>
        </label>

        <label>
          <span>Distance</span>
          <div class="input-with-unit">
            <input
              v-model.number="cardio.distanceKm"
              type="number"
              min="0"
              step="0.01"
              placeholder="—"
            />
            <em>km</em>
          </div>
        </label>

        <label>
          <span>Speed</span>
          <div class="input-with-unit">
            <input
              v-model.number="cardio.speedKmh"
              type="number"
              min="0"
              step="0.1"
              placeholder="—"
            />
            <em>km/h</em>
          </div>
        </label>

        <label>
          <span>Incline</span>
          <div class="input-with-unit">
            <input
              v-model.number="cardio.inclinePercent"
              type="number"
              min="0"
              step="0.5"
              placeholder="—"
            />
            <em>%</em>
          </div>
        </label>

        <button
          class="icon-button danger cardio-remove"
          type="button"
          title="Remove cardio entry"
          @click="removeCardioEntry(cardioIndex as number)"
        >
          ×
        </button>
      </div>

      <input
        v-model="cardio.notes"
        class="cardio-notes"
        type="text"
        placeholder="Cardio notes..."
      />
    </div>

    <button class="text-button" type="button" @click="addCardioEntry">
      + Add cardio entry
    </button>
  </div>

  <div class="exercise-footer">
    <input
      v-model="exercise.notes"
      class="exercise-notes"
      type="text"
      placeholder="Exercise notes..."
    />
  </div>
</template>

<style scoped>
.cardio-editor {
  padding: 10px;

  border-bottom: 1px solid var(--glass-border);
}

.cardio-entry {
  padding: 10px;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-sm);

  background: var(--glass-soft);
}

.cardio-entry + .cardio-entry {
  margin-top: 7px;
}

.cardio-grid {
  display: grid;
  grid-template-columns:
    repeat(4, minmax(90px, 1fr))
    28px;

  gap: 8px;

  align-items: end;
}

.cardio-grid label {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.cardio-grid label > span {
  color: var(--text-tertiary);

  font-size: 7px;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.cardio-grid label > small {
  color: var(--text-quiet);
  font-size: 7px;
}

.cardio-grid input {
  width: 100%;
  height: 29px;

  padding: 0 7px;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  outline: 0;

  background: var(--bg-void);
  color: var(--text-secondary);

  font-size: 9px;

  transition: border-color 150ms var(--ease);
}

.cardio-grid input:focus {
  border-color: var(--accent-border);
}

.input-with-unit {
  position: relative;
}

.input-with-unit input {
  padding-right: 34px;
}

.input-with-unit em {
  position: absolute;
  right: 8px;
  top: 50%;

  transform: translateY(-50%);

  color: var(--text-quiet);

  font-size: 7px;
  font-style: normal;
}

.icon-button {
  display: grid;
  place-items: center;

  width: 26px;
  height: 26px;

  flex: 0 0 auto;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  background: transparent;

  color: var(--text-tertiary);

  cursor: pointer;

  transition:
    border-color 150ms var(--ease),
    color 150ms var(--ease);
}

.icon-button.danger:hover {
  border-color: var(--danger-border);
  color: var(--danger);
}

.cardio-remove {
  margin-bottom: 1px;
}

.cardio-notes {
  width: 100%;
  height: 28px;

  margin-top: 8px;
  padding: 0 8px;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  outline: 0;

  background: var(--bg-void);
  color: var(--text-secondary);

  font-size: 8px;

  transition: border-color 150ms var(--ease);
}

.cardio-notes:focus {
  border-color: var(--accent-border);
}

.text-button {
  border: 0;
  background: transparent;

  color: var(--accent-text);

  font-size: 8px;
  font-weight: 800;

  cursor: pointer;
}

.text-button:hover {
  color: var(--accent-strong);
}

.exercise-footer {
  display: flex;
  align-items: center;
  gap: 10px;

  padding: 9px 10px;
}

.exercise-notes {
  flex: 1;
  min-width: 0;

  height: 28px;

  padding: 0 8px;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  outline: 0;

  background: var(--glass-soft);
  color: var(--text-secondary);

  font-size: 8px;

  transition: border-color 150ms var(--ease);
}

.exercise-notes:focus {
  border-color: var(--accent-border);
}

@media (max-width: 700px) {
  .cardio-grid {
    grid-template-columns: 1fr 1fr;
  }

  .cardio-remove {
    position: absolute;
  }
}
</style>
