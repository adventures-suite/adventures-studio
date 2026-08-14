#!/usr/bin/env bash
set -euo pipefail
umask 077

fail(){ printf '%s\n' "$1" >&2; exit 1; }
[[ $# -eq 2 ]] || fail closed-cleanup-arguments
binding_file="$1"; expected_sha="$2"
[[ "$expected_sha" =~ ^[0-9a-f]{64}$ ]] || fail binding-checksum-format
[[ -f "$binding_file" && ! -L "$binding_file" ]] || fail binding-file-required
actual_sha="$(shasum -a 256 "$binding_file" | awk '{print $1}')"
[[ "$actual_sha" == "$expected_sha" ]] || fail binding-checksum

operation_id="$(jq -er '.operationId' "$binding_file")"
source_sha="$(jq -er '.sourceSha' "$binding_file")"
subscription_id="$(jq -er '.subscriptionId' "$binding_file")"
resource_group_id="$(jq -er '.resourceGroupId' "$binding_file")"
resources=(); while IFS= read -r resource_id; do resources+=("$resource_id"); done < <(jq -er '.resources[]' "$binding_file")
jq -e '(keys|sort)==["operationId","resourceGroupId","resources","sourceSha","subscriptionId"] and (.resources|unique|length)==13' "$binding_file" >/dev/null || fail binding-shape
[[ "$operation_id" =~ ^broker-foundation-cleanup-[a-z0-9]{16,64}$ ]] || fail operation-binding
[[ "$source_sha" =~ ^[0-9a-f]{40}$ ]] || fail source-binding
[[ "$subscription_id" =~ ^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$ ]] || fail subscription-binding
[[ "$resource_group_id" =~ ^/subscriptions/${subscription_id}/resourceGroups/[A-Za-z0-9._()-]+$ ]] || fail resource-group-binding
[[ ${#resources[@]} -eq 13 ]] || fail resource-count-binding

expected_suffixes=(
  '/providers/Microsoft.Web/sites/func-adventures-suite-runner-broker-dev'
  '/providers/Microsoft.Web/serverfarms/plan-adventures-suite-runner-broker-dev'
  '/providers/Microsoft.Insights/components/appi-adventures-suite-runner-broker-dev'
  '/providers/Microsoft.OperationalInsights/workspaces/log-adventures-suite-runner-broker-dev'
  '/providers/Microsoft.Network/privateEndpoints/pe-adventures-runner-blob-dev'
  '/providers/Microsoft.Network/privateEndpoints/pe-adventures-runner-queue-dev'
  '/providers/Microsoft.Network/privateEndpoints/pe-adventures-runner-table-dev'
  '/providers/Microsoft.Network/privateEndpoints/pe-adventures-runner-kv-dev'
  '/providers/Microsoft.Storage/storageAccounts/stadvsrunnerbrokerdev'
  '/providers/Microsoft.KeyVault/vaults/kv-adventures-runner-dev'
  '/providers/Microsoft.Network/privateDnsZones/privatelink.queue.core.windows.net'
  '/providers/Microsoft.Network/privateDnsZones/privatelink.table.core.windows.net'
)
for index in {0..11}; do
  [[ "${resources[$index]}" == "$resource_group_id"/* ]] || fail resource-group-binding
  [[ "${resources[$index]}" == *"${expected_suffixes[$index]}" ]] || fail resource-order-binding
done
[[ "${resources[12]}" == "$resource_group_id"/providers/Microsoft.Network/virtualNetworks/*/subnets/snet-runner-broker-integration ]] || fail resource-group-binding
[[ "${resources[12]}" == */subnets/snet-runner-broker-integration ]] || fail subnet-binding

# Exact dependency order; the first failed or ambiguous operation stops all later deletion.
for resource_id in "${resources[@]}"; do
  az --subscription "$subscription_id" resource show --ids "$resource_id" --query id -o tsv >/dev/null
  az --subscription "$subscription_id" resource delete --ids "$resource_id"
done
printf '{"schemaVersion":1,"operationId":"%s","sourceSha":"%s","state":"DeletionSubmitted","vaultDisposition":"SoftDeletedRetained"}\n' "$operation_id" "$source_sha"
