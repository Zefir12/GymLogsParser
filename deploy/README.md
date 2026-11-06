# Deploying on nudge

The stack is `deploy/compose.yaml`, deployed by nudge as a stack from git. It runs two
containers; Postgres is an external service, not part of the stack:

| Service    | Image                                                 | Role |
|------------|-------------------------------------------------------|------|
| `ui`       | `registrydedic.zefirlabs.net/gymlogsparser-ui:${UI_TAG}` | nginx: serves the SPA, proxies the API. The only published port. |
| `api`      | `registrydedic.zefirlabs.net/gymlogsparser:${API_TAG}`   | ASP.NET API, applies EF migrations on startup. |

## Routing

Everything is served from one origin, `https://gymlogs.zefirlabs.net`:

- The host's reverse proxy (TLS) forwards to the `ui` port (`127.0.0.1:8085` by default)
  and must send `X-Forwarded-Proto: https` and the original `Host`.
- nginx in `ui` proxies `/api/*`, `/signin-oidc` and `/signout-callback-oidc` to `api:8080`
  and passes on `X-Forwarded-Proto`, `X-Forwarded-Host` and `X-Forwarded-For`. The API
  already trusts forwarded headers, so it builds `https://gymlogs.zefirlabs.net/...` URLs.
- Every other path is the SPA (including its own `/auth/error` and `/signed-out` routes).
- The UI image is built with `VITE_API_URL=/api`, so the browser never needs another origin
  and the session cookie is first-party.

## 1. Build and push the images

Forgejo Actions builds and pushes both images (see [CI](#ci) below):

| Push to   | Tags pushed (both images)  | nudge `API_TAG` / `UI_TAG` |
|-----------|----------------------------|----------------------------|
| `main`    | `latest`, `main-<sha>`     | `latest`                   |
| `develop` | `develop`, `develop-<sha>` | `develop`                  |

`<sha>` is the commit's first 12 hex characters; set `API_TAG`/`UI_TAG` to such a tag to
pin a build.

By hand instead, from the repo root, logged in to `registrydedic.zefirlabs.net`:

```sh
make publish-all              # API and UI, tag latest
make publish-all TAG=1.2.0    # or a fixed tag; then set API_TAG/UI_TAG to 1.2.0
```

`make publish` (API) and `make publish-ui` (UI) also work on their own.

## 2. authestra client

On the client used by `Auth__ClientId`:

- Redirect URI: `https://gymlogs.zefirlabs.net/signin-oidc`
- Post-logout redirect: the app sends `https://gymlogs.zefirlabs.net/signout-callback-oidc`.
  authestra accepts a post-logout URI on the same origin as a registered redirect URI,
  so the redirect URI above covers it.
- Scopes: `openid profile email`; the `roles` claim decides who is `admin`.

## 3. nudge setup

1. **Registry** (Registries): address `registrydedic.zefirlabs.net` with a user that can
   pull both images. nudge passes it to compose because the compose file names that host.
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
| `Auth__FrontendUrl` | yes | `https://gymlogs.zefirlabs.net` |
| `DeepSeek__ApiKey` | yes, secret | DeepSeek API key. |
| `Frontend__Cors` | recommended | `https://gymlogs.zefirlabs.net`, so the image's dev origins don't apply. |
| `Frontend__Url` | no | Public URL of the frontend. |
| `API_TAG`, `UI_TAG` | no | Image tags, default `latest`. |
| `UI_BIND`, `UI_PORT` | no | Where the UI port is published, default `127.0.0.1:8085`. |
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

1. Push both images (step 1) and set up authestra, the registry and the stack (steps 2-3).
2. Point the reverse proxy for `gymlogs.zefirlabs.net` at `UI_BIND:UI_PORT`.
3. Create the database and its owner (see [Database](#database)).
4. Pull/deploy the stack in nudge. `api` creates the schema (watch its logs for the
   migration), then `ui` starts.
5. Open https://gymlogs.zefirlabs.net, log in, and parse a workout.

Updates: push new images and deploy again (nudge deploys with `--pull always`).

## CI

`.forgejo/workflows/image.yaml` builds both images with kaniko (the runners have no
Docker socket):

- Push to `main` or `develop`: builds and pushes, tags as in step 1.
- Pull request touching a Dockerfile, a `.dockerignore`, a `.csproj`, the UI's
  `package.json`, `pnpm-lock.yaml` or `nginx.conf`, or the workflow: builds only, never
  pushes.
- The `image` job exposes `tag` and `digest-gymlogsparser` / `digest-gymlogsparser-ui`
  as outputs, for a nudge deploy job to be added later.

One-time setup by zefir (repo Settings):

1. **Enable Actions** for the repo (Settings: turn on the Actions unit).
2. **Secrets** (Settings, Actions, Secrets): `REGISTRY_USER` and `REGISTRY_PASSWORD`, a
   user that can push to `registrydedic.zefirlabs.net/gymlogsparser` and
   `registrydedic.zefirlabs.net/gymlogsparser-ui`. Without them, pushes to `main` and
   `develop` fail with "REGISTRY_USER/REGISTRY_PASSWORD secrets are not set"; pull
   request builds don't need them.
3. In nudge, set the stack's `API_TAG` and `UI_TAG` to `develop` or `latest` (the
   compose default is `latest`) and deploy; nudge pulls with `--pull always`.

## Known limits

- ASP.NET data-protection keys live inside the `api` container, so a redeploy signs
  everyone out (sessions last 4 hours anyway).
- `api` has no healthcheck (no curl/wget in the aspnet image).
