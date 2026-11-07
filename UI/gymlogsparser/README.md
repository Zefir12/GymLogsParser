# GymLogs frontend

## UI work without a backend (mock mode)

`make ui-mock` (from the repo root) runs the Vite dev server with a mock API: no .NET,
PostgreSQL or login. It runs in a `node:24` container as your user, installs
dependencies with pnpm (corepack) and serves on `HOST:PORT` with HMR:

    make ui-mock                               # http://127.0.0.1:5173
    make ui-mock HOST=192.168.1.10 PORT=5173   # reachable from your phone on the LAN

With Node and pnpm on the host, `pnpm dev --mode mock` does the same.

The mock (`mock/`, a dev-only Vite plugin) answers every `/api` call from fixtures
generated relative to today: a signed-in admin, ~5 months of workouts, the exercise
catalog, bodyweight entries, the default weekly targets, the dashboard and a canned
AI parse. Writes (save, edit, delete) change the in-memory data and are lost when the
server restarts. `vite build` never includes it.

## Vue 3 + TypeScript + Vite

This template should help get you started developing with Vue 3 and TypeScript in Vite. The template uses Vue 3 `<script setup>` SFCs, check out the [script setup docs](https://v3.vuejs.org/api/sfc-script-setup.html#sfc-script-setup) to learn more.

Learn more about the recommended Project Setup and IDE Support in the [Vue Docs TypeScript Guide](https://vuejs.org/guide/typescript/overview.html#project-setup).
