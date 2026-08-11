#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

if [ "$#" -ne 7 ]; then exit 2; fi
release_sha="$1"
approval_id="$2"
expected_template_sha="$3"
expected_parameters_sha="$4"
state_file="$5"
evidence_file="$6"
temporary_prefix="$7"
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
stage='artifact_validation'
classification='operation_failed'

write_state() {
  printf 'stage=%s\nclassification=%s\nexit_code=%s\n' "$stage" "$classification" "$1" >"$state_file"
}
cleanup() {
  original_exit="$?"
  rm -f "$compiled" "$resource_group_readback" "$vnet_readback" "$resource_readback" \
    "$validation" "$what_if" "$policy_result" "$deployment" "$deployment_readback" "$command_error"
  if [ "$original_exit" -ne 0 ]; then write_state "$original_exit"; fi
  exit "$original_exit"
}
trap cleanup EXIT

[[ "$release_sha" =~ ^[0-9a-f]{40}$ ]] || exit 2
[[ "$approval_id" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{7,127}$ ]] || exit 2
[[ "$expected_template_sha" =~ ^[0-9a-f]{64}$ ]] || exit 2
[[ "$expected_parameters_sha" =~ ^[0-9a-f]{64}$ ]] || exit 2
test "$APPROVED_SUBSCRIPTION_ID" = '5ace9cdd-06d1-47d9-8214-1e7c756d076a'
test "$TARGET_RESOURCE_GROUP" = 'rg-adventures-suite-dev'
test "$AZURE_CLIENT_ID" = '223af00d-69e5-4302-9ac5-6b338f3ea2e5'
test "$EXPECTED_PRINCIPAL_ID" = 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8'
test "$(sha256sum "$template" | awk '{print $1}')" = "$expected_template_sha"
test "$(sha256sum "$parameters" | awk '{print $1}')" = "$expected_parameters_sha"

stage='read_only_preflight'
az group show --subscription "$APPROVED_SUBSCRIPTION_ID" --name "$TARGET_RESOURCE_GROUP" \
  --only-show-errors --output json >"$resource_group_readback" 2>"$command_error"
az network vnet show --subscription "$APPROVED_SUBSCRIPTION_ID" --resource-group "$TARGET_RESOURCE_GROUP" \
  --name vnet-adventures-suite-dev --only-show-errors --output json >"$vnet_readback" 2>"$command_error"
az resource list --subscription "$APPROVED_SUBSCRIPTION_ID" --resource-group "$TARGET_RESOURCE_GROUP" \
  --only-show-errors --output json >"$resource_readback" 2>"$command_error"
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

stage='bicep_build'
az bicep build --file "$template" --outfile "$compiled" >/dev/null 2>"$command_error"

stage='deployment_validation'
deployment_name="migration-foundation-${release_sha:0:12}"
az deployment group validate \
  --subscription "$APPROVED_SUBSCRIPTION_ID" \
  --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" \
  --template-file "$template" \
  --parameters "$parameters" \
  --only-show-errors --output json >"$validation" 2>"$command_error"
VALIDATION_FILE="$validation" python3 - <<'PY'
import json, os
document = json.load(open(os.environ['VALIDATION_FILE'], encoding='utf-8'))
if document.get('properties', {}).get('provisioningState') != 'Succeeded':
    raise SystemExit(1)
PY

stage='what_if'
az deployment group what-if \
  --subscription "$APPROVED_SUBSCRIPTION_ID" \
  --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" \
  --template-file "$template" \
  --parameters "$parameters" \
  --result-format FullResourcePayloads \
  --no-pretty-print --only-show-errors --output json >"$what_if" 2>"$command_error"
set +e
node .github/scripts/foundation-deployment-policy.mjs what-if "$what_if" >"$policy_result" 2>"$command_error"
policy_exit="$?"
set -e
classification="$(POLICY_FILE="$policy_result" python3 -c 'import json,os; print(json.load(open(os.environ["POLICY_FILE"], encoding="utf-8"))["classification"])')"
test "$policy_exit" -eq 0 && test "$classification" = 'what_if_approved'

stage='deployment'
classification='deployment_failed'
az deployment group create \
  --subscription "$APPROVED_SUBSCRIPTION_ID" \
  --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" \
  --template-file "$template" \
  --parameters "$parameters" \
  --only-show-errors --output json >"$deployment" 2>"$command_error"

stage='deployment_readback'
az deployment group show \
  --subscription "$APPROVED_SUBSCRIPTION_ID" \
  --resource-group "$TARGET_RESOURCE_GROUP" \
  --name "$deployment_name" \
  --only-show-errors --output json >"$deployment_readback" 2>"$command_error"
node .github/scripts/foundation-deployment-policy.mjs deployment "$deployment_readback" >"$evidence_file"

stage='complete'
classification='complete'
write_state 0
