# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Project files first, restore, then the rest of the source.
#
# This ordering is the whole reason the build is fast on the second run: the
# restore layer is keyed on the .csproj files alone, so editing a .cs file
# reuses the cached NuGet restore instead of re-downloading every package.
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Products.Domain/Products.Domain.csproj                 src/Products.Domain/
COPY src/Products.Application/Products.Application.csproj       src/Products.Application/
COPY src/Products.Infrastructure/Products.Infrastructure.csproj src/Products.Infrastructure/
COPY src/Products.Api/Products.Api.csproj                       src/Products.Api/

RUN dotnet restore src/Products.Api/Products.Api.csproj

COPY src/ src/

RUN dotnet publish src/Products.Api/Products.Api.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# curl is here solely for the container healthcheck. The ASP.NET runtime image
# ships neither curl nor wget, and a HEALTHCHECK that cannot run is worse than
# none: the container reports healthy because nothing ever contradicts it.
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# The SQLite file and Serilog output live on a volume, not in the image layer.
# Owned by the non-root user, which cannot create directories under /app itself.
RUN mkdir -p /app/data /app/logs && chown -R app:app /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Disables the diagnostic pipe that is of no use in a container.
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# Runs as the unprivileged `app` user the base image provides. A container
# process running as root shares the host kernel, so a container escape
# becomes a root escape.
USER app

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Products.Api.dll"]
