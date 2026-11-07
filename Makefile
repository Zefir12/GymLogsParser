# --- CONFIG ---
REGISTRY=registrydedic.zefirlabs.net
IMAGE=$(REGISTRY)/gymlogsparser
TAG=latest
PROJECT=GymLogsParser/GymLogsParser.csproj
DOCKERFILE=GymLogsParser/Dockerfile
CONTEXT=.

.PHONY: dev run restore build-local clean test check build push publish

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

