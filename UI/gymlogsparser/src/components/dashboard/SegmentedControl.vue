<script setup lang="ts" generic="T extends string">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";

const props = defineProps<{
  options: { value: T; label: string }[];
  label: string;
  compact?: boolean;
}>();

const model = defineModel<T>({ required: true });

const root = ref<HTMLElement | null>(null);
const buttons = ref<HTMLButtonElement[]>([]);

/** The grey block behind the selected option; it slides to the new one. */
const thumb = ref<{ left: string; width: string } | null>(null);
/** Off for the first placement, so the block doesn't fly in from the left. */
const animate = ref(false);

function place() {
  const index = props.options.findIndex((o) => o.value === model.value);
  const button = buttons.value[index];
  thumb.value = button
    ? { left: `${button.offsetLeft}px`, width: `${button.offsetWidth}px` }
    : null;
}

let observer: ResizeObserver | null = null;

onMounted(() => {
  place();
  // enable the slide only after the first placement has painted
  requestAnimationFrame(() => (animate.value = true));
  if (typeof ResizeObserver !== "undefined" && root.value) {
    observer = new ResizeObserver(() => place());
    observer.observe(root.value);
  }
});

onBeforeUnmount(() => observer?.disconnect());

watch([model, () => props.options], () => nextTick(place), { deep: true });
</script>

<template>
  <div
    ref="root"
    role="group"
    :aria-label="label"
    class="segmented"
    :class="{ compact }"
  >
    <span
      v-if="thumb"
      class="thumb"
      :class="{ animate }"
      :style="thumb"
      aria-hidden="true"
    />
    <button
      v-for="option in options"
      :key="option.value"
      ref="buttons"
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
  position: relative;
  display: flex;
  gap: 4px;
  padding: 4px;

  border: 1px solid var(--card-border);
  border-radius: var(--radius-control);
  background: var(--segment-bg);
}

.thumb {
  position: absolute;
  top: 4px;
  bottom: 4px;
  border-radius: var(--radius-sm);
  background: var(--segment-on);
  pointer-events: none;
}

.thumb.animate {
  transition:
    left 0.22s var(--ease),
    width 0.22s var(--ease);
}

@media (prefers-reduced-motion: reduce) {
  .thumb.animate {
    transition: none;
  }
}

.segment {
  position: relative; /* above the thumb */
  min-width: 52px;
  min-height: 36px;
  padding: 0 12px;

  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--text-secondary);

  font-weight: 600;
  transition: color 0.15s var(--ease);
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
  color: var(--text-primary);
}
</style>
