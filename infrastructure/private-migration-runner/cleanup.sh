#!/usr/bin/env bash
set -euo pipefail
umask 077
: "${AZURE_SUBSCRIPTION_ID:?}" "${AZURE_RESOURCE_GROUP:?}" "${RUNNER_OPERATION_ID:?}"
[[ "$RUNNER_OPERATION_ID" =~ ^[a-z0-9][a-z0-9-]{7,31}$ ]]; [[ "$AZURE_RESOURCE_GROUP" == rg-adventures-suite-dev ]]
vm_name="vm-migration-runner-${RUNNER_OPERATION_ID}"; nic_name="nic-migration-runner-${RUNNER_OPERATION_ID}"; disk_name="disk-migration-runner-${RUNNER_OPERATION_ID}-os"; subnet_name="snet-migration-runner-${RUNNER_OPERATION_ID}"; nsg_name="nsg-migration-runner-${RUNNER_OPERATION_ID}"
if az vm show --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "$vm_name" --query id -o tsv >/dev/null 2>&1; then az vm delete --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "$vm_name" --yes --force-deletion true; fi
if az network nic show --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "$nic_name" --query id -o tsv >/dev/null 2>&1; then az network nic delete --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "$nic_name"; fi
if az disk show --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "$disk_name" --query id -o tsv >/dev/null 2>&1; then az disk delete --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "$disk_name" --yes; fi
if az network vnet subnet show --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" --vnet-name vnet-adventures-suite-dev -n "$subnet_name" --query id -o tsv >/dev/null 2>&1; then az network vnet subnet delete --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" --vnet-name vnet-adventures-suite-dev -n "$subnet_name"; fi
if az network nsg show --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "$nsg_name" --query id -o tsv >/dev/null 2>&1; then az network nsg delete --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" -n "$nsg_name"; fi
residue="$(az resource list --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" --tag "operationId=${RUNNER_OPERATION_ID}" --query 'length(@)' -o tsv)"; test "$residue" = 0
subnet_residue="$(az network vnet subnet list --subscription "$AZURE_SUBSCRIPTION_ID" -g "$AZURE_RESOURCE_GROUP" --vnet-name vnet-adventures-suite-dev --query "[?name=='${subnet_name}'] | length(@)" -o tsv)"; test "$subnet_residue" = 0
