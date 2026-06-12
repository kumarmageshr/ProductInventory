# ── Multi-stage Dockerfile for all .NET services ──────────────────────────
# Usage: docker build --build-arg SERVICE_NAME=ProductService.API -t product-service .
# Twelve-Factor: single codebase, port via environment, stateless process

ARG SERVICE_NAME=ProductService.API
ARG SERVICE_PATH=src/Services/ProductService/ProductService.API

# ── Stage 1: Build ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

ARG SERVICE_NAME
ARG SERVICE_PATH

# Copy solution + project files first for layer caching
COPY ["src/Shared/Shared.Domain/Shared.Domain.csproj", "src/Shared/Shared.Domain/"]
COPY ["src/Shared/Shared.Contracts/Shared.Contracts.csproj", "src/Shared/Shared.Contracts/"]
COPY ["src/Shared/Shared.Infrastructure/Shared.Infrastructure.csproj", "src/Shared/Shared.Infrastructure/"]
COPY ["${SERVICE_PATH}/${SERVICE_NAME}.csproj", "${SERVICE_PATH}/"]

RUN dotnet restore "${SERVICE_PATH}/${SERVICE_NAME}.csproj" --runtime linux-musl-x64

# Copy all source
COPY . .

# Publish optimized release build
RUN dotnet publish "${SERVICE_PATH}/${SERVICE_NAME}.csproj" \
    --configuration Release \
    --runtime linux-musl-x64 \
    --self-contained false \
    --output /app/publish \
    -p:PublishReadyToRun=true \
    -p:PublishTrimmed=false

# ── Stage 2: Runtime — minimal image ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Security: run as non-root user
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

# Copy published output
COPY --from=build --chown=appuser:appgroup /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

EXPOSE 8080
EXPOSE 8081

ARG SERVICE_NAME
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "${SERVICE_NAME}.dll"]
