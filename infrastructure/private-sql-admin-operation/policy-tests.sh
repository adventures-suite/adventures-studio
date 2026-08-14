#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
dir="$root/infrastructure/private-sql-admin-operation"; workflow="$root/.github/workflows/private-sql-admin-operation.yml"
require(){ rg -q --fixed-strings -- "$1" "$2"; }; reject(){ ! rg -q --fixed-strings -- "$1" "$2"; }
"$dir/validate-baseline-sql.sh"
jq -e '.workloadName=="id-adventures-suite-sql-bootstrap-dev" and (.prohibited|index("migrationUami"))!=null and (.prohibited|index("EntraGroupMembership"))!=null and .authentication.maximumOperationMinutes==45' "$dir/identity-model.json" >/dev/null
jq -e '.credential=="Azure.Identity.ManagedIdentityCredential" and .identitySelector=="exactAdministratorClientId" and .tokenScope=="https://database.windows.net/.default" and .delivery=="Microsoft.Data.SqlClient.SqlConnection.AccessTokenCallback" and .operationLimitMinutes==45 and .processReuse==false and .implementationState=="readerNotImplementedWorkflowGuarded" and (.prohibitedSurfaces|index("environment"))!=null' "$dir/authentication-contract.json" >/dev/null
jq -e '.additionalProperties==false and .properties.permissions.maxItems==128 and .properties.journal.properties.scripts.maxItems==9 and .properties.residue.properties.resourceCount.const==0' "$dir/evidence.schema.json" >/dev/null
jq -e '.deadlineMinutes==45 and .automaticRetryCount==0 and .baselineMustPrecedeBootstrap==true and .baselineCanInvokeBootstrap==false and .cleanup.independent==true and (.cleanup.requiredAfter|index("runnerLost"))!=null and (.failureOutcomes|index("ambiguous"))!=null and (.failureOutcomes|index("cleanupPartial"))!=null and (.authorityBoundaries|length)==4' "$dir/operation-policy.json" >/dev/null
require "Fail closed before Azure login, provisioning, or SQL" "$workflow"; require "environment: database-development" "$workflow"; require "timeout-minutes: 10" "$workflow"; require "cancel-in-progress: false" "$workflow"
require "bootstrap_approval_sha256" "$workflow"; require "operation_mode" "$workflow"; require "validate-operation.sh" "$workflow"; require "independent-cleanup" "$workflow"; require "if: always()" "$workflow"
require "package_artifact_id" "$workflow"; require "package_sha256" "$workflow"; require "catalog_sha256" "$workflow"
reject "azure/login@" "$workflow"; reject "az deployment" "$workflow"; reject "sqlcmd" "$workflow"; reject "--bootstrap-sql" "$workflow"; reject "continue-on-error" "$workflow"
reject "metadata/identity/oauth2/token" "$workflow"; reject "SQL_TOKEN" "$workflow"; reject "Bearer " "$workflow"
reject "snet-devtools" "$dir/main.bicep"; reject "publicIPAddress" "$dir/main.bicep"; reject "destinationPortRange: '22'" "$dir/main.bicep"; reject "3389" "$dir/main.bicep"; reject "firewallRules" "$dir/main.bicep"; reject "retry" "$workflow"
require "../private-migration-runner/main.bicep" "$dir/main.bicep"; require "administratorIdentityResourceId" "$dir/main.bicep"; require "toLower(administratorIdentityResourceId) != toLower(migrationIdentityResourceId)" "$dir/main.bicep"
test "$(rg -c '^\s*- uses:' "$workflow")" = 2
test "$(rg -c 'actions/checkout@11d5960a326750d5838078e36cf38b85af677262' "$workflow")" = 2
sha="$(printf 'a%.0s' {1..40})"; digest="$(printf 'b%.0s' {1..64})"
subscription=5ace9cdd-06d1-47d9-8214-1e7c756d076a
admin_id="/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-sql-bootstrap-dev"
migration_id="/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-job-dev"
server_id="/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/sql-adventures-suite-dev"
endpoint_id="/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Network/privateEndpoints/pe-sql-adventures-suite-dev"
validate(){ (
  export OPERATION_MODE=baseline REPOSITORY_ID=1317655952 ORGANIZATION_ID=316268438 SOURCE_SHA="$sha" CURRENT_PROTECTED_MAIN_SHA="$sha" WORKFLOW_SHA256="$digest" OPERATION_ID=admin-op-01
  export PACKAGE_RUN_ID=12345678 PACKAGE_ARTIFACT_ID=23456789 PACKAGE_SHA256="$digest" CATALOG_SHA256="$digest"
  export ADMINISTRATOR_IDENTITY_RESOURCE_ID="$admin_id" ADMINISTRATOR_CLIENT_ID=00000000-0000-0000-0000-000000000001 ADMINISTRATOR_PRINCIPAL_ID=00000000-0000-0000-0000-000000000002 MIGRATION_IDENTITY_RESOURCE_ID="$migration_id"
  export SQL_SERVER_RESOURCE_ID="$server_id" SQL_DATABASE_NAME=AdventuresSuiteDevelopment SQL_PRIVATE_ENDPOINT_RESOURCE_ID="$endpoint_id"
  for assignment in "$@"; do export "$assignment"; done
  "$dir/validate-operation.sh" >/dev/null 2>&1
); }
validate
for invalid in \
  'REPOSITORY_ID=1' \
  'SOURCE_SHA=cccccccccccccccccccccccccccccccccccccccc' \
  'OPERATION_ID=bad' \
  'PACKAGE_ARTIFACT_ID=bad' \
  "ADMINISTRATOR_IDENTITY_RESOURCE_ID=$migration_id" \
  'SQL_SERVER_RESOURCE_ID=/bad' \
  'SQL_DATABASE_NAME=wrong' \
  'SQL_PRIVATE_ENDPOINT_RESOURCE_ID=/bad' \
  'BOOTSTRAP_APPROVAL_SHA256=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'; do
  if validate "$invalid"; then echo "invalid binding accepted" >&2; exit 1; fi
done
if validate OPERATION_MODE=bootstrap; then echo "bootstrap lacked separate approval" >&2; exit 1; fi
validate OPERATION_MODE=bootstrap BOOTSTRAP_APPROVAL_SHA256="$digest"
echo 'private SQL administrator operation policy tests passed'
