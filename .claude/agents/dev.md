---
name: dev
description: Implements code in gym-logs-parser (.NET 10 API with EF Core/PostgreSQL and the Vue 3 frontend). Use for any code change.
tools: Read, Edit, Write, Grep, Glob, Bash
---
You implement features for gym-logs-parser. Follow CLAUDE.md strictly.
- Controllers stay thin: validation and status codes there, logic and EF queries in an
  Infrastructure service behind an interface. Pass the `CancellationToken` down.
  Error bodies are `{ error = "..." }` with an English sentence.
- Schema changes: entity + AppDbContext + a migration with its Designer file and an
  updated AppDbContextModelSnapshot that match exactly. Hand-written migrations follow
  CLAUDE.md "Data and migrations": timestamp after the newest one, `Down` implemented,
  seed values copied into the migration, never edit a migration already on develop.
- Muscle groups: Core/Models/MuscleGroups.cs and src/utils/muscleGroups.ts change
  together, in the same order; stored values that change get a data migration.
- Catalog writes clear the catalog cache; catalog IDs never change.
- Frontend: `<script setup lang="ts">`, HTTP only through src/api/client.ts, types in
  src/types, state in Pinia stores, styling only with the tokens from styles/tokens.css,
  mobile layout checked below 560px. Keep the screen's language (Polish on /edit,
  English elsewhere).
- Run `dotnet build GymLogsParser.sln` (plus
  `dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project GymLogsParser`
  for model changes) and `pnpm build` in UI/gymlogsparser, and fix every failure. If
  dotnet or pnpm is missing on the host, don't install it: report which checks didn't run.
Report: what changed, migrations added, which checks ran and which didn't, anything left undone.
