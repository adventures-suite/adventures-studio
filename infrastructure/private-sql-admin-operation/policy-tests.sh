#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
dir="$root/infrastructure/private-sql-admin-operation"; workflow="$root/.github/workflows/private-sql-admin-operation.yml"
require(){ rg -q --fixed-strings -- "$1" "$2"; }; reject(){ ! rg -q --fixed-strings -- "$1" "$2"; }
"$dir/validate-baseline-sql.sh"
jq -e '.workloadName=="id-adventures-suite-sql-bootstrap-dev" and (.prohibited|index("migrationUami"))!=null and (.prohibited|index("EntraGroupMembership"))!=null and (.prohibited|index("directoryReaders"))!=null and .authentication.maximumOperationMinutes==30 and .authentication.source=="GitHubOidcAzureCli"' "$dir/identity-model.json" >/dev/null
jq -e '.credential=="Azure.Identity.AzureCliCredential" and .identitySelector=="exactAdministratorClientId" and .tokenScope=="https://database.windows.net/.default" and .delivery=="Microsoft.Data.SqlClient.SqlConnection.AccessToken" and .operationLimitMinutes==30 and .processReuse==false and .implementationState=="hostedVnetRunnerImplemented" and (.prohibitedSurfaces|index("environment"))!=null' "$dir/authentication-contract.json" >/dev/null
jq -e '.additionalProperties==false and .properties.permissions.maxItems==168 and .properties.principals.maxItems==1 and .properties.principals.items.properties.name.const=="AdventuresSuiteMigrationDev-ffc9a" and .properties.journal.properties.scripts.maxItems==12 and .properties.roles.maxItems==5 and .properties.residue.properties.resourceCount.const==0' "$dir/evidence.schema.json" >/dev/null
jq -e '.additionalProperties==false and .properties.operation.const=="companion-policy-runtime-role-bootstrap-v1" and .properties.roleName.const=="AdventuresSuiteCompanionPolicyRuntime" and .properties.owner.const=="dbo" and .properties.memberCount.const==0 and .properties.parentRoleCount.const==0 and .properties.explicitPermissionCount.const==0 and .properties.ownedSecurableCount.const==0 and .properties.inheritedApplicationAuthorityCount.const==0 and .properties.readRuntimeRoleUnchanged.const==true' "$dir/policy-role-evidence.schema.json" >/dev/null
jq -e '.deadlineMinutes==30 and .automaticRetryCount==0 and .baselineMustPrecedeBootstrap==true and .baselineCanInvokeBootstrap==false and .cleanup.independent==true and (.operations==["baseline","bootstrap","bootstrap-policy-role","cleanup","denial-proof"]) and (.failureOutcomes|index("ambiguous"))!=null and (.failureOutcomes|index("cleanupPartial"))!=null and (.authorityBoundaries|length)==4' "$dir/operation-policy.json" >/dev/null
require "environment: database-development" "$workflow"; require "timeout-minutes: 30" "$workflow"; require "cancel-in-progress: false" "$workflow"
require "group: private-sql-migration-vnet" "$workflow"; require "labels: adventures-suite-private-sql" "$workflow"
require "options: [baseline, bootstrap, bootstrap-policy-role, cleanup, denial-proof]" "$workflow"; require "validate-operation.sh" "$workflow"; require "if: always()" "$workflow"
require "package_artifact_id" "$workflow"; require "package_sha256" "$workflow"; require "catalog_sha256" "$workflow"
require "azure/login@8216e11d8cd9b42fe925c852af8e76311ff067ac" "$workflow"; require "allow-no-subscriptions: true" "$workflow"
require '--admin-baseline' "$workflow"; require '--admin-bootstrap' "$workflow"; require '--admin-cleanup' "$workflow"; require '--admin-denial-proof' "$workflow"
require '--admin-bootstrap-companion-policy-role' "$workflow"
reject "az deployment" "$workflow"; reject "sqlcmd" "$workflow"; reject "--bootstrap-sql" "$workflow"; reject "continue-on-error" "$workflow"
reject "metadata/identity/oauth2/token" "$workflow"; reject "SQL_TOKEN" "$workflow"; reject "Bearer " "$workflow"
reject "publicIPAddress" "$workflow"; reject "firewallRules" "$workflow"; reject "retry" "$workflow"; reject "ManagedIdentityCredential" "$workflow"
require "MigrationCredentialMode.GitHubOidcAzureCli" "$root/src/AdventuresSuite.DatabaseMigrator/SqlAdministratorOperationRunner.cs"
require 'AzureCliSqlAudience = "https://database.windows.net"' "$root/src/AdventuresSuite.DatabaseMigrator/MigrationIdentityValidator.cs"
require 'ManagedIdentitySqlAudience = "https://database.windows.net/"' "$root/src/AdventuresSuite.DatabaseMigrator/MigrationIdentityValidator.cs"
test "$(rg -c '^\s*- uses:' "$workflow")" = 2
test "$(rg -c 'actions/checkout@11d5960a326750d5838078e36cf38b85af677262' "$workflow")" = 1
require "WHERE name = N'AdventuresSuiteMigrationDev-ffc9a'" "$dir/baseline.sql"
require "CREATE USER {quotedAlias} WITH SID = " "$root/src/AdventuresSuite.DatabaseMigrator/AzureDevelopmentBootstrapper.cs"
reject "FROM EXTERNAL PROVIDER" "$root/src/AdventuresSuite.DatabaseMigrator/AzureDevelopmentBootstrapper.cs"
sha="$(printf 'a%.0s' {1..40})"; digest="$(printf 'b%.0s' {1..64})"
subscription=5ace9cdd-06d1-47d9-8214-1e7c756d076a
admin_id="/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-sql-bootstrap-dev"
migration_id="/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-job-dev"
server_id="/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/adventures-suite-dev-sql"
endpoint_id="/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Network/privateEndpoints/pe-adventures-suite-dev-sql"
validate(){ (
  export OPERATION_MODE=baseline REPOSITORY_ID=1317655952 ORGANIZATION_ID=316268438 SOURCE_SHA="$sha" CURRENT_PROTECTED_MAIN_SHA="$sha" WORKFLOW_SHA256="$digest" BASELINE_SQL_SHA256="$digest" OPERATION_ID=admin-op-01
  export PACKAGE_RUN_ID=12345678 PACKAGE_ARTIFACT_ID=23456789 PACKAGE_SHA256="$digest" CATALOG_SHA256="$digest"
  export ADMINISTRATOR_IDENTITY_RESOURCE_ID="$admin_id" ADMINISTRATOR_CLIENT_ID=00000000-0000-0000-0000-000000000001 ADMINISTRATOR_PRINCIPAL_ID=00000000-0000-0000-0000-000000000002 MIGRATION_IDENTITY_RESOURCE_ID="$migration_id"
  export MIGRATION_PRINCIPAL_ID=ffc9a4bd-67c4-44af-82dc-b7f663f8bea5 MIGRATION_CLIENT_ID=d0da8236-91dc-4454-8a3d-19d08a406e5d
  export SQL_SERVER_RESOURCE_ID="$server_id" SQL_DATABASE_NAME=AdventuresSuiteDevelopment SQL_PRIVATE_ENDPOINT_RESOURCE_ID="$endpoint_id"
  for assignment in "$@"; do export "$assignment"; done
  "$dir/validate-operation.sh" >/dev/null 2>&1
); }
validate
while IFS= read -r valid_id; do validate "MIGRATION_IDENTITY_RESOURCE_ID=$valid_id"; done < <(jq -r '.validMigrationIdentityIds[]' "$dir/arm-resource-id-fixtures.json")
while IFS= read -r invalid_id; do
  if validate "MIGRATION_IDENTITY_RESOURCE_ID=$invalid_id"; then echo "invalid ARM ID accepted: $invalid_id" >&2; exit 1; fi
done < <(jq -r '.invalidMigrationIdentityIds[]' "$dir/arm-resource-id-fixtures.json")
validate \
  "ADMINISTRATOR_IDENTITY_RESOURCE_ID=${admin_id/resourceGroups/resourcegroups}" \
  "MIGRATION_IDENTITY_RESOURCE_ID=${migration_id/resourceGroups/resourcegroups}" \
  "SQL_SERVER_RESOURCE_ID=${server_id/resourceGroups/resourcegroups}" \
  "SQL_PRIVATE_ENDPOINT_RESOURCE_ID=${endpoint_id/resourceGroups/resourcegroups}"
for invalid in \
  'REPOSITORY_ID=1' \
  'SOURCE_SHA=cccccccccccccccccccccccccccccccccccccccc' \
  'OPERATION_ID=bad' \
  'PACKAGE_ARTIFACT_ID=bad' \
  "ADMINISTRATOR_IDENTITY_RESOURCE_ID=$migration_id" \
  'MIGRATION_PRINCIPAL_ID=00000000-0000-0000-0000-000000000000' \
  'MIGRATION_CLIENT_ID=00000000-0000-0000-0000-000000000000' \
  'SQL_SERVER_RESOURCE_ID=/bad' \
  'SQL_DATABASE_NAME=wrong' \
  'SQL_PRIVATE_ENDPOINT_RESOURCE_ID=/bad' \
  "MIGRATION_IDENTITY_RESOURCE_ID=${migration_id/providers\/Microsoft.ManagedIdentity/providers\/Microsoft.Network}" \
  "SQL_SERVER_RESOURCE_ID=$server_id/databases/extra" \
  "SQL_PRIVATE_ENDPOINT_RESOURCE_ID=${endpoint_id/pe-adventures-suite-dev-sql/pe-substituted}" \
  "ADMINISTRATOR_IDENTITY_RESOURCE_ID=$admin_id?api-version=1" \
  'OPERATION_APPROVAL_SHA256=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'; do
  if validate "$invalid"; then echo "invalid binding accepted" >&2; exit 1; fi
done
for mode in bootstrap cleanup denial-proof; do
  if validate OPERATION_MODE="$mode"; then echo "$mode lacked separate approval" >&2; exit 1; fi
  validate OPERATION_MODE="$mode" OPERATION_APPROVAL_SHA256="$digest"
done
if validate OPERATION_MODE=bootstrap-policy-role OPERATION_APPROVAL_SHA256="$digest"; then
  echo "policy-role bootstrap lacked bounded evidence identifiers" >&2; exit 1
fi
validate OPERATION_MODE=bootstrap-policy-role OPERATION_APPROVAL_SHA256="$digest" SUPPORT_ID=support-0001 CORRELATION_ID=correlation-0001
for invalid in 'SUPPORT_ID=bad' 'CORRELATION_ID=bad' 'SUPPORT_ID=case altered' 'CORRELATION_ID=case altered'; do
  if validate OPERATION_MODE=bootstrap-policy-role OPERATION_APPROVAL_SHA256="$digest" SUPPORT_ID=support-0001 CORRELATION_ID=correlation-0001 "$invalid"; then
    echo "invalid policy-role evidence binding accepted" >&2; exit 1
  fi
done
require 'CompanionPolicyRuntimeRoleName = "AdventuresSuiteCompanionPolicyRuntime"' "$root/src/AdventuresSuite.DatabaseMigrator/SqlAdministratorOperationRunner.cs"
reject 'Environment.GetEnvironmentVariable("ADVENTURESSUITE_COMPANION_POLICY_ROLE_NAME")' "$root/src/AdventuresSuite.DatabaseMigrator/SqlAdministratorOperationRunner.cs"
echo 'private SQL administrator operation policy tests passed'
