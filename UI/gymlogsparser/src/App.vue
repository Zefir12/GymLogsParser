<script setup lang="ts">
import Toast from "@/components/Toast.vue";
import { storeToRefs } from "pinia";
import { RouterLink, RouterView } from "vue-router";
import { useWorkoutStore } from "./stores/workout";

const workoutStore = useWorkoutStore();
import { useAuthStore } from "@/stores/auth";
const auth = useAuthStore();

const { error, success } = storeToRefs(workoutStore);
</script>

<template>
  <Toast v-if="success" variant="success" :message="success" />
  <Toast v-if="error" variant="error" :message="error" />
  <header class="site-header">
    <div class="site-header-inner">
      <RouterLink to="/" class="site-brand">
        <svg
          width="26"
          height="26"
          viewBox="0 0 24 24"
          fill="none"
          stroke="var(--accent)"
          stroke-width="2"
          stroke-linecap="round"
          aria-hidden="true"
        >
          <path d="M6 7v10M18 7v10M3 10v4M21 10v4M6 12h12" />
        </svg>
        GymLogs
      </RouterLink>

      <nav aria-label="Main" class="main-nav">
        <RouterLink to="/" class="nav-link" exact-active-class="active">
          Dashboard
        </RouterLink>
        <RouterLink to="/edit" class="nav-link" active-class="active">
          Workouts
        </RouterLink>
        <RouterLink to="/progress" class="nav-link" active-class="active">
          Progress
        </RouterLink>
        <RouterLink to="/exercises" class="nav-link" active-class="active">
          Exercises
        </RouterLink>
      </nav>

      <div class="account">
        <span v-if="auth.user" class="user">{{ auth.user.name }}</span>
        <button
          v-if="auth.isAuthenticated"
          type="button"
          class="nav-link"
          @click="auth.logout()"
        >
          Log out
        </button>
        <button v-else type="button" class="nav-link" @click="auth.login()">
          Log in
        </button>
      </div>

      <RouterLink to="/log" class="log-button" active-class="active">
        + Log workout
      </RouterLink>
    </div>
  </header>

  <main class="app-shell">
    <RouterView />
  </main>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
  padding: 24px;

  color: var(--text-secondary);
}

.site-header {
  border-bottom: 1px solid var(--card-border);
  background: var(--header-bg);
}

.site-header-inner {
  max-width: 1360px;
  margin: 0 auto;
  padding: 14px 24px;

  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px 28px;
}

.site-brand {
  display: flex;
  align-items: center;
  gap: 10px;

  color: var(--text-primary);
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 700;
  text-decoration: none;
}

.main-nav {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  flex: 1 1 auto;
}

.account {
  display: flex;
  align-items: center;
  gap: 4px;
}

.user {
  padding: 0 8px;
  color: var(--text-tertiary);
  font-size: 0.9rem;
}

.nav-link {
  padding: 8px 12px;

  border: 0;
  border-radius: 8px;
  background: none;
  color: var(--text-secondary);

  font-size: 0.9rem;
  text-decoration: none;

  transition:
    color 0.15s var(--ease),
    background 0.15s var(--ease);
}

.nav-link:hover {
  color: var(--text-primary);
}

.nav-link.active {
  color: var(--text-primary);
  background: rgba(255, 255, 255, 0.06);
  font-weight: 500;
}

.log-button {
  display: inline-flex;
  align-items: center;
  min-height: 44px;
  padding: 0 18px;

  border-radius: 10px;
  background: var(--accent);
  color: #1a0d04;

  font-weight: 600;
  text-decoration: none;

  transition: background 0.15s var(--ease);
}

.log-button:hover,
.log-button.active {
  background: var(--accent-strong);
}

@media (max-width: 700px) {
  .app-shell {
    padding: 10px;
  }

  .site-header-inner {
    padding: 12px 16px;
  }

  .main-nav {
    order: 3;
    flex-basis: 100%;
  }
}
</style>

<style>
@import "@/styles/tokens.css";
</style>
