<script setup lang="ts">
defineProps<{
  variant: "success" | "error";
  message: string;
}>();
</script>

<template>
  <Transition name="toast">
    <div class="toast" :class="`toast-${variant}`" role="status">
      <span class="toast-icon">
        {{ variant === "success" ? "✓" : "!" }}
      </span>

      <span>{{ message }}</span>
    </div>
  </Transition>
</template>

<style scoped>
.toast {
  position: fixed;
  top: 20px;
  right: 20px;
  z-index: 1000;

  display: flex;
  align-items: center;
  gap: 9px;

  min-width: 220px;
  max-width: 360px;
  padding: 11px 14px;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-sm);

  background: var(--glass);
  backdrop-filter: var(--blur-lg);
  -webkit-backdrop-filter: var(--blur-lg);

  box-shadow: var(--shadow-glass);

  color: var(--text-primary);
  font-size: 10px;
  font-weight: 700;
}

.toast-success {
  border-color: rgba(70, 190, 120, 0.35);
}

.toast-error {
  border-color: rgba(220, 70, 70, 0.35);
}

.toast-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;

  width: 19px;
  height: 19px;
  flex: 0 0 19px;

  border-radius: 50%;

  font-size: 10px;
  font-weight: 900;
}

.toast-success .toast-icon {
  background: rgba(70, 190, 120, 0.15);
  color: #46be78;
}

.toast-error .toast-icon {
  background: rgba(220, 70, 70, 0.15);
  color: #dc4646;
}

.toast-enter-active,
.toast-leave-active {
  transition:
    opacity 180ms ease,
    transform 180ms ease;
}

.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}

@media (max-width: 600px) {
  .toast {
    top: 12px;
    right: 12px;
    left: 12px;

    min-width: 0;
    max-width: none;
  }
}
</style>
