#!/usr/bin/env bash
set -euo pipefail
umask 077

if [ "$#" -ne 3 ]; then
  exit 2
fi

environment_name="$1"
error_prefix="$2"
state_file="$3"
stage='arm_token_acquisition'
classification='operation_failed'
read_error="${error_prefix}-read.err"
write_error="${error_prefix}-write.err"
token_response="${error_prefix}-token.json"
token_error="${error_prefix}-token.err"

write_state() {
  printf 'stage=%s\nclassification=%s\nexit_code=%s\n' \
    "$stage" "$classification" "$1" > "$state_file"
}

cleanup() {
  original_exit="$?"
  unset ARM_TOKEN_RESPONSE_FILE
  rm -f "$token_response" "$token_error" "$read_error" "$write_error"
  if [ "$original_exit" -ne 0 ]; then
    write_state "$original_exit"
  fi
  exit "$original_exit"
}
trap cleanup EXIT

case "$environment_name" in
  migration-foundation-deployment|migration-rbac-deployment) ;;
  *) exit 2 ;;
esac

if ! az account get-access-token \
  --tenant "$APPROVED_TENANT_ID" \
  --resource-type arm \
  --output json >"$token_response" 2>"$token_error"; then
  exit 1
fi
ARM_TOKEN_RESPONSE_FILE="$token_response"
export ARM_TOKEN_RESPONSE_FILE

stage='arm_token_claim_validation'
set +e
python3 - <<'PY'
import base64
import json
import os

try:
    response = json.loads(open(os.environ['ARM_TOKEN_RESPONSE_FILE'], encoding='utf-8').read())
    token = response['accessToken']
except Exception:
    raise SystemExit(45)
if not isinstance(token, str):
    raise SystemExit(45)
parts = token.split('.')
if len(parts) != 3:
    raise SystemExit(45)
payload = parts[1] + '=' * (-len(parts[1]) % 4)
try:
    claims = json.loads(base64.urlsafe_b64decode(payload))
except Exception:
    raise SystemExit(45)
if claims.get('tid') != os.environ['APPROVED_TENANT_ID']:
    raise SystemExit(41)
if claims.get('oid') != os.environ['EXPECTED_PRINCIPAL_ID']:
    raise SystemExit(42)
if claims.get('appid', claims.get('azp')) != os.environ['AZURE_CLIENT_ID']:
    raise SystemExit(43)
if claims.get('aud') not in ('https://management.azure.com', 'https://management.azure.com/'):
    raise SystemExit(44)
PY
claim_exit="$?"
set -e
unset ARM_TOKEN_RESPONSE_FILE
case "$claim_exit" in
  0) ;;
  41) classification='claim_mismatch_tid'; exit 1 ;;
  42) classification='claim_mismatch_oid'; exit 1 ;;
  43) classification='claim_mismatch_client_id'; exit 1 ;;
  44) classification='claim_mismatch_aud'; exit 1 ;;
  *) classification='malformed_token'; exit 1 ;;
esac

stage='resource_read_probe'
set +e
az rest --method get \
  --url "https://management.azure.com/subscriptions/$APPROVED_SUBSCRIPTION_ID/resourceGroups/$TARGET_RESOURCE_GROUP?api-version=2024-03-01" \
  --only-show-errors >/dev/null 2>"$read_error"
read_exit="$?"
set -e

stage='resource_read_denial_classification'
set +e
classification="$(.github/scripts/require-arm-authorization-denial.sh "$read_error" "$read_exit")"
classification_exit="$?"
set -e
if [ "$classification_exit" -ne 0 ]; then
  exit 1
fi

stage='deployment_validation_probe'
set +e
az rest --method post \
  --url "https://management.azure.com/subscriptions/$APPROVED_SUBSCRIPTION_ID/resourceGroups/$TARGET_RESOURCE_GROUP/providers/Microsoft.Resources/deployments/federation-proof/validate?api-version=2022-09-01" \
  --body '{"properties":{"mode":"Incremental","template":{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[]}}}' \
  --only-show-errors >/dev/null 2>"$write_error"
write_exit="$?"
set -e

stage='deployment_validation_denial_classification'
set +e
classification="$(.github/scripts/require-arm-authorization-denial.sh "$write_error" "$write_exit")"
classification_exit="$?"
set -e
if [ "$classification_exit" -ne 0 ]; then
  exit 1
fi

stage='complete'
classification='complete'
write_state 0
