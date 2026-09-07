<script setup lang="ts">
defineProps<{
  variant: "error" | "success";
  message: string;
}>();
</script>

<template>
  <div class="notice" :class="`notice-${variant}`">
    {{ message }}
  </div>
</template>

<style scoped>
.notice {
  margin: 12px 16px 0;
  padding: 10px 12px;

  border-radius: var(--radius-sm);

  font-size: 10px;
  backdrop-filter: var(--blur-md);
  -webkit-backdrop-filter: var(--blur-md);
}

.notice-error {
  border: 1px solid var(--danger-border);
  background: var(--danger-bg);
  color: var(--danger);
}

.notice-success {
  border: 1px solid var(--success-border);
  background: var(--success-bg);
  color: var(--success);
}
</style>
