<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  usage: {
    cacheHit: boolean;
    cacheHitTokens: number;
    inputTokens: number;
    outputTokens: number;
  };
}>();

const cacheInfo = computed(() => {
  if (props.usage.cacheHit) {
    return `${props.usage.cacheHitTokens.toLocaleString()} cached tokens`;
  }

  return "No cached prefix";
});
</script>

<template>
  <div class="ai-meta">
    <span class="ai-dot" />
    DeepSeek
    <span class="separator">·</span>
    {{ cacheInfo }}
    <span class="separator">·</span>
    {{ usage.inputTokens }} in
    <span class="separator">·</span>
    {{ usage.outputTokens }} out
  </div>
</template>

<style scoped>
.ai-meta {
  display: flex;
  align-items: center;
  gap: 7px;

  margin: 12px 16px 0;

  color: var(--text-tertiary);

  font-size: 8px;
  font-family: "JetBrains Mono", monospace;
}

.ai-dot {
  width: 5px;
  height: 5px;

  border-radius: 50%;

  background: var(--accent);
  box-shadow: 0 0 6px rgba(255, 122, 48, 0.6);
}

.separator {
  color: var(--text-quiet);
}
</style>
