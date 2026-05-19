# ============================================================
# Multi-stage Dockerfile for Enterprise Ticketing API
# Based on Microsoft's .NET 10 official images
#
# Stages:
#   base    — runtime base image (minimal, no SDK)
#   build   — SDK image for restore + build
#   publish — SDK image for publish
#   final   — runtime image with published output only
#
# Security hardening:
#   - Final image uses non-root user (app)
#   - No SDK in final image (reduces attack surface)
#   - Minimal layer count
# ============================================================

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443
# Run as non-root user for container security
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first for layer caching — restore only if .csproj changes
COPY ["src/4-API/EnterpriseTicketing.API/EnterpriseTicketing.API.csproj", "4-API/EnterpriseTicketing.API/"]
COPY ["src/3-Infrastructure/EnterpriseTicketing.Infrastructure/EnterpriseTicketing.Infrastructure.csproj", "3-Infrastructure/EnterpriseTicketing.Infrastructure/"]
COPY ["src/2-Application/EnterpriseTicketing.Application/EnterpriseTicketing.Application.csproj", "2-Application/EnterpriseTicketing.Application/"]
COPY ["src/1-Domain/EnterpriseTicketing.Domain/EnterpriseTicketing.Domain.csproj", "1-Domain/EnterpriseTicketing.Domain/"]

RUN dotnet restore "4-API/EnterpriseTicketing.API/EnterpriseTicketing.API.csproj"

# Copy all source after restore (layer cache optimization)
COPY src/ .
WORKDIR "/src/4-API/EnterpriseTicketing.API"
RUN dotnet build "EnterpriseTicketing.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EnterpriseTicketing.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Health check — Docker will mark container unhealthy if liveness fails
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "EnterpriseTicketing.API.dll"]
