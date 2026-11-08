<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref } from "vue";

import type { WorkoutChoice } from "@/stores/dashboard";

/**
 * Asks which workout to open when a training day or chart point covers more
 * than one (#73). Picking one emits `pick`; the caller opens the editor.
 */
const props = defineProps<{
  /** Shown above the title, e.g. "Mon 3 Nov". */
  dateLabel: string;
  choices: WorkoutChoice[];
}>();

const emit = defineEmits<{
  pick: [id: string];
  close: [];
}>();

const dialogEl = ref<HTMLElement | null>(null);
const titleId = `picker-title-${Math.random().toString(36).slice(2, 9)}`;

let previousFocus: HTMLElement | null = null;
let previousOverflow = "";

onMounted(async () => {
  previousFocus = document.activeElement as HTMLElement | null;
  previousOverflow = document.body.style.overflow;
  document.body.style.overflow = "hidden";
  await nextTick();
  const first = dialogEl.value?.querySelector<HTMLElement>(".choice");
  (first ?? dialogEl.value)?.focus();
});

onBeforeUnmount(() => {
  document.body.style.overflow = previousOverflow;
  if (previousFocus?.isConnected) previousFocus.focus();
});

function summary(choice: WorkoutChoice): string {
  const parts: string[] = [];
  if (choice.exerciseCount !== null) {
    parts.push(
      `${choice.exerciseCount} exercise${choice.exerciseCount === 1 ? "" : "s"}`,
    );
  }
  if (choice.totalSets !== null) {
    parts.push(`${choice.totalSets} set${choice.totalSets === 1 ? "" : "s"}`);
  }
  return parts.join(" · ");
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === "Escape") {
    e.preventDefault();
    e.stopPropagation();
    emit("close");
    return;
  }
  if (e.key !== "Tab" || !dialogEl.value) return;

  const items = Array.from(
    dialogEl.value.querySelectorAll<HTMLElement>("button:not([disabled])"),
  ).filter((el) => el.offsetParent !== null);
  if (items.length === 0) {
    e.preventDefault();
    return;
  }
  const firstItem = items[0]!;
  const lastItem = items[items.length - 1]!;
  const active = document.activeElement;
  if (e.shiftKey && (active === firstItem || active === dialogEl.value)) {
    e.preventDefault();
    lastItem.focus();
  } else if (!e.shiftKey && active === lastItem) {
    e.preventDefault();
    firstItem.focus();
  }
}

let backdropPointerDown = false;

function onBackdropPointerDown(e: PointerEvent) {
  backdropPointerDown = e.target === e.currentTarget;
}

function onBackdropClick(e: MouseEvent) {
  if (backdropPointerDown && e.target === e.currentTarget) emit("close");
  backdropPointerDown = false;
}
</script>

<template>
  <Teleport to="body">
    <div
      class="modal-backdrop"
      @pointerdown="onBackdropPointerDown"
      @click="onBackdropClick"
    >
      <div
        ref="dialogEl"
        class="modal"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="titleId"
        tabindex="-1"
        @keydown="onKeydown"
      >
        <header class="modal__header">
          <div class="modal__heading">
            <span class="modal__eyebrow">{{ props.dateLabel }}</span>
            <h2 :id="titleId" class="modal__title">Which workout?</h2>
          </div>
          <button
            type="button"
            class="icon-button"
            aria-label="Close"
            @click="emit('close')"
          >
            <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
              <path
                d="M4 4l8 8M12 4l-8 8"
                stroke="currentColor"
                stroke-width="1.6"
                stroke-linecap="round"
              />
            </svg>
          </button>
        </header>

        <ul class="modal__body choices">
          <li v-for="choice in props.choices" :key="choice.id">
            <button
              type="button"
              class="choice"
              @click="emit('pick', choice.id)"
            >
              <span class="choice__title">
                {{ choice.title || "Untitled workout" }}
              </span>
              <span v-if="summary(choice)" class="choice__meta">
                {{ summary(choice) }}
              </span>
            </button>
          </li>
        </ul>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 900;

  display: grid;
  place-items: center;
  padding: 24px 16px;

  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);

  animation: fade 140ms var(--ease) both;
}

.modal {
  display: flex;
  flex-direction: column;

  width: min(420px, 100%);
  max-height: min(560px, calc(100dvh - 48px));

  background: var(--surface-solid);
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-card);
  box-shadow: var(--shadow-glass);

  color: var(--text);
  font-family: var(--font-body);
  font-size: 14px;

  outline: none;

  animation: pop 180ms var(--ease) both;
}

.modal__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;

  padding: var(--card-pad) var(--card-pad) var(--card-pad-sm);
  border-bottom: 1px solid var(--border);
}

.modal__heading {
  min-width: 0;
}

.modal__eyebrow {
  display: block;
  margin-bottom: 4px;

  color: var(--text-faint);
  font-size: 0.68rem;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.modal__title {
  margin: 0;

  font-family: var(--font-display);
  font-size: 1.15rem;
  font-weight: 600;
  line-height: 1.25;
}

.modal__body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overscroll-behavior: contain;
}

.choices {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0;
  padding: var(--card-pad-sm) var(--card-pad) var(--card-pad);
  list-style: none;
}

.choice {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
  width: 100%;
  min-height: 44px;
  padding: 10px 12px;

  background: transparent;
  border: 1px solid var(--border);
  border-radius: var(--radius-control);
  color: var(--text);
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.choice:hover {
  border-color: var(--border-strong);
  background: var(--surface-hover);
}

.choice__title {
  font-weight: 600;
  overflow-wrap: anywhere;
}

.choice__meta {
  color: var(--text-dim);
  font-size: 0.82rem;
}

.icon-button {
  flex: none;
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;

  background: transparent;
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  color: var(--text-faint);
  cursor: pointer;
}

.icon-button:hover {
  color: var(--text);
  border-color: var(--border-strong);
}

.icon-button:focus-visible,
.choice:focus-visible {
  outline: 2px solid var(--accent-border);
  outline-offset: 2px;
}

@keyframes fade {
  from {
    opacity: 0;
  }
}

@keyframes pop {
  from {
    opacity: 0;
    transform: translateY(8px) scale(0.985);
  }
}

@media (prefers-reduced-motion: reduce) {
  .modal-backdrop,
  .modal {
    animation: none;
  }
}

@media (max-width: 560px) {
  .modal-backdrop {
    padding: 0;
    place-items: end stretch;
  }

  .modal {
    width: 100%;
    max-height: 88dvh;
    border-radius: var(--radius-card) var(--radius-card) 0 0;
    border-left: 0;
    border-right: 0;
    border-bottom: 0;
    padding-bottom: env(safe-area-inset-bottom);
  }

  .icon-button {
    width: 40px;
    height: 40px;
  }
}
</style>
