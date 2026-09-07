<script setup lang="ts">
import { computed } from "vue";

import BarbellToggle from "./BarbellToggle.vue";

const rawText = defineModel<string>("rawText", { required: true });
const barbellWeightsArePerSide = defineModel<boolean>(
  "barbellWeightsArePerSide",
  { required: true },
);

const props = defineProps<{ loading: boolean }>();

defineEmits<{ (event: "parse"): void }>();

const canParse = computed(
  () => rawText.value.trim().length > 0 && !props.loading,
);
</script>

<template>
  <section class="panel input-panel">
    <div class="panel-header">
      <div>
        <div class="eyebrow">01 / LOG</div>

        <h1>Paste your workout</h1>

        <p>
          Write it however you normally write it. The parser handles the
          structure.
        </p>
      </div>

      <span class="shortcut">AI</span>
    </div>

    <textarea
      v-model="rawText"
      class="workout-input"
      spellcheck="false"
      placeholder="24.06.2022
plecy bola kregosłup
start 17:16

incline 5% 1km 5:36
lawka 0x6, 10kg x6, 20kgx10, 22.5kgx6
deadlift 15kgx4(warmup), 25kgx4, 25kgx3
siady 0x6, 5kgx6, 10kgx8

legpress 2setsx14repsx15kg
hamstring 2setsx15repsx23kg

koniec 18:46"
    />

    <BarbellToggle v-model="barbellWeightsArePerSide" />

    <div class="input-footer">
      <span>{{ rawText.length.toLocaleString() }} characters</span>

      <button
        class="button button-primary"
        type="button"
        :disabled="!canParse"
        @click="$emit('parse')"
      >
        <span v-if="loading" class="spinner" />
        <span v-else>Parse workout</span>
      </button>
    </div>
  </section>
</template>

<style scoped>
.panel {
  min-width: 0;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);

  background: var(--glass);
  backdrop-filter: var(--blur-lg);
  -webkit-backdrop-filter: var(--blur-lg);

  box-shadow: var(--shadow-glass);

  overflow: hidden;
}

.input-panel {
  position: sticky;
  top: 24px;

  display: flex;
  flex-direction: column;

  min-height: 650px;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  gap: 20px;

  padding: 20px 20px 16px;

  border-bottom: 1px solid var(--glass-border);
}

.eyebrow {
  margin-bottom: 6px;

  color: var(--text-quiet);

  font-size: 8px;
  font-weight: 900;
  letter-spacing: 0.12em;
}

.panel-header h1 {
  margin: 0;

  color: var(--text-primary);

  font-size: 16px;
  line-height: 1.2;
  letter-spacing: -0.025em;
}

.panel-header p {
  margin: 6px 0 0;

  color: var(--text-tertiary);

  font-size: 10px;
  line-height: 1.5;
}

.shortcut {
  display: grid;
  place-items: center;

  width: 27px;
  height: 22px;

  border: 1px solid var(--accent-border);
  border-radius: var(--radius-xs);

  background: var(--accent-soft);
  color: var(--accent-text);

  font-size: 8px;
  font-weight: 900;
  letter-spacing: 0.08em;
}

.workout-input {
  flex: 1;

  width: 100%;
  min-height: 460px;

  padding: 18px 20px;

  resize: vertical;

  border: 0;
  outline: 0;

  background: transparent;
  color: var(--text-secondary);

  font-family: "JetBrains Mono", "SFMono-Regular", Consolas, monospace;

  font-size: 11px;
  line-height: 1.7;

  tab-size: 2;
}

.workout-input::placeholder {
  color: var(--text-quiet);
}

.input-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;

  padding: 12px 16px;

  border-top: 1px solid var(--glass-border);

  color: var(--text-quiet);

  font-size: 9px;
}

.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;

  min-height: 33px;
  padding: 0 14px;

  border-radius: var(--radius-xs);

  font-size: 10px;
  font-weight: 800;

  cursor: pointer;

  transition:
    background 150ms var(--ease),
    opacity 150ms var(--ease),
    transform 150ms var(--ease);
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.4;
}

.button-primary {
  border: 1px solid var(--accent-border);

  background: linear-gradient(160deg, var(--accent), #d85a1a);
  color: #1a0a00;

  box-shadow: 0 6px 18px rgba(255, 122, 48, 0.28);
}

.button-primary:hover:not(:disabled) {
  transform: translateY(-1px);
}

.spinner {
  width: 11px;
  height: 11px;

  border: 2px solid rgba(26, 10, 0, 0.55);
  border-top-color: transparent;

  border-radius: 50%;

  animation: spin 700ms linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 1100px) {
  .input-panel {
    position: static;
    min-height: auto;
  }

  .workout-input {
    min-height: 350px;
  }
}
</style>
