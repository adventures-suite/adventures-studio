#!/usr/bin/env bash
set -euo pipefail
: "${OPERATION_MODE:?}" "${REPOSITORY_ID:?}" "${ORGANIZATION_ID:?}" "${SOURCE_SHA:?}" "${CURRENT_PROTECTED_MAIN_SHA:?}" "${WORKFLOW_SHA256:?}" "${OPERATION_ID:?}"
: "${PACKAGE_RUN_ID:?}" "${PACKAGE_ARTIFACT_ID:?}" "${PACKAGE_SHA256:?}" "${CATALOG_SHA256:?}"
: "${ADMINISTRATOR_IDENTITY_RESOURCE_ID:?}" "${ADMINISTRATOR_CLIENT_ID:?}" "${ADMINISTRATOR_PRINCIPAL_ID:?}" "${MIGRATION_IDENTITY_RESOURCE_ID:?}"
: "${SQL_SERVER_RESOURCE_ID:?}" "${SQL_DATABASE_NAME:?}" "${SQL_PRIVATE_ENDPOINT_RESOURCE_ID:?}"
[[ "$REPOSITORY_ID" == 1317655952 && "$ORGANIZATION_ID" == 316268438 ]] || exit 1
[[ "$SOURCE_SHA" =~ ^[0-9a-f]{40}$ && "$SOURCE_SHA" == "$CURRENT_PROTECTED_MAIN_SHA" ]] || exit 1
[[ "$WORKFLOW_SHA256" =~ ^[0-9a-f]{64}$ && "$OPERATION_ID" =~ ^[a-z0-9][a-z0-9-]{7,31}$ ]] || exit 1
[[ "$PACKAGE_RUN_ID" =~ ^[1-9][0-9]{7,19}$ && "$PACKAGE_ARTIFACT_ID" =~ ^[1-9][0-9]{7,19}$ ]] || exit 1
[[ "$PACKAGE_SHA256" =~ ^[0-9a-f]{64}$ && "$CATALOG_SHA256" =~ ^[0-9a-f]{64}$ ]] || exit 1
[[ "$ADMINISTRATOR_IDENTITY_RESOURCE_ID" =~ ^/subscriptions/[0-9a-f-]{36}/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-sql-bootstrap-dev$ ]] || exit 1
[[ "$MIGRATION_IDENTITY_RESOURCE_ID" =~ /userAssignedIdentities/id-adventures-suite-migrate-job-dev$ ]] || exit 1
administrator_identity_lower="$(printf '%s' "$ADMINISTRATOR_IDENTITY_RESOURCE_ID" | tr '[:upper:]' '[:lower:]')"
migration_identity_lower="$(printf '%s' "$MIGRATION_IDENTITY_RESOURCE_ID" | tr '[:upper:]' '[:lower:]')"
[[ "$administrator_identity_lower" != "$migration_identity_lower" ]] || exit 1
[[ "$ADMINISTRATOR_CLIENT_ID" =~ ^[0-9a-f-]{36}$ && "$ADMINISTRATOR_PRINCIPAL_ID" =~ ^[0-9a-f-]{36}$ ]] || exit 1
[[ "$SQL_SERVER_RESOURCE_ID" =~ ^/subscriptions/[0-9a-f-]{36}/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Sql/servers/[a-z0-9-]+$ ]] || exit 1
[[ "$SQL_DATABASE_NAME" == AdventuresSuiteDevelopment ]] || exit 1
[[ "$SQL_PRIVATE_ENDPOINT_RESOURCE_ID" =~ ^/subscriptions/[0-9a-f-]{36}/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.Network/privateEndpoints/[a-z0-9-]+$ ]] || exit 1
case "$OPERATION_MODE" in
  baseline) [[ -z "${BOOTSTRAP_APPROVAL_SHA256:-}" ]] || exit 1 ;;
  bootstrap) [[ "${BOOTSTRAP_APPROVAL_SHA256:-}" =~ ^[0-9a-f]{64}$ ]] || exit 1 ;;
  *) exit 1 ;;
esac
