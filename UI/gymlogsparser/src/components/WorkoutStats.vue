<script setup lang="ts">
defineProps<{
  exerciseCount: number;
  strengthCount: number;
  cardioCount: number;
  setCount: number;
}>();
</script>

<template>
  <div class="stats">
    <div class="stat">
      <strong>{{ exerciseCount }}</strong>
      <span>exercises</span>
    </div>

    <div class="stat">
      <strong>{{ strengthCount }}</strong>
      <span>strength</span>
    </div>

    <div class="stat">
      <strong>{{ cardioCount }}</strong>
      <span>cardio</span>
    </div>

    <div class="stat">
      <strong>{{ setCount }}</strong>
      <span>sets</span>
    </div>
  </div>
</template>

<style scoped>
.stats {
  display: flex;
  gap: 14px;
}

.stat {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 2px;
}

.stat strong {
  color: var(--text-primary);

  font-size: 13px;
  line-height: 1;
}

.stat span {
  color: var(--text-quiet);

  font-size: 7px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}
</style>
