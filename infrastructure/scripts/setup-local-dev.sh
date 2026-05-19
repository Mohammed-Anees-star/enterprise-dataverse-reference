#!/usr/bin/env bash
# Bring up the local developer loop.
#  1. Restore .NET tooling and project deps
#  2. Generate a self-signed dev cert for HTTPS
#  3. Trust the dev cert
#  4. Build the API + Functions Docker images
#  5. Start docker-compose
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

echo "[1/5] Restoring .NET tools and projects..."
dotnet tool restore 2>/dev/null || true
dotnet restore src/EnterpriseTicketing.sln

echo "[2/5] Generating dev HTTPS cert..."
dotnet dev-certs https --clean
dotnet dev-certs https --trust

echo "[3/5] Building API image..."
docker build -f src/4-API/EnterpriseTicketing.API/Dockerfile -t enterprise-ticketing-api:dev .

echo "[4/5] Building Functions image..."
docker build -f src/5-Functions/EnterpriseTicketing.Functions/Dockerfile -t enterprise-ticketing-functions:dev .

echo "[5/5] Starting docker-compose..."
if [[ ! -f .env ]]; then
  echo "WARN: .env file not found. Copy .env.example to .env and fill in values first."
  exit 1
fi

docker compose up -d
docker compose ps
echo "Local dev environment ready. API at http://localhost:5000 - Swagger at /swagger"
