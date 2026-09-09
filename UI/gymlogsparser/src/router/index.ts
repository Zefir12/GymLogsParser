import { createRouter, createWebHistory } from "vue-router";

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
];

export const router = createRouter({
  history: createWebHistory(),
  routes,
});
