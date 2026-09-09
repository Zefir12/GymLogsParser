<script setup lang="ts">
import { computed, ref, watch } from "vue";

export interface SearchableSelectOption {
  value: string;
  label: string;
  sublabel?: string;
}

const props = defineProps<{
  modelValue: string;
  options: SearchableSelectOption[];
  placeholder?: string;
}>();

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

const query = ref("");
const open = ref(false);
const highlightedIndex = ref(0);

const selectedOption = computed(
  () => props.options.find((o) => o.value === props.modelValue) ?? null,
);

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase();
  if (!q) return props.options;

  return props.options.filter(
    (o) =>
      o.label.toLowerCase().includes(q) ||
      o.sublabel?.toLowerCase().includes(q),
  );
});

watch(filtered, () => {
  highlightedIndex.value = 0;
});

function openDropdown() {
  open.value = true;
  query.value = "";
  highlightedIndex.value = Math.max(
    0,
    filtered.value.findIndex((o) => o.value === props.modelValue),
  );
}

function closeDropdown() {
  open.value = false;
  query.value = "";
}

function selectOption(option: SearchableSelectOption) {
  emit("update:modelValue", option.value);
  closeDropdown();
}

function moveHighlight(delta: number) {
  if (!open.value) {
    openDropdown();
    return;
  }
  const max = filtered.value.length - 1;
  if (max < 0) return;
  highlightedIndex.value = Math.min(
    max,
    Math.max(0, highlightedIndex.value + delta),
  );
}

function confirmHighlighted() {
  const option = filtered.value[highlightedIndex.value];
  if (option) selectOption(option);
}
</script>

<template>
  <div class="searchable-select">
    <div class="input-wrap">
      <input
        class="select-input"
        type="text"
        :value="open ? query : (selectedOption?.label ?? '')"
        :placeholder="placeholder ?? 'Search…'"
        @focus="openDropdown"
        @input="query = ($event.target as HTMLInputElement).value"
        @blur="closeDropdown"
        @keydown.down.prevent="moveHighlight(1)"
        @keydown.up.prevent="moveHighlight(-1)"
        @keydown.enter.prevent="confirmHighlighted"
        @keydown.escape="closeDropdown"
      />
      <svg
        class="chevron"
        :class="{ flipped: open }"
        width="14"
        height="14"
        viewBox="0 0 14 14"
        fill="none"
      >
        <path
          d="M3 5.5L7 9.5L11 5.5"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
      </svg>
    </div>

    <ul v-if="open" class="dropdown">
      <li v-if="filtered.length === 0" class="empty">No matches</li>
      <li
        v-for="(option, i) in filtered"
        :key="option.value"
        class="option"
        :class="{
          active: i === highlightedIndex,
          selected: option.value === modelValue,
        }"
        @mousedown.prevent="selectOption(option)"
        @mouseenter="highlightedIndex = i"
      >
        <span class="option-label">{{ option.label }}</span>
        <span v-if="option.sublabel" class="option-sublabel">
          {{ option.sublabel }}
        </span>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.searchable-select {
  position: relative;
  min-width: 220px;
}

.input-wrap {
  position: relative;
  display: flex;
  align-items: center;
}

.select-input {
  width: 100%;
  padding: 8px 32px 8px 12px;

  font-size: 0.9rem;
  color: var(--text-primary);

  background: var(--glass);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);

  cursor: pointer;
}

.select-input::placeholder {
  color: var(--text-tertiary);
}

.select-input:focus {
  outline: none;
  border-color: var(--accent-border);
  background: var(--glass-strong);
}

.chevron {
  position: absolute;
  right: 10px;

  color: var(--text-tertiary);
  pointer-events: none;

  transition: transform 0.15s var(--ease);
}

.chevron.flipped {
  transform: rotate(180deg);
}

.dropdown {
  position: absolute;
  z-index: 20;
  top: calc(100% + 6px);
  left: 0;
  right: 0;

  max-height: 260px;
  overflow-y: auto;

  margin: 0;
  padding: 4px;
  list-style: none;

  background: var(--bg-void);
  backdrop-filter: var(--blur-md);
  border: 1px solid var(--glass-border-strong);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-glass);
}

.option {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;

  padding: 8px 10px;

  font-size: 0.88rem;
  color: var(--text-secondary);

  border-radius: var(--radius-sm);
  cursor: pointer;
}

.option.active {
  background: var(--glass-strong);
  color: var(--text-primary);
}

.option.selected .option-label {
  color: var(--accent-text);
}

.option-sublabel {
  font-size: 0.75rem;
  color: var(--text-quiet);
  white-space: nowrap;
}

.empty {
  padding: 10px;

  font-size: 0.85rem;
  color: var(--text-tertiary);
  text-align: center;
}
</style>
