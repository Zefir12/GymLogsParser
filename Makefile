# --- CONFIG ---
REGISTRY=registrydedic.zefirlabs.net
IMAGE=$(REGISTRY)/gymlogsparser
TAG=latest
PROJECT=GymLogsParser/GymLogsParser.csproj
DOCKERFILE=GymLogsParser/Dockerfile
CONTEXT=.

.PHONY: dev run restore build-local clean test check ui-mock build push publish

# Load .env if it exists.
ifneq (,$(wildcard .env))
include .env
export
endif

# -------------------------------------------------------------------
# Local development
# -------------------------------------------------------------------

dev: restore run

restore:
	dotnet restore $(PROJECT)

run: restore
	dotnet run --project $(PROJECT)

watch:
	dotnet watch --project $(PROJECT) run

build-local: restore
	dotnet build $(PROJECT) -c Release --no-restore

clean:
	dotnet clean $(PROJECT)

# Tests (Tests/). They start a throwaway PostgreSQL with Testcontainers, so Docker must be
# running, unless GYMLOGS_TEST_DATABASE is the connection string of a server they may create
# and drop databases on (never the production one).
test:
	dotnet test GymLogsParser.sln

# The pre-commit gate in containers, for a host with no dotnet or Node (check.Dockerfile):
# dotnet build, `dotnet ef migrations has-pending-model-changes` and the frontend's
# `pnpm install --frozen-lockfile && pnpm build`. Needs only Docker with BuildKit. The repo goes
# in as the build context (minus .env files, see .dockerignore); it never reaches a database.
# No tests: those are `make test` (and CI).
check:
	docker build -f check.Dockerfile --target check --progress=plain $(CONTEXT)

# Frontend only, against the in-memory mock API (UI/gymlogsparser/mock, #66): no .NET,
# no database, no login. Runs the Vite dev server (`--mode mock`, HMR on) in node:24 as
# the current user, so node_modules stays yours, published on HOST:PORT. Passes none of
# .env into the container. Stop it with Ctrl-C or `docker stop gymlogs-ui-mock`.
#   make ui-mock HOST=192.168.1.10 PORT=5173
HOST ?= 127.0.0.1
PORT ?= 5173
PNPM_VERSION ?= 10
ui-mock:
	docker run --rm --init --name gymlogs-ui-mock \
		--user "$$(id -u):$$(id -g)" \
		-v "$(CURDIR)/UI/gymlogsparser:/ui" -w /ui \
		-p "$(HOST):$(PORT):$(PORT)" \
		-e HOME=/tmp -e COREPACK_HOME=/tmp/corepack -e COREPACK_ENABLE_DOWNLOAD_PROMPT=0 \
		-e VITE_API_URL=/api -e VITE_ALLOWED_HOSTS="$(HOST)" -e VITE_USE_POLLING=1 \
		node:24 sh -c 'corepack pnpm@$(PNPM_VERSION) install --frozen-lockfile --store-dir /tmp/pnpm-store --config.confirm-modules-purge=false \
			&& exec corepack pnpm@$(PNPM_VERSION) exec vite --mode mock --host 0.0.0.0 --port $(PORT) --strictPort'

# -------------------------------------------------------------------
# Docker
# -------------------------------------------------------------------

# Build Docker image locally
build:
	docker build -f $(DOCKERFILE) -t $(IMAGE):$(TAG) $(CONTEXT)

# Run the container locally (maps the exposed ports)
docker-run:
	docker run --rm -p 8080:8080 -p 8081:8081 --env-file .env $(IMAGE):$(TAG)

# Push image to private registry
push:
	docker push $(IMAGE):$(TAG)

# Build and push in one step (recommended)
publish:
	docker buildx build -f $(DOCKERFILE) -t $(IMAGE):$(TAG) --push $(CONTEXT)

