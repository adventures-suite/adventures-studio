#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

if [ "$#" -ne 10 ]; then exit 2; fi
release_sha="$1"
approval_id="$2"
expected_template_sha="$3"
expected_parameters_sha="$4"
state_file="$5"
evidence_file="$6"
temporary_prefix="$7"
expected_identity_catalog_sha="$8"
assignment_timestamp_utc="$9"
authority_deadline_utc="${10}"
template='infrastructure/container-apps-migrations/foundation-resources.bicep'
parameters='infrastructure/container-apps-migrations/foundation-resources.dev.bicepparam'
compiled="${temporary_prefix}.json"
resource_group_readback="${temporary_prefix}.resource-group.json"
vnet_readback="${temporary_prefix}.vnet.json"
resource_readback="${temporary_prefix}.resources.json"
validation="${temporary_prefix}.validation.json"
what_if="${temporary_prefix}.what-if.json"
policy_result="${temporary_prefix}.policy.json"
deployment="${temporary_prefix}.deployment.json"
deployment_readback="${temporary_prefix}.deployment-readback.json"
command_error="${temporary_prefix}.err"
identity_catalog='infrastructure/container-apps-migrations/foundation-identity-catalog.dev.json'
identity_readback="${temporary_prefix}.identities.json"
identity_job="${temporary_prefix}.identity-job.json"
identity_pull="${temporary_prefix}.identity-pull.json"
identity_publisher="${temporary_prefix}.identity-publisher.json"
identity_starter="${temporary_prefix}.identity-starter.json"
stage='artifact_validation'
classification='operation_failed'
azure_error_code=''
azure_error_limit=65536

write_state() {
  printf 'stage=%s\nclassification=%s\nazure_error_code=%s\nassignment_timestamp_utc=%s\nauthority_deadline_utc=%s\nexit_code=%s\n' \
    "$stage" "$classification" "$azure_error_code" "$assignment_timestamp_utc" "$authority_deadline_utc" "$1" >"$state_file"
}
run_azure() {
  local restore_errexit=false command_exit
  local -a parsed
  case "$-" in *e*) restore_errexit=true; set +e ;; esac
  "$@" 2> >( { head -c "$((azure_error_limit + 1))"; cat >/dev/null; } >"$command_error" )
  command_exit="$?"; wait
  if [ "$command_exit" -ne 0 ]; then
    mapfile -t parsed < <(python3 .github/scripts/classify-azure-error.py "$command_error")
    classification="${parsed[0]:-azure_error_unclassified}"
    azure_error_code="${parsed[1]:-}"
  fi
  if [ "$restore_errexit" = true ]; then set -e; fi
  return "$command_exit"
}
require_active() {
  node .github/scripts/foundation-authority-window.mjs active "$assignment_timestamp_utc" "$authority_deadline_utc" >/dev/null
}
cleanup() {
  original_exit="$?"
  rm -f "$compiled" "$resource_group_readback" "$vnet_readback" "$resource_readback" \
    "$validation" "$what_if" "$policy_result" "$deployment" "$deployment_readback" "$command_error" "$identity_readback" \
    "$identity_job" "$identity_pull" "$identity_publisher" "$identity_starter"
  if [ "$original_exit" -ne 0 ]; then write_state "$original_exit"; fi
  exit "$original_exit"
}
trap cleanup EXIT

[[ "$release_sha" =~ ^[0-9a-f]{40}$ ]] || exit 2
[[ "$approval_id" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{7,127}$ ]] || exit 2
[[ "$expected_template_sha" =~ ^[0-9a-f]{64}$ ]] || exit 2
[[ "$expected_parameters_sha" =~ ^[0-9a-f]{64}$ ]] || exit 2
[[ "$expected_identity_catalog_sha" =~ ^[0-9a-f]{64}$ ]] || exit 2
test "$APPROVED_SUBSCRIPTION_ID" = '5ace9cdd-06d1-47d9-8214-1e7c756d076a'
test "$TARGET_RESOURCE_GROUP" = 'rg-adventures-suite-dev'
test "$AZURE_CLIENT_ID" = '223af00d-69e5-4302-9ac5-6b338f3ea2e5'
test "$EXPECTED_PRINCIPAL_ID" = 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8'
test "$(sha256sum "$template" | awk '{print $1}')" = "$expected_template_sha"
test "$(sha256sum "$parameters" | awk '{print $1}')" = "$expected_parameters_sha"
test "$(sha256sum "$identity_catalog" | awk '{print $1}')" = "$expected_identity_catalog_sha"
require_active

stage='read_only_preflight'
run_azure az group show --subscription "$APPROVED_SUBSCRIPTION_ID" --name "$TARGET_RESOURCE_GROUP" \
  --only-show-errors --output json >"$resource_group_readback"
require_active
run_azure az network vnet show --subscription "$APPROVED_SUBSCRIPTION_ID" --resource-group "$TARGET_RESOURCE_GROUP" \
  --name vnet-adventures-suite-dev --only-show-errors --output json >"$vnet_readback"
require_active
run_azure az resource list --subscription "$APPROVED_SUBSCRIPTION_ID" --resource-group "$TARGET_RESOURCE_GROUP" \
  --only-show-errors --output json >"$resource_readback"
RESOURCE_GROUP_FILE="$resource_group_readback" VNET_FILE="$vnet_readback" RESOURCES_FILE="$resource_readback" python3 - <<'PY'
import ipaddress, json, os
group = json.load(open(os.environ['RESOURCE_GROUP_FILE'], encoding='utf-8'))
vnet = json.load(open(os.environ['VNET_FILE'], encoding='utf-8'))
resources = json.load(open(os.environ['RESOURCES_FILE'], encoding='utf-8'))
if group.get('name') != 'rg-adventures-suite-dev' or group.get('location', '').replace(' ', '').lower() != 'westus2':
    raise SystemExit(1)
if vnet.get('name') != 'vnet-adventures-suite-dev':
    raise SystemExit(1)
candidate = ipaddress.ip_network('10.40.3.0/27')
for subnet in vnet.get('subnets', []):
    for prefix in [subnet.get('addressPrefix'), *(subnet.get('addressPrefixes') or [])]:
        if prefix and candidate.overlaps(ipaddress.ip_network(prefix)):
            raise SystemExit(1)
reserved = {
    'snet-container-apps-migrations', 'log-adventures-suite-migrations-dev',
    'advsuitemigrationsdev', 'cae-adventures-suite-migrations-dev'
}
if any(item.get('name') in reserved for item in resources):
    raise SystemExit(1)
PY

for identity_spec in \
  "id-adventures-suite-migrate-job-dev:$identity_job" \
  "id-adventures-suite-migrate-pull-dev:$identity_pull" \
  "id-adventures-suite-migrate-publisher-dev:$identity_publisher" \
  "id-adventures-suite-migrate-starter-dev:$identity_starter"; do
  require_active
  identity_name="${identity_spec%%:*}"
  identity_file="${identity_spec#*:}"
  run_azure az identity show --subscription "$APPROVED_SUBSCRIPTION_ID" --resource-group "$TARGET_RESOURCE_GROUP" \
    --name "$identity_name" --only-show-errors --output json >"$identity_file"
done
IDENTITY_CATALOG="$identity_catalog" IDENTITY_READBACK="$identity_readback" IDENTITY_JOB="$identity_job" \
IDENTITY_PULL="$identity_pull" IDENTITY_PUBLISHER="$identity_publisher" IDENTITY_STARTER="$identity_starter" python3 - <<'PY'
import json, os
catalog = json.load(open(os.environ['IDENTITY_CATALOG'], encoding='utf-8'))
rows = [json.load(open(os.environ[name], encoding='utf-8')) for name in ['IDENTITY_JOB','IDENTITY_PULL','IDENTITY_PUBLISHER','IDENTITY_STARTER']]
expected_ids = [catalog['migrationIdentityResourceId'],catalog['pullIdentityResourceId'],catalog['publisherIdentityResourceId'],catalog['starterIdentityResourceId']]
if any(not isinstance(row.get('id'), str) or row['id'].lower() != expected.lower() for row, expected in zip(rows, expected_ids)):
    raise SystemExit(1)
if rows[0].get('principalId','').lower() != catalog['migrationIdentityPrincipalId'] or rows[0].get('clientId','').lower() != catalog['migrationIdentityClientId']:
    raise SystemExit(1)
json.dump([{'id':r.get('id'),'principalId':r.get('principalId'),'clientId':r.get('clientId')} for r in rows], open(os.environ['IDENTITY_READBACK'], 'w', encoding='utf-8'), separators=(',',':'))
PY

stage='bicep_build'
run_azure az bicep build --file "$template" --outfile "$compiled" >/dev/null

stage='deployment_validation'
deployment_name="migration-foundation-${release_sha:0:12}"
require_active
run_azure az deployment group validate \
  --subscription "$APPROVED_SUBSCRIPTION_ID" \
  --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" \
  --template-file "$template" \
  --parameters "$parameters" \
  --only-show-errors --output json >"$validation"
VALIDATION_FILE="$validation" python3 - <<'PY'
import json, os
document = json.load(open(os.environ['VALIDATION_FILE'], encoding='utf-8'))
if document.get('properties', {}).get('provisioningState') != 'Succeeded':
    raise SystemExit(1)
PY

stage='what_if'
require_active
run_azure az deployment group what-if \
  --subscription "$APPROVED_SUBSCRIPTION_ID" \
  --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" \
  --template-file "$template" \
  --parameters "$parameters" \
  --result-format FullResourcePayloads \
  --no-pretty-print --only-show-errors --output json >"$what_if"
set +e
node .github/scripts/foundation-deployment-policy.mjs what-if "$what_if" >"$policy_result" 2>"$command_error"
policy_exit="$?"
set -e
classification="$(POLICY_FILE="$policy_result" python3 -c 'import json,os; print(json.load(open(os.environ["POLICY_FILE"], encoding="utf-8"))["classification"])')"
test "$policy_exit" -eq 0 && test "$classification" = 'what_if_approved'

stage='deployment'
classification='deployment_failed'
require_active
run_azure az deployment group create \
  --subscription "$APPROVED_SUBSCRIPTION_ID" \
  --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" \
  --template-file "$template" \
  --parameters "$parameters" \
  --only-show-errors --output json >"$deployment"

stage='deployment_readback'
require_active
run_azure az deployment group show \
  --subscription "$APPROVED_SUBSCRIPTION_ID" \
  --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" \
  --only-show-errors --output json >"$deployment_readback"
node .github/scripts/foundation-deployment-policy.mjs deployment "$deployment_readback" "$identity_catalog" >"$evidence_file"

stage='complete'
classification='complete'
write_state 0
