#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

if [ "$#" -ne 9 ]; then exit 2; fi
operation="$1"
expected_template_sha="$2"
expected_parameters_sha="$3"
expected_catalog_sha="$4"
state_file="$5"
evidence_file="$6"
prefix="$7"
assignment_timestamp_utc="$8"
authority_deadline_utc="$9"
error_file="${prefix}.err"
what_if_file="${prefix}.what-if.json"
result_file="${prefix}.result.json"
second_result_file="${prefix}.result-2.json"
policy_file="${prefix}.policy.json"
stage='artifact_validation'
classification='operation_failed'
azure_error_code=''
azure_error_limit=65536
scope="/subscriptions/$APPROVED_SUBSCRIPTION_ID/resourceGroups/$TARGET_RESOURCE_GROUP"
infra_assignment="$scope/providers/Microsoft.Authorization/roleAssignments/5c14d19b-04c7-4dfa-83ed-9447d0ea3c33"
reader_assignment="$scope/providers/Microsoft.Authorization/roleAssignments/fa329695-3907-4852-94f5-fda8a26a4698"

write_state() {
  printf 'stage=%s\nclassification=%s\nazure_error_code=%s\nassignment_timestamp_utc=%s\nauthority_deadline_utc=%s\nexit_code=%s\n' \
    "$stage" "$classification" "$azure_error_code" "$assignment_timestamp_utc" "$authority_deadline_utc" "$1" >"$state_file"
}
run_azure() {
  local restore_errexit=false
  local command_exit
  local -a parsed
  case "$-" in *e*) restore_errexit=true; set +e ;; esac
  "$@" 2> >(
    { head -c "$((azure_error_limit + 1))"; cat >/dev/null; } >"$error_file"
  )
  command_exit="$?"
  wait
  if [ "$command_exit" -ne 0 ]; then
    mapfile -t parsed < <(python3 .github/scripts/classify-azure-error.py "$error_file")
    classification="${parsed[0]:-azure_error_unclassified}"
    azure_error_code="${parsed[1]:-}"
  fi
  if [ "$restore_errexit" = true ]; then set -e; fi
  return "$command_exit"
}
cleanup() {
  original_exit="$?"
  rm -f "$error_file" "$what_if_file" "$result_file" "$second_result_file" "$policy_file"
  if [ "$original_exit" -ne 0 ]; then write_state "$original_exit"; fi
  exit "$original_exit"
}
trap cleanup EXIT

test "$APPROVED_SUBSCRIPTION_ID" = '5ace9cdd-06d1-47d9-8214-1e7c756d076a'
test "$TARGET_RESOURCE_GROUP" = 'rg-adventures-suite-dev'
test "$AZURE_CLIENT_ID" = 'd678e2ad-ada2-4cde-bb79-44630acf1cc8'
test "$EXPECTED_PRINCIPAL_ID" = '822c1c0c-39e1-400f-b9fc-9532a11bae5d'

case "$operation" in
  bootstrap-role-definitions)
    template='infrastructure/container-apps-migrations/deployer-role-definitions.bicep'
    test "$(sha256sum "$template" | awk '{print $1}')" = "$expected_template_sha"
    test -z "$expected_parameters_sha"
    actual_catalog_sha="$(sha256sum \
      infrastructure/container-apps-migrations/roles/infrastructure-deployer.role.json \
      infrastructure/container-apps-migrations/roles/identity-reader.role.json | sha256sum | awk '{print $1}')"
    test "$actual_catalog_sha" = "$expected_catalog_sha"
    node .github/scripts/rbac-boundary-policy.mjs catalog infrastructure/container-apps-migrations/roles/infrastructure-deployer.role.json >/dev/null
    node .github/scripts/rbac-boundary-policy.mjs catalog infrastructure/container-apps-migrations/roles/identity-reader.role.json >/dev/null
    mode='bootstrap'
    deployment_name='migration-deployer-role-definitions'
    parameters=()
    ;;
  assign-foundation-access)
    node .github/scripts/foundation-authority-window.mjs active "$assignment_timestamp_utc" "$authority_deadline_utc" >/dev/null
    template='infrastructure/container-apps-migrations/foundation-temporary-access.bicep'
    parameter_file='infrastructure/container-apps-migrations/foundation-temporary-access.dev.bicepparam'
    test "$(sha256sum "$template" | awk '{print $1}')" = "$expected_template_sha"
    test "$(sha256sum "$parameter_file" | awk '{print $1}')" = "$expected_parameters_sha"
    test -z "$expected_catalog_sha"
    mode='assignment'
    deployment_name='migration-foundation-temporary-access'
    parameters=(--parameters "$parameter_file")
    ;;
  remove-foundation-access)
    test -z "$expected_template_sha" && test -z "$expected_parameters_sha" && test -z "$expected_catalog_sha"
    node .github/scripts/foundation-authority-window.mjs cleanup "$assignment_timestamp_utc" "$authority_deadline_utc" >/dev/null
    stage='assignment_inspection'
    classification='cleanup_failed'
    run_azure az role assignment list --subscription "$APPROVED_SUBSCRIPTION_ID" --assignee-object-id b77b6201-ad26-4f77-8f88-6d0d43f7dbb8 \
      --scope "$scope" --all --only-show-errors --output json >"$result_file"
    deletion_plan="$(node .github/scripts/foundation-assignment-cleanup-policy.mjs inspect "$result_file")"
    stage='assignment_removal'
    for assignment_id in $deletion_plan; do
      run_azure az role assignment delete --ids "$scope/providers/Microsoft.Authorization/roleAssignments/$assignment_id" --only-show-errors
    done
    stage='residue_verification'
    run_azure az role assignment list --subscription "$APPROVED_SUBSCRIPTION_ID" --assignee-object-id b77b6201-ad26-4f77-8f88-6d0d43f7dbb8 \
      --include-inherited --all --only-show-errors --output json >"$result_file"
    node .github/scripts/foundation-assignment-cleanup-policy.mjs residue "$result_file" >/dev/null
    printf '{"classification":"access_removed","assignmentCount":0,"assignmentTimestampUtc":"%s","authorityDeadlineUtc":"%s"}\n' \
      "$assignment_timestamp_utc" "$authority_deadline_utc" >"$evidence_file"
    stage='complete'; classification='complete'; write_state 0; exit 0
    ;;
  *) exit 2 ;;
esac

stage='deployment_validation'
if [ "$mode" = 'assignment' ]; then node .github/scripts/foundation-authority-window.mjs active "$assignment_timestamp_utc" "$authority_deadline_utc" >/dev/null; fi
run_azure az deployment group validate --subscription "$APPROVED_SUBSCRIPTION_ID" --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" --template-file "$template" "${parameters[@]}" --only-show-errors --output none
stage='what_if'
if [ "$mode" = 'assignment' ]; then node .github/scripts/foundation-authority-window.mjs active "$assignment_timestamp_utc" "$authority_deadline_utc" >/dev/null; fi
run_azure az deployment group what-if --subscription "$APPROVED_SUBSCRIPTION_ID" --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" --template-file "$template" "${parameters[@]}" --result-format FullResourcePayloads \
  --no-pretty-print --only-show-errors --output json >"$what_if_file"
set +e
node .github/scripts/rbac-boundary-policy.mjs "$mode" "$what_if_file" >"$policy_file" 2>"$error_file"
policy_exit="$?"
set -e
classification="$(POLICY_FILE="$policy_file" python3 -c 'import json,os; print(json.load(open(os.environ["POLICY_FILE"], encoding="utf-8"))["classification"])')"
test "$policy_exit" -eq 0
stage='deployment'
classification='deployment_failed'
if [ "$mode" = 'assignment' ]; then node .github/scripts/foundation-authority-window.mjs active "$assignment_timestamp_utc" "$authority_deadline_utc" >/dev/null; fi
run_azure az deployment group create --subscription "$APPROVED_SUBSCRIPTION_ID" --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" --template-file "$template" "${parameters[@]}" --only-show-errors --output none
stage='deployment_readback'
if [ "$mode" = 'assignment' ]; then node .github/scripts/foundation-authority-window.mjs active "$assignment_timestamp_utc" "$authority_deadline_utc" >/dev/null; fi
if [ "$mode" = 'bootstrap' ]; then
  run_azure az role definition list --subscription "$APPROVED_SUBSCRIPTION_ID" --name 4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54 --output json >"$result_file"
  run_azure az role definition list --subscription "$APPROVED_SUBSCRIPTION_ID" --name 9df6bf68-4db7-4d38-b7f1-7bb26a541199 --output json >"$second_result_file"
  FIRST_RESULT="$result_file" SECOND_RESULT="$second_result_file" python3 - <<'PY'
import json, os
scope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev'
expected = [
    ('4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54', 'AdventuresSuite Migration Infrastructure Deployer'),
    ('9df6bf68-4db7-4d38-b7f1-7bb26a541199', 'AdventuresSuite Migration Identity Reader')]
for path, (role_id, role_name) in zip([os.environ['FIRST_RESULT'], os.environ['SECOND_RESULT']], expected):
    values = json.load(open(path, encoding='utf-8'))
    if len(values) != 1:
        raise SystemExit(1)
    role = values[0]
    if role.get('name', '').lower() != role_id or role.get('roleName') != role_name or role.get('assignableScopes') != [scope]:
        raise SystemExit(1)
    permissions = role.get('permissions') or []
    if len(permissions) != 1 or any('*' in action for action in permissions[0].get('actions', [])):
        raise SystemExit(1)
PY
else
  run_azure az role assignment show --ids "$infra_assignment" --output json >"$result_file"
  run_azure az role assignment show --ids "$reader_assignment" --output json >"$second_result_file"
  FIRST_RESULT="$result_file" SECOND_RESULT="$second_result_file" python3 - <<'PY'
import json, os
scope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev'
principal = 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8'
roles = ['4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54', '9df6bf68-4db7-4d38-b7f1-7bb26a541199']
for path, role in zip([os.environ['FIRST_RESULT'], os.environ['SECOND_RESULT']], roles):
    value = json.load(open(path, encoding='utf-8'))
    if value.get('principalId', '').lower() != principal or value.get('scope', '').lower() != scope.lower() or value.get('roleDefinitionId', '').rsplit('/', 1)[-1].lower() != role:
        raise SystemExit(1)
PY
fi
printf '{"classification":"%s_complete"}\n' "$mode" >"$evidence_file"
stage='complete'; classification='complete'; write_state 0
