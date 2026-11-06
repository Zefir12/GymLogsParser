<script setup lang="ts" generic="T extends string">
defineProps<{
  options: { value: T; label: string }[];
  label: string;
  compact?: boolean;
}>();

const model = defineModel<T>({ required: true });
</script>

<template>
  <div role="group" :aria-label="label" class="segmented" :class="{ compact }">
    <button
      v-for="option in options"
      :key="option.value"
      type="button"
      class="segment"
      :class="{ on: model === option.value }"
      :aria-pressed="model === option.value"
      @click="model = option.value"
    >
      {{ option.label }}
    </button>
  </div>
</template>

<style scoped>
.segmented {
  display: flex;
  gap: 4px;
  padding: 4px;

  border: 1px solid var(--card-border);
  border-radius: var(--radius-control);
  background: var(--segment-bg);
}

.segment {
  min-width: 52px;
  min-height: 36px;
  padding: 0 12px;

  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--text-secondary);

  font-weight: 600;
}

.compact .segment {
  min-width: 0;
  min-height: 34px;
  font-weight: 500;
}

.segment:hover {
  color: var(--text-primary);
}

.segment.on {
  background: var(--segment-on);
  color: var(--text-primary);
}
</style>
