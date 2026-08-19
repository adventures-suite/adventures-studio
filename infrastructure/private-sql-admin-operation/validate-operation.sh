#!/usr/bin/env bash
set -euo pipefail
: "${OPERATION_MODE:?}" "${REPOSITORY_ID:?}" "${ORGANIZATION_ID:?}" "${SOURCE_SHA:?}" "${CURRENT_PROTECTED_MAIN_SHA:?}" "${WORKFLOW_SHA256:?}" "${BASELINE_SQL_SHA256:?}" "${OPERATION_ID:?}"
: "${PACKAGE_RUN_ID:?}" "${PACKAGE_ARTIFACT_ID:?}" "${PACKAGE_SHA256:?}" "${CATALOG_SHA256:?}"
: "${ADMINISTRATOR_IDENTITY_RESOURCE_ID:?}" "${ADMINISTRATOR_CLIENT_ID:?}" "${ADMINISTRATOR_PRINCIPAL_ID:?}" "${MIGRATION_IDENTITY_RESOURCE_ID:?}"
: "${MIGRATION_PRINCIPAL_ID:?}" "${MIGRATION_CLIENT_ID:?}"
: "${SQL_SERVER_RESOURCE_ID:?}" "${SQL_DATABASE_NAME:?}" "${SQL_PRIVATE_ENDPOINT_RESOURCE_ID:?}"

arm_id_equals() {
  local actual="$1" expected="$2"
  local actual_lower expected_lower
  local arm_id_pattern='^/subscriptions/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/resourcegroups/[a-z0-9._()-]+/providers/[a-z0-9.]+/[a-z0-9.]+/[a-z0-9._()-]+$'
  actual_lower="$(LC_ALL=C tr '[:upper:]' '[:lower:]' <<<"$actual")"
  expected_lower="$(LC_ALL=C tr '[:upper:]' '[:lower:]' <<<"$expected")"
  [[ "$actual_lower" =~ $arm_id_pattern && "$expected_lower" =~ $arm_id_pattern ]] || return 1
  [[ "$actual_lower" == "$expected_lower" ]]
}

[[ "$REPOSITORY_ID" == 1317655952 && "$ORGANIZATION_ID" == 316268438 ]] || exit 1
[[ "$SOURCE_SHA" =~ ^[0-9a-f]{40}$ && "$SOURCE_SHA" == "$CURRENT_PROTECTED_MAIN_SHA" ]] || exit 1
[[ "$WORKFLOW_SHA256" =~ ^[0-9a-f]{64}$ && "$BASELINE_SQL_SHA256" =~ ^[0-9a-f]{64}$ && "$OPERATION_ID" =~ ^[a-z0-9][a-z0-9-]{7,31}$ ]] || exit 1
[[ "$PACKAGE_RUN_ID" =~ ^[1-9][0-9]{7,19}$ && "$PACKAGE_ARTIFACT_ID" =~ ^[1-9][0-9]{7,19}$ ]] || exit 1
[[ "$PACKAGE_SHA256" =~ ^[0-9a-f]{64}$ && "$CATALOG_SHA256" =~ ^[0-9a-f]{64}$ ]] || exit 1
subscription=5ace9cdd-06d1-47d9-8214-1e7c756d076a
arm_id_equals "$ADMINISTRATOR_IDENTITY_RESOURCE_ID" "/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-sql-bootstrap-dev" || exit 1
arm_id_equals "$MIGRATION_IDENTITY_RESOURCE_ID" "/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-job-dev" || exit 1
! arm_id_equals "$ADMINISTRATOR_IDENTITY_RESOURCE_ID" "$MIGRATION_IDENTITY_RESOURCE_ID" || exit 1
[[ "$ADMINISTRATOR_CLIENT_ID" =~ ^[0-9a-f-]{36}$ && "$ADMINISTRATOR_PRINCIPAL_ID" =~ ^[0-9a-f-]{36}$ ]] || exit 1
[[ "$MIGRATION_PRINCIPAL_ID" == ffc9a4bd-67c4-44af-82dc-b7f663f8bea5 ]] || exit 1
[[ "$MIGRATION_CLIENT_ID" == d0da8236-91dc-4454-8a3d-19d08a406e5d ]] || exit 1
arm_id_equals "$SQL_SERVER_RESOURCE_ID" "/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/adventures-suite-dev-sql" || exit 1
[[ "$SQL_DATABASE_NAME" == AdventuresSuiteDevelopment ]] || exit 1
arm_id_equals "$SQL_PRIVATE_ENDPOINT_RESOURCE_ID" "/subscriptions/$subscription/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Network/privateEndpoints/pe-adventures-suite-dev-sql" || exit 1
case "$OPERATION_MODE" in
  baseline)
    [[ -z "${OPERATION_APPROVAL_SHA256:-}" && -z "${SUPPORT_ID:-}" && -z "${CORRELATION_ID:-}" ]] || exit 1
    ;;
  bootstrap|cleanup|denial-proof)
    [[ "${OPERATION_APPROVAL_SHA256:-}" =~ ^[0-9a-f]{64}$ && -z "${SUPPORT_ID:-}" && -z "${CORRELATION_ID:-}" ]] || exit 1
    ;;
  bootstrap-policy-role)
    [[ "${OPERATION_APPROVAL_SHA256:-}" =~ ^[0-9a-f]{64}$ ]] || exit 1
    [[ "${SUPPORT_ID:-}" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{7,63}$ ]] || exit 1
    [[ "${CORRELATION_ID:-}" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{7,63}$ ]] || exit 1
    ;;
  *) exit 1 ;;
esac
