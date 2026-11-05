# Single-origin UI image: nginx serves the SPA and proxies the API paths to the
# `api` service, so the browser only ever talks to one origin.
# Build from this folder: docker buildx build -t <image> UI/gymlogsparser

FROM node:24-alpine AS build
WORKDIR /app
# Same origin as the SPA; nginx forwards /api/* to the API container.
ARG VITE_API_URL=/api
ENV VITE_API_URL=$VITE_API_URL
# The lockfile is v9: pnpm 9 or 10 reads it.
RUN npm install -g pnpm@10
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .
RUN pnpm run build

FROM nginx:1.29-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1
