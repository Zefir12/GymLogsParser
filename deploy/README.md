# Deploying on nudge

The stack is `deploy/compose.yaml`, deployed by nudge as a stack from git. It runs only
the API; Postgres is an external service, and the Vue frontend (`UI/gymlogsparser`) is
built and hosted by Cloudflare Pages on every push to `main`:

| Part     | Where | Role |
|----------|-------|------|
| `api`    | nudge, `registrydedic.zefirlabs.net/gymlogsparser:${API_TAG}` | ASP.NET API and OIDC login, applies EF migrations on startup. Published on `API_BIND:API_PORT`. |
| frontend | Cloudflare Pages | The SPA. Calls the API cross-origin with the session cookie. |

The examples below use `https://gymlogs.zefirlabs.net` for the Pages site and
`https://gymlogs-api.zefirlabs.net` for the API; use your own hostnames.

## Routing

- The host's reverse proxy (TLS) forwards the API's public hostname to the `api` port
  (`127.0.0.1:8085` by default, `API_BIND`/`API_PORT`) and must send
  `X-Forwarded-Proto: https` and the original `Host`. The API trusts forwarded headers,
  so it builds `https://<api host>/...` URLs (the OIDC redirect URI among them).
- Every controller lives under `/api` (`/api/auth`, `/api/workouts`, `/api/ai`, ...);
  the OIDC handler answers on `/signin-oidc` and `/signout-callback-oidc`.

### Cloudflare Pages build

Set the build environment variable (Production, and Preview if you use it):

```
VITE_API_URL=https://gymlogs-api.zefirlabs.net/api
```

It must end in `/api`, with no trailing slash: the frontend prefixes it to paths without
`/api` (`/workouts`, `/auth/me`, `/auth/login`), and the routes are `api/...`. Vite reads
it at build time, so redeploy Pages after changing it.

### Session cookie: same site

The login cookie (`gymlogs.session`) is `SameSite=Lax`. The frontend sends it with
`fetch(..., { credentials: "include" })` (`src/api/api.ts`) and logs out with a form
POST to `/api/auth/logout`. Browsers send a Lax cookie on those requests only when the
frontend and the API are **same-site**: subdomains of the same registrable domain, e.g.
`gymlogs.zefirlabs.net` and `gymlogs-api.zefirlabs.net`. So:

- Give the Pages project a custom domain under the API's domain. The default
  `*.pages.dev` address is a different site: logged-in requests from it get 401.
- Both must be HTTPS (the cookie is `Secure`).

Cross-origin is still needed: `Frontend__Cors` must list the Pages origin exactly (the
API allows credentials for those origins only).

## 1. Build and push the image

Forgejo Actions builds and pushes the API image (see [CI](#ci) below):

| Push to   | Tags pushed                | nudge `API_TAG` |
|-----------|----------------------------|-----------------|
| `main`    | `latest`, `main-<sha>`     | `latest`        |
| `develop` | `develop`, `develop-<sha>` | `develop`       |

`<sha>` is the commit's first 12 hex characters; set `API_TAG` to such a tag to pin a
build.

By hand instead, from the repo root, logged in to `registrydedic.zefirlabs.net`:

```sh
make publish              # tag latest
make publish TAG=1.2.0    # or a fixed tag; then set API_TAG to 1.2.0
```

## 2. authestra client

On the client used by `Auth__ClientId`:

- Redirect URI: `https://gymlogs-api.zefirlabs.net/signin-oidc` (the API host, not Pages).
- Post-logout redirect: the app sends `https://gymlogs-api.zefirlabs.net/signout-callback-oidc`
  (then `/api/auth/signed-out` sends the browser on to the Pages site). authestra accepts
  a post-logout URI on the same origin as a registered redirect URI, so the redirect URI
  above covers it.
- Scopes: `openid profile email`; the `roles` claim decides who is `admin`.

## 3. nudge setup

1. **Registry** (Registries): address `registrydedic.zefirlabs.net` with a user that can
   pull the image. nudge passes it to compose because the compose file names that host.
2. **Stack from git** (Stacks, new stack, from git):
   - name: `gym-logs-parser`
   - repo URL: `https://git.zefirlabs.net/zefir/gym-logs-parser.git`
   - ref: `main` (or the branch you deploy from)
   - compose path: `deploy/compose.yaml`
   - env path: leave empty (don't use `deploy/.env.example`: it would overwrite your values)
   - auth: `token`, with a Forgejo access token that can read this private repo
     (`read:repository`)
3. **Stack env**: set the variables below. Secrets live only here, never in git.

## 4. Variables

`deploy/.env.example` has them all with examples. Write a literal `$` as `$$`.

| Variable | Required | What it is |
|----------|----------|------------|
| `ConnectionStrings__GymLogs` | yes, secret | Npgsql connection string of the external Postgres (see [Database](#database)). |
| `Auth__Authority` | yes | authestra issuer URL (exactly its ISSUER). |
| `Auth__ClientId` | yes | authestra client ID. |
| `Auth__ClientSecret` | yes, secret | authestra client secret. |
| `Auth__FrontendUrl` | yes | The Pages URL, e.g. `https://gymlogs.zefirlabs.net`; login, logout and errors redirect there. |
| `DeepSeek__ApiKey` | yes, secret | DeepSeek API key. |
| `Frontend__Cors` | yes | The Pages origin, e.g. `https://gymlogs.zefirlabs.net` (comma-separated if several). |
| `Frontend__Url` | no | Not read by the API today; keep it equal to `Auth__FrontendUrl`. |
| `API_TAG` | no | Image tag, default `latest`. |
| `API_BIND`, `API_PORT` | no | Where the API port is published, default `127.0.0.1:8085`. |
| `Database__MigrateOnStartup` | no | Default `true`: the API applies pending migrations on start. |
| `DeepSeek__BaseUrl`, `DeepSeek__Model`, `DeepSeek__MaxTokens`, `DeepSeek__CacheMinutes` | no | Default to the values in the image's appsettings.json. |

The whole stack env is also handed to the `api` container (`env_file`), so any other
`Section__Key` override works without changing the compose file.

### Database

Postgres runs outside this stack. Before the first deploy, create an empty database and a
user that owns it, for example:

```sql
CREATE USER gymlogs WITH PASSWORD '...';
CREATE DATABASE gymlogs OWNER gymlogs;
```

Then set `ConnectionStrings__GymLogs`, e.g.
`Host=db.example.lan;Port=5432;Database=gymlogs;Username=gymlogs;Password=...`
(write a literal `$` in the password as `$$`). The API applies EF Core migrations on
startup, so it creates the schema itself, unless `Database__MigrateOnStartup=false`
(then apply the migrations yourself).

The `api` container must be able to reach the database host:

- Postgres on another machine: use its hostname or IP as `Host`.
- Postgres on the Docker host itself: use the host's LAN address, or add
  `extra_hosts: ["host.docker.internal:host-gateway"]` to `api` and use
  `Host=host.docker.internal`. Postgres must listen on that interface and allow the
  Docker network in `pg_hba.conf`.
- Postgres in another compose stack: publish its port on the host and use the host
  address as above, or attach `api` to that stack's network (external network) and use
  its service name.

`deploy/compose.yaml` does not include these; add the one you need to the `api` service.

## 5. First deploy

1. Push the image (step 1) and set up authestra, the registry and the stack (steps 2-3).
2. Point the reverse proxy for the API hostname (e.g. `gymlogs-api.zefirlabs.net`) at
   `API_BIND:API_PORT`.
3. Create the database and its owner (see [Database](#database)).
4. Pull/deploy the stack in nudge. `api` creates the schema (watch its logs for the
   migration).
5. In Cloudflare Pages, set `VITE_API_URL` (see [Cloudflare Pages build](#cloudflare-pages-build))
   and a custom domain on the same site, then redeploy Pages.
6. Open the Pages site, log in, and parse a workout.

Updates: push a new image and deploy again (nudge deploys with `--pull always`).

## CI

`.forgejo/workflows/image.yaml` builds the API image with kaniko (the runners have no
Docker socket). The frontend is not built here; Cloudflare Pages builds it.

- Push to `main` or `develop`: builds and pushes, tags as in step 1.
- Pull request touching `GymLogsParser/Dockerfile`, the root `.dockerignore`, a
  `.csproj` or the workflow: builds only, never pushes.
- The `image` job exposes `tag` and `digest-gymlogsparser` as outputs, for a nudge
  deploy job to be added later.

One-time setup by zefir (repo Settings):

1. **Enable Actions** for the repo (Settings: turn on the Actions unit).
2. **Secrets** (Settings, Actions, Secrets): `REGISTRY_USER` and `REGISTRY_PASSWORD`, a
   user that can push to `registrydedic.zefirlabs.net/gymlogsparser`. Without them, pushes to `main` and
   `develop` fail with "REGISTRY_USER/REGISTRY_PASSWORD secrets are not set"; pull
   request builds don't need them.
3. In nudge, set the stack's `API_TAG` to `develop` or `latest` (the
   compose default is `latest`) and deploy; nudge pulls with `--pull always`.

## Known limits

- ASP.NET data-protection keys live inside the `api` container, so a redeploy signs
  everyone out (sessions last 4 hours anyway).
- `api` has no healthcheck (no curl/wget in the aspnet image).
