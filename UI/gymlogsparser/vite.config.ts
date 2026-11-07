import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import { mockApi } from "./mock/mockApi.ts";

export default defineConfig(({ command, mode }) => {
  // `vite --mode mock` (make ui-mock): the dev server answers /api itself from
  // in-memory fixtures (mock/), so no backend, database or login is needed.
  // Only for the dev server: `vite build` never includes the mock.
  const mock = command === "serve" && mode === "mock";
  if (mock) {
    // Variables already in the environment win over .env files, so the app calls
    // the mock on the same origin whatever VITE_API_URL a local .env sets.
    process.env.VITE_API_URL = "/api";
  }

  // Comma-separated hostnames the dev server may be reached by (IPs and localhost
  // are always allowed). make ui-mock passes HOST.
  const allowedHosts = process.env.VITE_ALLOWED_HOSTS?.split(",")
    .map((h) => h.trim())
    .filter(Boolean);

  return {
    plugins: [vue(), ...(mock ? [mockApi()] : [])],

    resolve: {
      alias: {
        "@": fileURLToPath(new URL("./src", import.meta.url)),
      },
    },

    server: {
      port: 5173,
      allowedHosts: allowedHosts?.length ? allowedHosts : undefined,
      // Bind mounts (Docker Desktop, WSL) may not deliver file events; make ui-mock
      // sets VITE_USE_POLLING=1.
      watch: process.env.VITE_USE_POLLING === "1" ? { usePolling: true, interval: 300 } : undefined,
    },
  };
});
