<script setup lang="ts">
import Toast from "@/components/Toast.vue";
import { storeToRefs } from "pinia";
import { RouterLink, RouterView } from "vue-router";
import { useWorkoutStore } from "./stores/workout";

const workoutStore = useWorkoutStore();
import { useAuthStore } from "@/stores/auth";
const auth = useAuthStore();

const { rawText, workout, loading, barbellWeightsArePerSide, error, success } =
  storeToRefs(workoutStore);
</script>

<template>
  <Toast v-if="success" variant="success" :message="success" />
  <Toast v-if="error" variant="error" :message="error" />
  <main class="app-shell">
    <nav class="top-nav">
      <span v-if="auth.user" class="nav-link">{{ auth.user.name }}</span>
      <button
        v-if="auth.isAuthenticated"
        class="nav-link"
        @click="auth.logout()"
      >
        Log out
      </button>
      <button v-else class="nav-link" @click="auth.login()">Log in</button>

      <RouterLink
        to="/"
        class="nav-link"
        active-class="active"
        exact-active-class="active"
      >
        Log workout
      </RouterLink>
      <RouterLink to="/progress" class="nav-link" active-class="active">
        Progress
      </RouterLink>
      <RouterLink to="/edit" class="nav-link" active-class="active">
        Edit
      </RouterLink>
    </nav>

    <RouterView />
  </main>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
  padding: 24px;

  color: var(--text-secondary);
}

.top-nav {
  max-width: 1500px;
  margin: 0 auto 18px;

  display: flex;
  gap: 4px;

  padding: 4px;
  width: fit-content;

  background: var(--glass-soft);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xl);
}

.nav-link {
  padding: 7px 16px;

  font-size: 0.9rem;
  color: var(--text-tertiary);
  text-decoration: none;

  border-radius: var(--radius-lg);
  transition:
    color 0.15s var(--ease),
    background 0.15s var(--ease);
}

.nav-link:hover {
  color: var(--text-primary);
}

.nav-link.active {
  color: var(--text-primary);
  background: var(--glass-strong);
}

@media (max-width: 700px) {
  .app-shell {
    padding: 10px;
  }
}
</style>

<style>
@import "@/styles/tokens.css";
</style>
