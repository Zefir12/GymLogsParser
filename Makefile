# --- CONFIG ---
REGISTRY=registrydedic.zefirlabs.net
IMAGE=$(REGISTRY)/gymlogsparser
TAG=latest
PROJECT=GymLogsParser/GymLogsParser.csproj
DOCKERFILE=GymLogsParser/Dockerfile
CONTEXT=.

.PHONY: dev run restore build-local clean build push publish

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