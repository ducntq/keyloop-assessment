# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage — restores and publishes the API (Domain + Infrastructure come
# along transitively, so no test project is ever pulled into the image).
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore layer: copy only the project graph so NuGet restore is cached until a
# .csproj (or global.json) actually changes.
COPY global.json ./
COPY src/KeyloopScheduler.Domain/KeyloopScheduler.Domain.csproj src/KeyloopScheduler.Domain/
COPY src/KeyloopScheduler.Infrastructure/KeyloopScheduler.Infrastructure.csproj src/KeyloopScheduler.Infrastructure/
COPY src/KeyloopScheduler.Api/KeyloopScheduler.Api.csproj src/KeyloopScheduler.Api/
RUN dotnet restore src/KeyloopScheduler.Api/KeyloopScheduler.Api.csproj

COPY src/ src/
RUN dotnet publish src/KeyloopScheduler.Api/KeyloopScheduler.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is used only by the container HEALTHCHECK below.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Development is the default so a bare `docker run`/compose up applies migrations
# and seeds the demo catalogue (AGENTS.md §8). Override for production.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish ./

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=25s --retries=5 \
    CMD curl --fail --silent --show-error http://localhost:8080/health || exit 1

# Run as the non-root user bundled in the .NET 8 images (UID 1654).
USER $APP_UID

ENTRYPOINT ["dotnet", "KeyloopScheduler.Api.dll"]
