<script setup lang="ts">
import { themes, useThemeStore } from "@/stores/theme";

const store = useThemeStore();

function onChange(event: Event) {
  store.setTheme((event.target as HTMLSelectElement).value);
}
</script>

<template>
  <label class="theme-switcher">
    <span class="sr-only">Palette</span>
    <select :value="store.theme" title="Palette" @change="onChange">
      <option v-for="t in themes" :key="t.id" :value="t.id">
        {{ t.label }}
      </option>
    </select>
  </label>
</template>

<style scoped>
.theme-switcher {
  display: inline-flex;
}

select {
  min-height: 36px;
  padding: 0 var(--card-pad-sm);

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-control);
  background: var(--segment-bg);
  color: var(--text-secondary);

  font-size: 0.85rem;
  color-scheme: dark;
  cursor: pointer;

  transition:
    color 0.15s var(--ease),
    border-color 0.15s var(--ease);
}

select:hover,
select:focus-visible {
  border-color: var(--glass-border-strong);
  color: var(--text-primary);
}

select:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 1px;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}

/* 16px so iOS doesn't zoom on focus */
@media (max-width: 700px) {
  select {
    font-size: 16px;
  }
}
</style>
