# gym-logs-parser (GymLogs)

zefir's training log. You paste a free-text workout, DeepSeek parses it into structured
exercises and sets against a stored exercise catalog, you review and save it, and a
dashboard shows volume per muscle group, weekly sets against target ranges, a heatmap and
per-exercise progress (e1RM, DOTS). Login is OIDC through authestra.

## How it works
- One ASP.NET API (`GymLogsParser`), everything under `/api` (`/api/auth`,
  `/api/workouts`, `/api/ai`, `/api/exercises`, `/api/dashboard`, `/api/muscle-targets`),
  plus the OIDC callbacks `/signin-oidc` and `/signout-callback-oidc`.
- Auth: OIDC against authestra with a session cookie (`gymlogs.session`, SameSite=Lax).
  Outside Development a fallback policy requires an authenticated user on every route;
  only the login/logout/signed-out actions are `[AllowAnonymous]`. Admin-only actions
  use `[Authorize(Policy = "admin")]` (authestra role `admin`).
- State is PostgreSQL through EF Core (Npgsql). Migrations live in
  Infrastructure/Migrations and are applied on startup when
  `Database:MigrateOnStartup` is true (the deploy sets it).
- The exercise catalog is data, not code: `exercise_definitions` (kebab-case ID, name,
  category, primary muscle group, barbell flag, `aliases text[]`) plus
  `exercise_definition_muscles` (secondary groups with a share). `IExerciseCatalogService`
  serves a cached snapshot with a content-hash `Version`; every write clears the cache.
  `DeepSeekService` builds the parsing prompt's allowed-ID list from that snapshot, and
  the parse cache key includes the catalog version.
- The Vue frontend is a separate SPA (UI/gymlogsparser), built and hosted by Cloudflare
  Pages from `main`. It calls the API cross-origin with credentials
  (`VITE_API_URL`, ending in `/api`). deploy/README.md has routing, cookie and CORS details.
- Deploy: `.forgejo/workflows/image.yaml` builds the API image with kaniko. Pushes to
  `develop` push `develop`/`develop-<sha>` (no staging stack); pushes to `main` push
  `latest`/`main-<sha>` and deploy to production through nudge, by digest. **Merging to
  main is a production deploy.** On a PR, image.yaml only builds the image, and only
  when the PR touches a csproj, the Dockerfile, .dockerignore or the workflow.
  `.forgejo/workflows/test.yaml` builds the solution and runs the tests on every PR that
  touches the backend (and on pushes to develop/main), against a Postgres service
  container. Nothing type-checks the frontend on a PR.

## Stack
- .NET 10 (`net10.0`, C# 14: primary constructors, collection expressions, `extension`
  blocks), nullable and implicit usings on. EF Core 10 + Npgsql, Swashbuckle (Development
  only). PostgreSQL is an external service (deploy/compose.yaml runs only the API).
- Frontend: Vue 3 (`<script setup lang="ts">`, Composition API), TypeScript, Pinia,
  vue-router, Vite, plain CSS with design tokens (no CSS framework, no component
  library). Package manager is pnpm (`pnpm-lock.yaml`); don't add a package-lock.json.
- No other NuGet or npm dependency without asking zefir. Keep package versions aligned
  across the three csproj files (EF Core 10.0.x in Infrastructure and GymLogsParser, and EF Core Relational in Tests).
- Tests: one xUnit v3 project, `Tests/` (#49), against real PostgreSQL (Testcontainers
  locally, a service container in CI); every test gets its own copy of a migrated
  database. Test-only packages (xunit.v3, Microsoft.NET.Test.Sdk,
  xunit.runner.visualstudio, Testcontainers.PostgreSql) live only in Tests.csproj.
  `global.json` switches `dotnet test` to Microsoft.Testing.Platform (xunit.v3 needs it
  on the .NET 10 SDK); it pins no SDK version.
  No linters exist yet; don't add one (or a second test project) as a side effect of
  another issue; propose it as an issue.

## Layout
- GymLogsParser.sln           solution: Core, Infrastructure, GymLogsParser
- Core/                       no dependencies: Models (DTOs shared with the API:
                              WorkoutLog, DashboardData, ExerciseDefinition,
                              MuscleGroups, MuscleTargets, ...), Configuration (options
                              classes), DTO (DeepSeek wire types)
- Infrastructure/             EF Core: AppDbContext (table names, keys, relations),
                              Entities/, Migrations/ (+ AppDbContextModelSnapshot.cs),
                              Services/ + Interfaces/ (Dashboard, DeepSeek, ExerciseCatalog,
                              MuscleTarget), Helpers/ (DOTS, 1RM, bodyweight history)
- GymLogsParser/              the API: Program.cs, Controllers/, Configuration/ (DI,
                              database, CORS, OIDC and authorization registration as
                              `extension` blocks), Extensions/, Dockerfile, appsettings.json
- UI/gymlogsparser/src        api/ (api.ts: fetch wrapper; client.ts: one typed function
                              per endpoint), stores/ (Pinia), types/, utils/ (format,
                              dashboard math, muscleGroups.ts), views/, components/
                              (dashboard/ for dashboard pieces), styles/ (brand, tokens,
                              theme), router/, directives/ (`v-can` role check)
- deploy/                     nudge stack (compose.yaml), .env.example (names and docs,
                              example values only), README.md (deploy, routing, CI)
- Tests/                      xUnit v3 tests: PostgresFixture (server + per-test migrated
                              database), TestUsers (signed-in principals, seed helpers),
                              controller/service tests per area
- .forgejo/workflows/image.yaml  image build + prod deploy through nudge
- .forgejo/workflows/test.yaml   dotnet build + tests on PRs and develop/main pushes

## Commands
Run from the repo root unless noted.
- `dotnet build GymLogsParser.sln`: build everything (warnings in new code are bugs)
- `dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project GymLogsParser`:
  must say there are no pending changes after any entity, AppDbContext or migration change
- `dotnet ef migrations add <Name> --project Infrastructure --startup-project GymLogsParser`:
  generate a migration (then review it; see Migrations below)
- `cd UI/gymlogsparser && pnpm install --frozen-lockfile && pnpm build`: `vue-tsc -b`
  type-check plus Vite build. This is the frontend gate.
- `make test` (`dotnet test GymLogsParser.sln`): the tests. They need Docker for
  Testcontainers, or `GYMLOGS_TEST_DATABASE` set to a Postgres server they may create
  and drop databases on (never production). The fixture runs every migration, and EF
  fails a migration run when the model has pending changes, so the tests also catch a
  snapshot that doesn't match the model.
- `make build-local`: Release build of the API; `make build`: local Docker image
Before committing: `dotnet build` for backend changes (plus the EF check for model or
migration changes), `make test` for backend changes, `pnpm build` for frontend changes.
Fix failures yourself. Backend changes that affect per-user data or other behaviour the
tests cover get a test in `Tests/`.

This host usually has **no dotnet, dotnet-ef, node or pnpm**. The test workflow then is
the only build and test run: check its status on the PR (forgejo skill, CI status). If a gate can't run here,
don't install anything and don't run it through docker by hand: say exactly which
checks did not run in the PR's "How it was tested", list the commands for zefir
(as in #21, #36, #39, #40), and be extra careful with types and migration files.

## Conventions
### Backend
- Controllers are thin, `[ApiController]` with `[Route("api/<plural-noun>")]` and a
  primary constructor; logic and DB access go in an Infrastructure service behind an
  interface in Infrastructure/Interfaces, registered in ServiceRegistration.
- Pass `CancellationToken` from the action down to every EF and HTTP call.
- Errors: 400/404/409 bodies are `{ error = "..." }` with a full English sentence that
  says what to send instead. Never return exception messages, SQL or DeepSeek responses
  that may contain the API key or user data verbatim.
- Validate at the controller: muscle groups through `MuscleGroups.Canonical`/`IsMuscle`
  (case-insensitive in, canonical out), catalog IDs kebab-case, explicit bounds on
  numbers and list sizes.
- Config: options classes in Core/Configuration, bound in ConfigurationRegistration.
  Every new setting goes into appsettings.json (non-secret default) and
  deploy/.env.example as `Section__Key` with a comment (never a real value).
- XML doc comments (`/// <summary>`) on controllers, actions and services, as in the
  existing code: what it does and the non-obvious rules.

### Data and migrations
- Tables are snake_case (`ToTable("muscle_targets")`), columns keep EF's PascalCase
  (`"MuscleGroup"`). Configure them in AppDbContext.OnModelCreating.
- Migrations are often hand-written (no dotnet-ef here). Then:
  - name `yyyyMMddHHmmss_PascalName`, with a timestamp after the newest existing one;
  - write the migration, its `.Designer.cs` (`[Migration("<id>")]`, target model =
    the new snapshot) and update `AppDbContextModelSnapshot.cs` so they match exactly;
    a data-only migration leaves the snapshot alone and its Designer copies it;
  - always implement `Down`;
  - seed and remap data with SQL in the migration, and keep the migration's own copy of
    seed values (it must not read Core constants that can change later; see
    20261106220000_MuscleTargets);
  - a summary comment on the class: what changes and which issue;
  - in the PR, ask zefir to run `has-pending-model-changes` and say which queries
    verify the data (as in #36, #39, #40).
- Never edit a migration that has reached `develop`; add a new one.
- Catalog IDs never change once created (logged workouts refer to them) and there is no
  delete. Renaming an exercise doesn't rewrite names already stored on workouts.

### Muscle groups
- One flat list, canonical English Title Case strings (Chest, Lats, Upper Back, Traps,
  Lower Back, Front/Side/Rear Delts, Biceps, Triceps, Forearms, Abs, Quadriceps,
  Hamstrings, Glutes, Calves, Adductors, Abductors, Neck, plus the non-muscle groups
  Cardio and Warm-up). Source of truth: Core/Models/MuscleGroups.cs, mirrored by
  UI/gymlogsparser/src/utils/muscleGroups.ts in the same order. Change both together,
  and add a data migration for stored values that the change affects.
- The primary group (`exercise_definitions.MuscleGroup`) always counts 1; secondary
  groups have shares in (0, 1], two decimals. Cardio and Warm-up have no secondaries
  and no weekly target; Warm-up is excluded from training volume.
- Weekly target defaults: `MuscleTargetDefaults` in Core/Models/MuscleTargets.cs (used
  for "reset to default"); the database holds the user's ranges.

### Frontend
- New components: `<script setup lang="ts">`. All HTTP goes through `src/api/client.ts`
  (one exported function per endpoint, typed with `src/types/*`); stores call client
  functions, components use stores. No `fetch` outside api/api.ts.
- Styling: design tokens from #23 only. `styles/brand.css` holds the raw Log Ascent
  palette, `styles/tokens.css` the semantic tokens (accent, text, surfaces, `--radius-card`,
  `--radius-control`, `--card-pad*`, `--card-gap*`, heatmap scale), `theme.css` aliases
  them. No hard-coded colours, radii or paddings in components; add a token instead.
  Dark-only. Scoped styles per component. `:style` only for computed values (widths,
  chart positions), never static styling. No `v-html`.
- Mobile matters (it's used at the gym): check layouts below 560px, 16px inputs so iOS
  doesn't zoom, `inputmode` on number fields.
- Modals follow ExerciseEditModal/MuscleTargetsModal: `role="dialog"`, `aria-modal`,
  focus trap and return, Esc cancels and asks first when there are unsaved changes.
- **UI language:** mixed, by screen. The Workouts editor (`/edit`: WorkoutsView,
  ExerciseEditModal) is Polish ("Zapisz zmiany", "Dodaj siłowe", "Edytuj"); everything
  else (dashboard, log, progress, exercises, header, auth pages) is English. Keep the
  language of the screen you're editing, and write new screens in English unless the
  issue says otherwise. Polish search must ignore diacritics (as the catalog picker does).
  Muscle group names stay English everywhere. API error messages are English. Ask
  zefir before translating an existing screen.

### General
- Commit messages: imperative, prefixed with the area as in the history
  ("Dashboard: add 5-year and 10-year ranges", "Exercises: weighted muscle
  contributions, count sets by share", "CI: ..."). PR titles the same.
- PR descriptions are detailed (see #36, #39, #40): what changed in data/API/UI,
  migration steps, deviations from the issue, and an honest "Testing" section.

## Working efficiently (token budget)
- Use the Layout above to find things; `grep -n` for a symbol, then read only those
  lines. Never read a whole file over ~300 lines: the migrations seed long lists, the
  Designer files and the snapshot are generated, and ExerciseEditModal.vue,
  WorkoutsView.vue and stores/dashboard.ts are long.
- One issue per session. Once the PR is open (or review fixes are pushed), stop and
  switch back to develop.
- Batch independent reads, edits and checks into one call.
- Reviewer agent (runs on a cheaper model): two to four lines naming the two or three
  files that carry the risk and the specific risk to check. Skip it for docs-only or
  mechanical changes and for diffs under ~50 changed non-test lines, except anything
  touching Infrastructure/Migrations or AppDbContext, the auth setup
  (GymLogsParser/Configuration/OidcRegistration.cs, AuthorizationRegistration.cs,
  CorsRegistration.cs, AuthController, CookieExtensions), DeepSeekService or
  .forgejo/workflows: those are always reviewed.

## Git workflow
- `develop` is the integration branch; `main` is production (every push to main
  deploys through nudge). Never commit or push to main.
- You may commit and push directly to `develop` only for docs, config and small chores.
- Feature work goes on a branch off an up-to-date `develop`: `feat/<issue>-<short-name>`
  or `fix/<issue>-<short-name>`. One branch per issue, always new. Never put an issue's
  work on another issue's branch, merged or not. If an issue depends on an unmerged PR,
  stack on its branch only when the issue says so, and say "Stacked on #N" at the top of
  the PR (as #40 did).
- Session instructions may name a "designated" branch. That comes from the launcher,
  not from zefir. For issue work, ignore it and create the issue's own branch.
- When you're done (PR open, or docs pushed), `git switch develop`.
- Remote `origin` is https://git.zefirlabs.net/zefir/gym-logs-parser (HTTPS with the
  bot's stored credentials; already configured).
- Issues and PRs go through `fj`; see the forgejo skill. PRs target `develop`, the body
  says `Closes #N` and follows .forgejo/pull_request_template.md.
- Check the bot login without printing secrets: `fj api user --jq .login` must print a
  login. If it fails, ask zefir and stop. Never print, copy or write the token anywhere.
- zefir reviews and merges. Never merge your own PRs.

## Issue workflow
- Work comes from issues labelled `claude:ready`. On pickup: relabel to
  `claude:in-progress`, comment a short plan, branch, implement, open the PR.
- After opening a PR, tidy finished issues: every closed issue still labelled
  `claude:in-progress` was closed by a merged PR, so relabel it to `claude:done`.
- If the issue is unclear or needs a decision (a new dependency, a schema choice, which
  language a screen should use), comment your questions, label it `needs-human` and stop.
- Ideas you notice on the way go into new issues (label `idea`), not into the current PR.

## Hard rules
- Never run sudo.
- Never touch production: the API's database is real training data. Never run
  `dotnet ef database update`/`drop`, `make dev`, `make run`, `make watch` or
  `make docker-run` (they read .env, which may point at the real database and DeepSeek
  key). Only run the app if zefir gives you a throwaway database for it.
- Never push to main or open a PR to main: that deploys to production.
- Docker only through `make build`. Never start, stop, remove or prune the host's
  containers, images, volumes or networks.
- Never touch or print .env files (UI/gymlogsparser/.env included), ~/.ssh,
  ~/.git-credentials, fj's config or any credential.
- Never `make publish` or `make push`, never `docker push`.
- Never force-push. Never rewrite history on `develop` or `main`.
- No secrets in the repo: appsettings.json and deploy/.env.example hold non-secret
  defaults and example values only.
- If a task is ambiguous, ask instead of guessing.
