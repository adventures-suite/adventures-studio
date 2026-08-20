#!/usr/bin/env bash
set -euo pipefail

readonly script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly policy_path="$script_dir/authority-policy.json"
readonly subscription_id="5ace9cdd-06d1-47d9-8214-1e7c756d076a"
readonly tenant_id="d7add2bb-ac03-49a8-9377-d0bf6a012f2f"
readonly resource_group="rg-adventures-suite-dev"
readonly sql_server="adventures-suite-dev-sql"
readonly normal_name="AdventuresSuite Development SQL Bootstrap Administrators"
readonly normal_object_id="99a35676-95b4-47d9-bbde-74ece42ebcae"
readonly temporary_name="id-adventures-suite-sql-bootstrap-dev"
readonly temporary_principal_id="34069e5e-75f9-42ac-a7f8-f0115e9434bb"
readonly temporary_client_id="9de6645c-8e83-4e28-af7a-e4d6408e8bb4"

fail() {
  printf 'Authority operation stopped: %s\n' "$1" >&2
  exit 1
}

sha256_file() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  else
    shasum -a 256 "$1" | awk '{print $1}'
  fi
}

sha256_text() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum | awk '{print $1}'
  else
    shasum -a 256 | awk '{print $1}'
  fi
}

require_tools() {
  command -v az >/dev/null 2>&1 || fail "Azure CLI is required."
  command -v git >/dev/null 2>&1 || fail "Git is required."
  command -v jq >/dev/null 2>&1 || fail "jq is required."
  test -f "$policy_path" || fail "The authority policy is unavailable."
}

require_source_binding() {
  [[ "${APPROVED_SOURCE_SHA:-}" =~ ^[0-9a-f]{40}$ ]] || fail "Set the exact protected-main APPROVED_SOURCE_SHA."
  [[ "${AUTHORITY_OPERATION_ID:-}" =~ ^[a-z0-9][a-z0-9-]{7,31}$ ]] || fail "Set a bounded AUTHORITY_OPERATION_ID."
  test "$(git rev-parse HEAD)" = "$APPROVED_SOURCE_SHA" || fail "The checkout is not the approved source SHA."
  test "$(git ls-remote origin refs/heads/main | awk '{print $1}')" = "$APPROVED_SOURCE_SHA" || fail "Protected main advanced."
  git diff --quiet && git diff --cached --quiet || fail "The checkout is not clean."
}

require_human_owner_session() {
  local account
  account="$(az account show --query '{subscription:id,tenant:tenantId,userType:user.type}' -o json)"
  test "$(jq -r '.subscription' <<<"$account")" = "$subscription_id" || fail "The Azure subscription is not approved."
  test "$(jq -r '.tenant' <<<"$account")" = "$tenant_id" || fail "The Azure tenant is not approved."
  test "$(jq -r '.userType' <<<"$account")" = "user" || fail "A human Owner Azure session is required."
}

read_admin() {
  az sql server ad-admin list \
    --subscription "$subscription_id" \
    --resource-group "$resource_group" \
    --server "$sql_server" \
    --query '[0].{name:login,objectId:sid,tenantId:tenantId,administratorType:administratorType,azureAdOnly:azureAdOnlyAuthentication}' \
    -o json
}

require_admin() {
  local expected_name="$1" expected_object_id="$2" admin
  admin="$(read_admin)"
  test "$(jq -r '.name' <<<"$admin")" = "$expected_name" || fail "The live administrator name is not the approved pre-state."
  test "$(jq -r '.objectId' <<<"$admin")" = "$expected_object_id" || fail "The live administrator object is not the approved pre-state."
  test "$(jq -r '.tenantId' <<<"$admin")" = "$tenant_id" || fail "The live administrator tenant is not approved."
  test "$(jq -r '.administratorType' <<<"$admin")" = "ActiveDirectory" || fail "The administrator type is not approved."
  test "$(jq -r '.azureAdOnly' <<<"$admin")" = "true" || fail "Azure AD-only authentication is not enforced."
}

require_temporary_identity() {
  local identity
  identity="$(az identity show \
    --subscription "$subscription_id" \
    --resource-group "$resource_group" \
    --name "$temporary_name" \
    --query '{clientId:clientId,principalId:principalId}' -o json)"
  test "$(jq -r '.clientId' <<<"$identity")" = "$temporary_client_id" || fail "The temporary administrator client ID changed."
  test "$(jq -r '.principalId' <<<"$identity")" = "$temporary_principal_id" || fail "The temporary administrator principal ID changed."
}

packet() {
  local transition="$1" expected_name="$2" expected_id="$3" desired_name="$4" desired_id="$5"
  jq -cnS \
    --arg schemaVersion "1" \
    --arg transition "$transition" \
    --arg sourceSha "$APPROVED_SOURCE_SHA" \
    --arg operationId "$AUTHORITY_OPERATION_ID" \
    --arg scriptSha256 "$(sha256_file "${BASH_SOURCE[0]}")" \
    --arg policySha256 "$(sha256_file "$policy_path")" \
    --arg subscriptionId "$subscription_id" \
    --arg tenantId "$tenant_id" \
    --arg resourceGroup "$resource_group" \
    --arg sqlServer "$sql_server" \
    --arg expectedAdministratorName "$expected_name" \
    --arg expectedAdministratorObjectId "$expected_id" \
    --arg desiredAdministratorName "$desired_name" \
    --arg desiredAdministratorObjectId "$desired_id" \
    '{schemaVersion:($schemaVersion|tonumber),transition:$transition,sourceSha:$sourceSha,operationId:$operationId,scriptSha256:$scriptSha256,policySha256:$policySha256,subscriptionId:$subscriptionId,tenantId:$tenantId,resourceGroup:$resourceGroup,sqlServer:$sqlServer,expectedAdministratorName:$expectedAdministratorName,expectedAdministratorObjectId:$expectedAdministratorObjectId,desiredAdministratorName:$desiredAdministratorName,desiredAdministratorObjectId:$desiredAdministratorObjectId,automaticRetryCount:0}'
}

prepare() {
  local transition="$1" expected_name="$2" expected_id="$3" desired_name="$4" desired_id="$5" value
  require_admin "$expected_name" "$expected_id"
  value="$(packet "$transition" "$expected_name" "$expected_id" "$desired_name" "$desired_id")"
  printf '%s\n' "$value"
  printf 'approvalSha256=%s\n' "$(printf '%s' "$value" | sha256_text)"
}

execute_transition() {
  local transition="$1" expected_name="$2" expected_id="$3" desired_name="$4" desired_id="$5" value digest
  require_admin "$expected_name" "$expected_id"
  value="$(packet "$transition" "$expected_name" "$expected_id" "$desired_name" "$desired_id")"
  digest="$(printf '%s' "$value" | sha256_text)"
  test "${AUTHORITY_APPROVAL_SHA256:-}" = "$digest" || fail "The separate authority approval digest does not match."

  az sql server ad-admin create \
    --subscription "$subscription_id" \
    --resource-group "$resource_group" \
    --server "$sql_server" \
    --display-name "$desired_name" \
    --object-id "$desired_id" \
    --only-show-errors -o none

  require_admin "$desired_name" "$desired_id"
  jq -cnS --arg transition "$transition" --arg operationId "$AUTHORITY_OPERATION_ID" \
    --arg sourceSha "$APPROVED_SOURCE_SHA" --arg approvalSha256 "$digest" \
    '{schemaVersion:1,transition:$transition,operationId:$operationId,sourceSha:$sourceSha,approvalSha256:$approvalSha256,readbackVerified:true,azureAdOnlyAuthentication:true}'
}

require_tools
require_source_binding
require_human_owner_session
require_temporary_identity

case "${1:-}" in
  prepare-establish)
    prepare establish "$normal_name" "$normal_object_id" "$temporary_name" "$temporary_principal_id"
    ;;
  establish)
    execute_transition establish "$normal_name" "$normal_object_id" "$temporary_name" "$temporary_principal_id"
    ;;
  prepare-restore)
    prepare restore "$temporary_name" "$temporary_principal_id" "$normal_name" "$normal_object_id"
    ;;
  restore)
    execute_transition restore "$temporary_name" "$temporary_principal_id" "$normal_name" "$normal_object_id"
    ;;
  *)
    fail "Use prepare-establish, establish, prepare-restore, or restore."
    ;;
esac
