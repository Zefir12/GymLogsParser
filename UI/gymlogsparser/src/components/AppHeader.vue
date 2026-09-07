<script setup lang="ts">
defineEmits<{ (event: "new-workout"): void }>();
</script>

<template>
  <header class="topbar">
    <div>
      <div class="brand">
        <span class="brand-mark">G</span>
        <span>GymLog</span>
      </div>

      <p class="subtitle">
        Turn messy workout notes into structured training data.
      </p>
    </div>

    <button
      class="button button-secondary"
      type="button"
      @click="$emit('new-workout')"
    >
      New workout
    </button>
  </header>
</template>

<style scoped>
.topbar {
  max-width: 1500px;
  margin: 0 auto 22px;

  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 9px;

  color: var(--text-primary);

  font-size: 15px;
  font-weight: 800;
  letter-spacing: -0.02em;
}

.brand-mark {
  display: grid;
  place-items: center;

  width: 27px;
  height: 27px;

  border-radius: var(--radius-xs);

  background: linear-gradient(155deg, var(--accent), #d85a1a);
  color: #1a0a00;

  font-size: 12px;
  font-weight: 900;

  box-shadow: 0 4px 14px rgba(255, 122, 48, 0.35);
}

.subtitle {
  margin: 7px 0 0;

  color: var(--text-tertiary);

  font-size: 11px;
}

.button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;

  min-height: 33px;
  padding: 0 13px;

  border-radius: var(--radius-xs);

  font-size: 10px;
  font-weight: 800;

  cursor: pointer;

  transition:
    background 150ms var(--ease),
    border-color 150ms var(--ease),
    color 150ms var(--ease);
}

.button-secondary {
  border: 1px solid var(--glass-border);

  background: var(--glass);
  backdrop-filter: var(--blur-md);
  -webkit-backdrop-filter: var(--blur-md);

  color: var(--text-secondary);
}

.button-secondary:hover {
  border-color: var(--glass-border-strong);
  color: var(--text-primary);
}

@media (max-width: 700px) {
  .topbar {
    align-items: center;
  }

  .subtitle {
    display: none;
  }
}
</style>
