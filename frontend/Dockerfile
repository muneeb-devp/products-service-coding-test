# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
FROM node:24-alpine AS build
WORKDIR /app

# Manifests first so the dependency layer is cached independently of source
# changes.
COPY package.json package-lock.json ./

# `npm ci` rather than `npm install`: it installs exactly what the lockfile
# pins and fails if the two disagree, which is what makes the image
# reproducible.
RUN npm ci

COPY . .

# Vite inlines environment variables at BUILD time, so the API URL has to be
# known here rather than at container start. The consequence is one image per
# target environment.
#
# The alternative, emitting a config.js at container start and reading it at
# run time, buys one image for all environments, at the cost of an extra
# request before the app can talk to anything. For a demo the build arg is the
# clearer trade; noted in the README as something to revisit.
ARG VITE_API_BASE_URL=http://localhost:8080
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN npm run build

# ---------------------------------------------------------------------------
# Runtime stage, static files only
# ---------------------------------------------------------------------------
FROM nginx:1.29-alpine AS final

# The built bundle is plain static assets. Shipping Node here would mean
# carrying a runtime, a package manager and their CVE surface to serve files
# nginx serves better.
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
