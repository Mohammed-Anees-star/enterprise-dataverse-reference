#!/usr/bin/env bash
# Deploy the enterprise-ticketing footprint to Azure.
#  Required env vars:
#    AZURE_SUBSCRIPTION_ID
#    AZURE_RESOURCE_GROUP   (e.g. rg-enterprise-ticketing-prod)
#    AZURE_LOCATION         (e.g. eastus2)
#    ENVIRONMENT            (dev / uat / prod)
set -euo pipefail

: "${AZURE_SUBSCRIPTION_ID:?missing AZURE_SUBSCRIPTION_ID}"
: "${AZURE_RESOURCE_GROUP:?missing AZURE_RESOURCE_GROUP}"
: "${AZURE_LOCATION:?missing AZURE_LOCATION}"
: "${ENVIRONMENT:?missing ENVIRONMENT (dev|uat|prod)}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

az account set --subscription "$AZURE_SUBSCRIPTION_ID"

echo "Ensuring resource group $AZURE_RESOURCE_GROUP exists in $AZURE_LOCATION..."
az group create \
  --name "$AZURE_RESOURCE_GROUP" \
  --location "$AZURE_LOCATION" \
  --output none

echo "Linting Bicep..."
az bicep build --file "$REPO_ROOT/infrastructure/azure/main.bicep" --stdout >/dev/null

echo "Deploying Bicep template..."
az deployment group create \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --template-file "$REPO_ROOT/infrastructure/azure/main.bicep" \
  --parameters environmentName="$ENVIRONMENT" \
  --query "{api:properties.outputs.apiWebAppHostname.value, kv:properties.outputs.keyVaultName.value, sb:properties.outputs.serviceBusNamespace.value}"

echo "Done."
