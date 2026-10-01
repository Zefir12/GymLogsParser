import { useAuthStore } from "@/stores/auth";
import { createRouter, createWebHistory } from "vue-router";

declare module "vue-router" {
  interface RouteMeta {
    public?: boolean;
    roles?: string[];
  }
}

const routes = [
  {
    path: "/",
    name: "home",
    component: () => import("@/views/HomeView.vue"),
  },
  {
    path: "/progress",
    name: "progress",
    component: () => import("@/views/ExerciseProgressView.vue"),
  },
  {
    path: "/edit",
    name: "edit",
    component: () => import("@/views/WorkoutsView.vue"),
  },
  {
    path: "/admin",
    component: () => import("@/views/AdminView.vue"),
    meta: { roles: ["admin"] },
  },
  {
    path: "/signed-out",
    name: "signed-out",
    component: () => import("@/views/SignedOutView.vue"),
    meta: { public: true },
  },
  {
    path: "/auth/error",
    name: "auth-error",
    component: () => import("@/views/AuthErrorView.vue"),
    meta: { public: true },
  },
  {
    path: "/forbidden",
    name: "forbidden",
    component: () => import("@/views/ForbiddenView.vue"),
    meta: { public: true },
  },
  {
    path: "/:pathMatch(.*)*",
    component: () => import("@/views/NotFoundView.vue"),
    meta: { public: true },
  },
];

export const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach(async (to) => {
  const auth = useAuthStore();
  if (auth.status === "idle") await auth.init();

  if (to.name === "signed-out" && auth.isAuthenticated) return { name: "home" };

  if (to.meta.public) return true;

  if (auth.status === "error")
    return { name: "auth-error", query: { code: "unreachable" } };
  if (!auth.isAuthenticated) {
    auth.login(to.fullPath);
    return false;
  }
  if (to.meta.roles && !auth.hasAnyRole(to.meta.roles))
    return { name: "forbidden" };
  return true;
});
