import { api, ApiError } from "@/api/api";
import { defineStore } from "pinia";
import { computed, ref } from "vue";

export type User = { id: string; email: string; name: string; roles: string[] };
type Status = "idle" | "loading" | "authenticated" | "anonymous" | "error";

const LOOP_KEY = "auth:lastRedirect";
const channel =
  "BroadcastChannel" in window ? new BroadcastChannel("auth") : null;

export const useAuthStore = defineStore("auth", () => {
  const user = ref<User | null>(null);
  const status = ref<Status>("idle");
  const isAuthenticated = computed(() => status.value === "authenticated");
  let inflight: Promise<void> | null = null;

  function init(): Promise<void> {
    if (inflight) return inflight;
    status.value = "loading";
    inflight = (async () => {
      try {
        user.value = await api.get<User>("/auth/me");
        status.value = "authenticated";
      } catch (e) {
        user.value = null;
        status.value =
          e instanceof ApiError && e.status === 401 ? "anonymous" : "error";
      }
    })().finally(() => (inflight = null));
    return inflight;
  }

  const hasRole = (r: string) => !!user.value?.roles.includes(r);
  const hasAnyRole = (rs: string[]) => rs.some(hasRole);

  /** Full-page redirect to the IdP, with a redirect-loop breaker. */
  function login(
    returnUrl = location.pathname + location.search + location.hash,
  ) {
    const last = Number(sessionStorage.getItem(LOOP_KEY) ?? 0);
    if (Date.now() - last < 5000) {
      location.assign("/auth/error?code=loop");
      return;
    }
    sessionStorage.setItem(LOOP_KEY, String(Date.now()));
    location.assign(
      `${import.meta.env.VITE_API_URL}/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`,
    );
  }

  function expire() {
    user.value = null;
    status.value = "anonymous";
  }

  function logout() {
    channel?.postMessage("logout"); // other tabs expire themselves
    const form = document.createElement("form");
    form.method = "POST";
    form.action = `${import.meta.env.VITE_API_URL}/auth/logout`;
    document.body.appendChild(form);
    form.submit();
  }
  channel?.addEventListener("message", (e) => {
    if (e.data === "logout") expire();
  });
  // back/forward cache could show a stale authenticated page
  window.addEventListener("pageshow", (e) => {
    if (e.persisted) init();
  });

  return {
    user,
    status,
    isAuthenticated,
    init,
    login,
    logout,
    expire,
    hasRole,
    hasAnyRole,
  };
});
