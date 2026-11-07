# syntax=docker/dockerfile:1
# `make check`: the pre-commit gate (CLAUDE.md "Commands") in containers, for a host with
# no dotnet or Node. Two independent stages that BuildKit runs in parallel:
#   backend   dotnet build of the solution + `dotnet ef migrations has-pending-model-changes`
#   frontend  pnpm install --frozen-lockfile + pnpm build (vue-tsc -b, Vite build)
# The repo goes in as the build context, filtered by the root .dockerignore (no .env files,
# bin/obj or node_modules); nothing is bind-mounted and nothing connects to a database.
# The final stage is an empty marker image: the build succeeding is the result.
# It does not run the tests (they need a Postgres server; see `make test`).
# CI runs the same steps in .forgejo/workflows/test.yaml; keep the versions in step.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
# Keep in step with the EF Core packages in the csproj files.
ARG DOTNET_EF_VERSION=10.0.11
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
RUN dotnet tool install --tool-path /tools dotnet-ef --version "$DOTNET_EF_VERSION"
WORKDIR /src
COPY GymLogsParser.sln global.json ./
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY GymLogsParser/GymLogsParser.csproj GymLogsParser/
COPY Tests/Tests.csproj Tests/
RUN dotnet restore GymLogsParser.sln
COPY Core/ Core/
COPY Infrastructure/ Infrastructure/
COPY GymLogsParser/ GymLogsParser/
COPY Tests/ Tests/
RUN dotnet build GymLogsParser.sln --no-restore
RUN /tools/dotnet-ef migrations has-pending-model-changes \
      --project Infrastructure --startup-project GymLogsParser --no-build
RUN touch /backend.ok

FROM node:24 AS frontend
ARG PNPM_VERSION=10
RUN npm install -g "pnpm@$PNPM_VERSION"
WORKDIR /ui
COPY UI/gymlogsparser/package.json UI/gymlogsparser/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY UI/gymlogsparser/ ./
RUN pnpm build
RUN touch /frontend.ok

FROM scratch AS check
COPY --from=backend /backend.ok /
COPY --from=frontend /frontend.ok /
