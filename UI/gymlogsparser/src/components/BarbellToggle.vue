<script setup lang="ts">
defineProps<{ modelValue: boolean }>();
defineEmits<{ (event: "update:modelValue", value: boolean): void }>();
</script>

<template>
  <div class="parser-options">
    <label class="checkbox-option">
      <input
        type="checkbox"
        :checked="modelValue"
        @change="
          $emit(
            'update:modelValue',
            ($event.target as HTMLInputElement).checked,
          )
        "
      />

      <span class="checkbox-ui" />

      <span class="checkbox-copy">
        <strong>Barbell weights are per side</strong>

        <small>40 kg = 40 kg each side + 20 kg bar → 100 kg total</small>
      </span>
    </label>
  </div>
</template>

<style scoped>
.parser-options {
  margin: 0 16px 12px;
  padding: 13px;

  border: 1px solid var(--glass-border);
  border-radius: var(--radius-md);

  background: var(--glass-soft);
}

.checkbox-option {
  display: flex;
  align-items: flex-start;
  gap: 10px;

  cursor: pointer;
}

.checkbox-option input {
  position: absolute;

  opacity: 0;
  pointer-events: none;
}

.checkbox-ui {
  position: relative;

  flex: 0 0 auto;

  width: 17px;
  height: 17px;

  margin-top: 1px;

  border: 1px solid var(--glass-border-strong);
  border-radius: 5px;

  background: var(--bg-void);

  transition:
    background 150ms var(--ease),
    border-color 150ms var(--ease);
}

.checkbox-option input:checked + .checkbox-ui {
  border-color: var(--accent);
  background: var(--accent);
}

.checkbox-option input:checked + .checkbox-ui::after {
  content: "";

  position: absolute;

  left: 4px;
  top: 1px;

  width: 5px;
  height: 9px;

  border: solid #1a0a00;
  border-width: 0 2px 2px 0;

  transform: rotate(45deg);
}

.checkbox-copy {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.checkbox-copy strong {
  color: var(--text-secondary);

  font-size: 10px;
}

.checkbox-copy small {
  color: var(--text-tertiary);

  font-size: 9px;
  line-height: 1.45;
}
</style>
