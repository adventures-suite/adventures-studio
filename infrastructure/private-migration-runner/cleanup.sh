#!/usr/bin/env bash
set -euo pipefail
umask 077
: "${AZURE_SUBSCRIPTION_ID:?}" "${AZURE_RESOURCE_GROUP:?}" "${RUNNER_OPERATION_ID:?}"
[[ "$RUNNER_OPERATION_ID" =~ ^[a-z0-9][a-z0-9-]{7,31}$ ]]; [[ "$AZURE_RESOURCE_GROUP" == rg-adventures-suite-dev ]]
az vm delete --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "vm-migration-runner-${RUNNER_OPERATION_ID}" --yes --force-deletion true || true
az network nic delete --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "nic-migration-runner-${RUNNER_OPERATION_ID}" || true
az disk delete --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "disk-migration-runner-${RUNNER_OPERATION_ID}-os" --yes || true
az network vnet subnet delete --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" --vnet-name vnet-adventures-suite-dev -n "snet-migration-runner-${RUNNER_OPERATION_ID}" || true
az network nsg delete --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "nsg-migration-runner-${RUNNER_OPERATION_ID}" || true
residue="$(az resource list --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" --tag "operationId=${RUNNER_OPERATION_ID}" --query 'length(@)' -o tsv)"; test "$residue" = 0
