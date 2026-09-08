```vue
<script setup lang="ts">
defineProps<{
  workout: {
    title: string;
    date: string;
    notes: string | null;
    startTime: string | null;
    endTime: string | null;
    persons: string[];
  };
}>();
</script>

<template>
  <div class="workout-meta">
    <label>
      <span>Title</span>
      <input v-model="workout.title" type="text" placeholder="Push day" />
    </label>

    <label>
      <span>Date</span>
      <input v-model="workout.date" type="date" />
    </label>

    <label>
      <span>Start time</span>
      <input
        v-model="workout.startTime"
        type="text"
        inputmode="numeric"
        placeholder="17:47"
        pattern="^([01]\d|2[0-3]):[0-5]\d$"
      />
    </label>

    <label>
      <span>End time</span>
      <input
        v-model="workout.endTime"
        type="text"
        inputmode="numeric"
        placeholder="19:40"
        pattern="^([01]\d|2[0-3]):[0-5]\d$"
      />
    </label>

    <label class="full-width">
      <span>Persons</span>
      <input
        :value="workout.persons.join(', ')"
        type="text"
        placeholder="Karol, Marek, Hubert"
        @input="
          workout.persons = ($event.target as HTMLInputElement).value
            .split(',')
            .map((person) => person.trim())
            .filter(Boolean)
        "
      />
    </label>

    <label class="full-width">
      <span>Notes</span>
      <textarea
        v-model="workout.notes"
        rows="2"
        placeholder="Workout notes..."
      />
    </label>
  </div>
</template>

<style scoped>
.workout-meta {
  display: grid;
  grid-template-columns: 1fr 170px;
  gap: 10px;

  padding: 16px;

  border-bottom: 1px solid var(--glass-border);
}

.workout-meta label {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.workout-meta .full-width {
  grid-column: 1 / -1;
}

.workout-meta label > span {
  color: var(--text-quiet);

  font-size: 8px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.workout-meta input,
.workout-meta textarea {
  width: 100%;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-sm);

  outline: 0;

  background: var(--glass-soft);
  color: var(--text-secondary);

  font: inherit;
  font-size: 10px;

  transition: border-color 150ms var(--ease);
}

.workout-meta input {
  height: 32px;
  padding: 0 10px;
}

.workout-meta textarea {
  padding: 8px 10px;
  resize: vertical;
}

.workout-meta input:focus,
.workout-meta textarea:focus {
  border-color: var(--accent-border);
}

@media (max-width: 700px) {
  .workout-meta {
    grid-template-columns: 1fr;
  }

  .workout-meta .full-width {
    grid-column: auto;
  }
}
</style>
```
