#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
dir="$root/infrastructure/runner-registration-broker"
workflow="$root/.github/workflows/ephemeral-runner-registration-broker.yml"
require(){ grep -Fq -- "$1" "$2"; }
reject(){ ! grep -Fq -- "$1" "$2"; }

npm --prefix "$dir" test
compiled="$(mktemp)"
trap 'rm -f -- "$compiled"' EXIT
az bicep build --file "$dir/main.bicep" --stdout --no-restore >"$compiled"
jq -e '
  .variables.functionName=="func-adventures-suite-runner-broker-dev" and
  .variables.planName=="plan-adventures-suite-runner-broker-dev" and
  .variables.storageName=="stadvsrunnerbrokerdev" and
  .variables.vaultName=="kv-adventures-runner-dev" and
  ([.resources[]|select(.type=="Microsoft.Web/sites")][0].properties.functionAppConfig.runtime=={"name":"node","version":"24"}) and
  ([.resources[]|select(.type=="Microsoft.Web/sites")][0].properties.functionAppConfig.scaleAndConcurrency.maximumInstanceCount==1) and
  ([.resources[]|select(.type=="Microsoft.Web/sites")][0].properties.functionAppConfig.scaleAndConcurrency.instanceMemoryMB==512) and
  ([.resources[]|select(.type=="Microsoft.Web/sites")][0].properties.functionAppConfig.scaleAndConcurrency.alwaysReady==[]) and
  ([.resources[]|select(.type=="Microsoft.Storage/storageAccounts")][0].properties.allowSharedKeyAccess==false) and
  ([.resources[]|select(.type=="Microsoft.Storage/storageAccounts")][0].properties.publicNetworkAccess=="Disabled") and
  ([.resources[]|select(.type=="Microsoft.KeyVault/vaults")][0].properties.publicNetworkAccess=="Disabled") and
  ([.resources[]|select(.type=="Microsoft.KeyVault/vaults")][0].properties.enablePurgeProtection==true) and
  ([.resources[]|select(.type=="Microsoft.KeyVault/vaults")][0].properties.softDeleteRetentionInDays==90) and
  ([.resources[]|select(.type=="Microsoft.Network/virtualNetworks/subnets")][0].properties.addressPrefix=="10.40.4.0/27") and
  ([.resources[]|select(.type=="Microsoft.Network/virtualNetworks/subnets")][0].properties.delegations|length==1) and
  ([.resources[]|select(.type=="Microsoft.Network/virtualNetworks/subnets")][0].properties.delegations[0].properties.serviceName=="Microsoft.App/environments") and
  ([.resources[]|select(.type=="Microsoft.Network/privateEndpoints")]|length==4) and
  ([.resources[]|select(.type=="Microsoft.Network/privateEndpoints")|.properties.privateLinkServiceConnections[0].properties.groupIds[0]]|sort==["blob","queue","table","vault"]) and
  ([.resources[]|select(.type=="Microsoft.Network/privateDnsZones")]|length==2) and
  ([.resources[]|select(.type|endswith("/roleAssignments"))]|length==0) and
  (.outputs.containsCredentialMaterial.value==false)
' "$compiled" >/dev/null

jq -e '.repository.id==1317655952 and .repository.ownerId==316268438 and .maximumLifetimeMinutes==45 and .githubApp.repositorySelection=="selected" and .githubApp.repositoryPermissions.administration=="write" and .githubApp.organizationPermissions=={} and .githubApp.userPermissions=={} and .githubApp.webhooks==false' "$dir/contracts.json" >/dev/null
jq -e '.additionalProperties==false and .properties.deleteAttempts.maximum==1 and .properties.repositoryId.const==1317655952 and .properties.ownerId.const==316268438' "$dir/evidence.schema.json" >/dev/null
jq -e '.additionalProperties==false and .properties.secretName.const=="github-app-4590229-private-key" and (.properties.immutableVersionUri.pattern|contains("/[0-9a-f]{32}$"))' "$dir/key-custody-evidence.schema.json" >/dev/null

require "generate-jitconfig" "$dir/github-adapter.mjs"
require "repository_ids:[1317655952]" "$dir/github-app-adapter.mjs"
require "immutable-secret-version-required" "$dir/azure-adapters.mjs"
require "ManagedIdentityCredential(importerClientId)" "$dir/import-app-key.mjs"
require "const fd = 3" "$dir/import-app-key.mjs"
require "3<&0 </dev/null" "$dir/key-custody-session.sh"
require "ram://65536" "$dir/key-custody-session.sh"
require "hdiutil detach" "$dir/key-custody-session.sh"
require "cleanup-residue" "$dir/key-custody-session.sh"
require "DestinationSecretName = 'github-app-4590229-private-key'" "$dir/key-custody-importer.mjs"
require "DestinationContentType = 'application/x-pem-file'" "$dir/key-custody-importer.mjs"
reject "DefaultAzureCredential" "$dir/import-app-key.mjs"
reject "process.env" "$dir/import-app-key.mjs"
reject "stdin" "$dir/import-app-key.mjs"
reject "KEY_CUSTODY_TEST_IMPORTER" "$dir/key-custody-session.sh"

require "environment: database-development" "$workflow"
require "workflow_dispatch:" "$workflow"
require "if: always()" "$workflow"
require "cancel-in-progress: false" "$workflow"
require "Deliberately stop before Azure login, deployment, arming, or JIT generation" "$workflow"
reject "azure/login@" "$workflow"
reject "az deployment" "$workflow"
reject "import-app-key" "$workflow"
reject "generate-jitconfig" "$workflow"
reject "continue-on-error" "$workflow"
test "$(grep -Ec '^[[:space:]]*- uses:' "$workflow")" = 3
test "$(grep -Fc 'actions/checkout@11d5960a326750d5838078e36cf38b85af677262' "$workflow")" = 2
test "$(grep -Fc 'actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020' "$workflow")" = 1

for prohibited in roleAssignments publicIPAddress networkSecurityGroups virtualMachines Microsoft.Sql firewallRules snet-devtools; do reject "$prohibited" "$dir/main.bicep"; done
for prohibited in jitConfiguration appPrivateKey appJwt installationToken azureToken authorizationHeader rawClaims rawResponses vmBootstrapContent packageUrl connectionString arbitraryLabels; do jq -e --arg p "$prohibited" '.prohibitedMaterial|index($p)!=null' "$dir/contracts.json" >/dev/null; done

echo 'runner registration broker foundation and custody policy tests passed'
