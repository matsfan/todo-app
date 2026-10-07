#!/usr/bin/env bash
#
# One-time provisioning of the free-tier Azure resources for the todo app.
#
# Prerequisites: Azure CLI (`brew install azure-cli`) and `az login`.
#
# Usage:
#   ./infra/provision.sh <app-name> [resource-group] [location]
#
# Example:
#   ./infra/provision.sh mytodo rg-todo-app eastus
#
# Re-running is safe: the deployment is incremental, so it converges on the template.

set -euo pipefail

APP_NAME="${1:-}"
RESOURCE_GROUP="${2:-rg-todo-app}"
LOCATION="${3:-eastus}"

if [[ -z "$APP_NAME" ]]; then
  echo "Usage: $0 <app-name> [resource-group] [location]" >&2
  exit 1
fi

if ! [[ "$APP_NAME" =~ ^[a-z0-9]{3,20}$ ]]; then
  echo "Error: app-name must be 3-20 characters, lowercase letters and digits only." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==> Subscription"
az account show --query "{name:name, id:id}" -o tsv

# Only one free-tier Cosmos account is allowed per subscription. Detect an existing one
# so the deployment does not fail with a confusing quota error.
echo "==> Checking for an existing free-tier Cosmos DB account"
EXISTING_FREE_TIER="$(
  az cosmosdb list --query "[?enableFreeTier].name" -o tsv 2>/dev/null || true
)"

USE_FREE_TIER=true
if [[ -n "$EXISTING_FREE_TIER" ]]; then
  echo "    Found free-tier account(s) already in use: $EXISTING_FREE_TIER"
  echo "    Deploying WITHOUT the free tier (this account will incur charges)."
  echo "    Press Ctrl+C within 10 seconds to abort."
  sleep 10
  USE_FREE_TIER=false
else
  echo "    None found - requesting the free tier."
fi

echo "==> Creating resource group $RESOURCE_GROUP in $LOCATION"
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --output none

echo "==> Validating the template"
az deployment group validate \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$SCRIPT_DIR/main.bicep" \
  --parameters appName="$APP_NAME" location="$LOCATION" useCosmosFreeTier="$USE_FREE_TIER" \
  --output none

echo "==> Deploying (this takes a few minutes; Cosmos DB is the slow part)"
OUTPUTS="$(
  az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --name "todo-app-$(date +%Y%m%d%H%M%S)" \
    --template-file "$SCRIPT_DIR/main.bicep" \
    --parameters appName="$APP_NAME" location="$LOCATION" useCosmosFreeTier="$USE_FREE_TIER" \
    --query properties.outputs \
    --output json
)"

WEB_APP_NAME="$(echo "$OUTPUTS" | python3 -c 'import json,sys; print(json.load(sys.stdin)["webAppName"]["value"])')"
WEB_APP_URL="$(echo "$OUTPUTS" | python3 -c 'import json,sys; print(json.load(sys.stdin)["webAppUrl"]["value"])')"
COSMOS_NAME="$(echo "$OUTPUTS" | python3 -c 'import json,sys; print(json.load(sys.stdin)["cosmosAccountName"]["value"])')"

cat <<SUMMARY

==================================================================
Provisioning complete.

  Resource group     $RESOURCE_GROUP
  Web app name       $WEB_APP_NAME
  App URL            $WEB_APP_URL
  Cosmos account     $COSMOS_NAME

Next steps
  1. In Azure DevOps: Project Settings > Service connections >
     New > Azure Resource Manager, scoped to $RESOURCE_GROUP.
  2. On the pipeline, add these variables:
       azureServiceConnection = <the service connection name>
       webAppName             = $WEB_APP_NAME
  3. Push to main. The pipeline builds, tests, deploys, and smoke tests.

To tear everything down:
  az group delete --name $RESOURCE_GROUP --yes --no-wait
==================================================================
SUMMARY
