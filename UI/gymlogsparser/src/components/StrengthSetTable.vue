<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref } from "vue";
import { useWorkoutStore } from "@/stores/workout";

const props = defineProps<{ exercise: any }>();

const store = useWorkoutStore();

function addSet() {
  store.addSet(props.exercise);
}

function removeSet(setIndex: number) {
  store.removeSet(props.exercise, setIndex);
  if (openNoteIndex.value === setIndex) closeNotes();
}

// ---- notes popover ----
const openNoteIndex = ref<number | null>(null);
const popoverStyle = ref<Record<string, string>>({});
const buttonRefs: Record<number, HTMLElement> = {};
const popoverEl = ref<HTMLElement | null>(null);

function setButtonRef(el: Element | null, idx: number) {
  if (el) buttonRefs[idx] = el as HTMLElement;
}

async function toggleNotes(idx: number) {
  if (openNoteIndex.value === idx) {
    closeNotes();
    return;
  }
  openNoteIndex.value = idx;
  await nextTick();
  positionPopover(idx);
  popoverEl.value?.querySelector("textarea")?.focus();
}

function positionPopover(idx: number) {
  const btn = buttonRefs[idx];
  if (!btn) return;

  const rect = btn.getBoundingClientRect();
  const popoverWidth = 220;

  const left = Math.min(
    Math.max(8, rect.left),
    window.innerWidth - popoverWidth - 8,
  );

  popoverStyle.value = {
    top: `${rect.bottom + 6}px`,
    left: `${left}px`,
  };
}

function closeNotes() {
  openNoteIndex.value = null;
}

function onDocPointerDown(e: MouseEvent) {
  if (openNoteIndex.value === null) return;

  const target = e.target as Node;
  const btn = buttonRefs[openNoteIndex.value];

  if (popoverEl.value?.contains(target)) return;
  if (btn?.contains(target)) return;

  closeNotes();
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === "Escape") closeNotes();
}

onMounted(() => {
  document.addEventListener("mousedown", onDocPointerDown);
  document.addEventListener("keydown", onKeydown);
});

onBeforeUnmount(() => {
  document.removeEventListener("mousedown", onDocPointerDown);
  document.removeEventListener("keydown", onKeydown);
});
</script>

<template>
  <div class="set-table">
    <div class="set-row set-head">
      <span>SET</span>
      <span>WEIGHT</span>
      <span>REPS</span>
      <span>RIR</span>
      <span>RPE</span>
      <span>TYPE</span>
      <span />
      <span />
    </div>

    <div
      v-for="(set, setIndex) in exercise.sets"
      :key="setIndex"
      class="set-row"
    >
      <input
        v-model.number="set.setNumber"
        type="number"
        min="1"
        class="set-number"
      />

      <div class="weight-cell">
        <input
          v-model.number="set.weight"
          type="number"
          min="0"
          step="0.25"
          placeholder="—"
        />

        <span
          v-if="set.weightEntryMode === 'PerSide'"
          class="weight-mode"
          title="Original value was interpreted as weight per side"
        >
          /side
        </span>
        <span v-else class="weight-mode">kg</span>
      </div>

      <input v-model.number="set.reps" type="number" min="0" placeholder="—" />

      <input
        v-model.number="set.rir"
        type="number"
        min="0"
        step="0.5"
        placeholder="—"
      />

      <input
        v-model.number="set.rpe"
        type="number"
        min="1"
        max="10"
        step="0.5"
        placeholder="—"
      />

      <select v-model="set.warmup">
        <option :value="null">Normal</option>
        <option :value="true">Warm-up</option>
        <option :value="false">Working</option>
      </select>

      <button
        type="button"
        class="notes-btn"
        :class="{
          'has-notes': !!set.notes,
          active: openNoteIndex === setIndex,
        }"
        :ref="(el) => setButtonRef(el as Element | null, setIndex as number)"
        :title="set.notes ? 'Edit set note' : 'Add set note'"
        @click="toggleNotes(setIndex as number)"
      >
        <svg viewBox="0 0 16 16" width="11" height="11">
          <path
            fill="currentColor"
            d="M3 2h7l3 3v9a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1z"
            opacity="0"
          />
          <path
            fill="none"
            stroke="currentColor"
            stroke-width="1.2"
            d="M3.5 2.5h6l3 3v8a1 1 0 0 1-1 1h-8a1 1 0 0 1-1-1v-10a1 1 0 0 1 1-1z"
          />
          <path
            fill="none"
            stroke="currentColor"
            stroke-width="1"
            d="M9.5 2.5v3h3"
          />
          <path
            fill="none"
            stroke="currentColor"
            stroke-width="1"
            stroke-linecap="round"
            d="M5 8.5h5M5 10.5h5M5 6.5h2"
          />
        </svg>
        <span v-if="set.notes" class="notes-dot" />
      </button>

      <button
        class="remove-set"
        type="button"
        @click="removeSet(setIndex as number)"
      >
        ×
      </button>
    </div>

    <div class="exercise-footer">
      <button class="text-button" type="button" @click="addSet">
        + Add set
      </button>

      <input
        v-model="exercise.notes"
        class="exercise-notes"
        type="text"
        placeholder="Exercise notes..."
      />
    </div>

    <Teleport to="body">
      <div
        v-if="openNoteIndex !== null"
        ref="popoverEl"
        class="notes-popover"
        :style="popoverStyle"
      >
        <div class="notes-popover-head">
          <span>
            Set
            {{ exercise.sets[openNoteIndex].setNumber ?? openNoteIndex + 1 }}
            note
          </span>
          <button type="button" class="notes-popover-close" @click="closeNotes">
            ×
          </button>
        </div>

        <textarea
          v-model="exercise.sets[openNoteIndex].notes"
          class="notes-popover-textarea"
          placeholder="Set notes..."
          rows="3"
        />
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.set-table {
  width: 100%;
}

.set-row {
  display: grid;

  grid-template-columns:
    52px
    minmax(85px, 0.5fr)
    minmax(55px, 0.65fr)
    minmax(50px, 0.6fr)
    minmax(50px, 0.6fr)
    minmax(90px, 0.9fr)
    30px
    30px;

  align-items: center;

  min-width: 0;

  border-bottom: 1px solid var(--glass-border);
}

.set-row > * {
  min-width: 0;
}

.set-head {
  min-height: 26px;

  background: var(--glass-soft);
}

.set-head span {
  padding: 0 7px;

  color: var(--text-quiet);

  font-size: 7px;
  font-weight: 900;
  letter-spacing: 0.06em;
}

.set-row:not(.set-head) {
  min-height: 42px;
}

.set-row input,
.set-row select {
  width: calc(100% - 10px);
  height: 29px;

  margin: 0 5px;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  outline: 0;

  background: var(--glass-soft);
  color: var(--text-secondary);

  font-size: 9px;

  transition: border-color 150ms var(--ease);
}

.set-row input {
  padding: 0 7px;
}

.set-row select {
  padding: 0 5px;
}

.set-row input:focus,
.set-row select:focus {
  border-color: var(--accent-border);
}

.set-number {
  text-align: center;
}

.weight-cell {
  position: relative;

  display: flex;
  align-items: center;
}

.weight-cell input {
  padding-right: 25px;
}

.weight-mode {
  position: absolute;
  right: 11px;

  color: var(--text-quiet);

  font-size: 7px;
  pointer-events: none;
}

.notes-btn {
  position: relative;

  display: grid;
  place-items: center;

  width: 22px;
  height: 22px;

  margin: auto;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  background: var(--glass-soft);

  color: var(--text-quiet);

  cursor: pointer;

  transition:
    color 150ms var(--ease),
    border-color 150ms var(--ease),
    background 150ms var(--ease);
}

.notes-btn:hover {
  border-color: var(--accent-border);
  color: var(--text-secondary);
}

.notes-btn.active {
  border-color: var(--accent-border);
  background: var(--accent-bg, var(--glass-soft));
  color: var(--accent-text);
}

.notes-btn.has-notes {
  border-color: var(--accent-border);
  color: var(--accent-text);
}

.notes-dot {
  position: absolute;
  top: -3px;
  right: -3px;

  width: 6px;
  height: 6px;

  border-radius: 50%;
  background: var(--accent-strong, var(--accent-text));
}

.remove-set {
  display: grid;
  place-items: center;

  width: 22px;
  height: 22px;

  margin: auto;

  border: 0;
  border-radius: var(--radius-xs);

  background: transparent;

  color: var(--text-quiet);

  font-size: 14px;

  cursor: pointer;

  transition:
    color 150ms var(--ease),
    background 150ms var(--ease);
}

.remove-set:hover {
  background: var(--danger-bg);
  color: var(--danger);
}

.exercise-footer {
  display: flex;
  align-items: center;
  gap: 10px;

  padding: 9px 10px;
}

.text-button {
  flex: 0 0 auto;

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
  .set-table {
    overflow-x: auto;
  }

  .set-row {
    min-width: 660px;
  }
}
</style>

<style>
.notes-popover {
  position: fixed;
  z-index: 1000;

  width: 220px;

  padding: 8px;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  background: var(--glass-strong, var(--glass-soft));
  backdrop-filter: blur(12px);

  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.25);
}

.notes-popover-head {
  display: flex;
  align-items: center;
  justify-content: space-between;

  margin-bottom: 6px;

  color: var(--text-quiet);

  font-size: 7px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.notes-popover-close {
  border: 0;
  background: transparent;

  color: var(--text-quiet);

  font-size: 13px;
  line-height: 1;

  cursor: pointer;
}

.notes-popover-close:hover {
  color: var(--text-secondary);
}

.notes-popover-textarea {
  width: 100%;

  padding: 6px 7px;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xs);

  outline: 0;
  resize: vertical;

  background: var(--glass-soft);
  color: var(--text-secondary);

  font-family: inherit;
  font-size: 8.5px;
  line-height: 1.4;

  transition: border-color 150ms var(--ease);
}

.notes-popover-textarea:focus {
  border-color: var(--accent-border);
}
</style>
