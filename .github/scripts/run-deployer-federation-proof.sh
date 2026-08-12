#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

if [ "$#" -lt 3 ] || [ "$#" -gt 4 ]; then
  exit 2
fi

environment_name="$1"
error_prefix="$2"
state_file="$3"
proof_mode="${4:-denial}"
stage='arm_token_acquisition'
classification='operation_failed'
token_response="${error_prefix}-token.json"
token_error="${error_prefix}-token.err"
audience_response="${error_prefix}-audience.txt"
authorization_config="${error_prefix}-authorization.conf"
request_body="${error_prefix}-request.json"
read_prefix="${error_prefix}-read"
write_prefix="${error_prefix}-write"

write_state() {
  printf 'stage=%s\nclassification=%s\nexit_code=%s\n' \
    "$stage" "$classification" "$1" > "$state_file"
}

cleanup() {
  original_exit="$?"
  unset ARM_TOKEN_RESPONSE_FILE EXPECTED_ARM_AUDIENCE_FILE ARM_AUTHORIZATION_CONFIG_FILE
  rm -f \
    "$token_response" "$token_error" "$audience_response" \
    "$authorization_config" "$request_body" \
    "${read_prefix}.body" "${read_prefix}.status" "${read_prefix}.transport.err" \
    "${write_prefix}.body" "${write_prefix}.status" "${write_prefix}.transport.err"
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
case "$proof_mode" in
  denial|identity-only) ;;
  *) exit 2 ;;
esac
test "${APPROVED_TENANT_ID:-}" = 'd7add2bb-ac03-49a8-9377-d0bf6a012f2f'
test "${APPROVED_SUBSCRIPTION_ID:-}" = '5ace9cdd-06d1-47d9-8214-1e7c756d076a'
test "${TARGET_RESOURCE_GROUP:-}" = 'rg-adventures-suite-dev'
case "$environment_name" in
  migration-foundation-deployment)
    test "${AZURE_CLIENT_ID:-}" = '223af00d-69e5-4302-9ac5-6b338f3ea2e5'
    test "${EXPECTED_PRINCIPAL_ID:-}" = 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8'
    ;;
  migration-rbac-deployment)
    test "${AZURE_CLIENT_ID:-}" = 'd678e2ad-ada2-4cde-bb79-44630acf1cc8'
    test "${EXPECTED_PRINCIPAL_ID:-}" = '822c1c0c-39e1-400f-b9fc-9532a11bae5d'
    ;;
esac

if ! az cloud show \
  --query endpoints.activeDirectoryResourceId \
  --output tsv >"$audience_response" 2>"$token_error"; then
  exit 1
fi

if ! az account get-access-token \
  --tenant "$APPROVED_TENANT_ID" \
  --resource-type arm \
  --output json >"$token_response" 2>"$token_error"; then
  exit 1
fi
ARM_TOKEN_RESPONSE_FILE="$token_response"
EXPECTED_ARM_AUDIENCE_FILE="$audience_response"
ARM_AUTHORIZATION_CONFIG_FILE="$authorization_config"
export ARM_TOKEN_RESPONSE_FILE EXPECTED_ARM_AUDIENCE_FILE ARM_AUTHORIZATION_CONFIG_FILE

stage='arm_token_claim_validation'
set +e
python3 - <<'PY'
import base64
import json
import os
import re

try:
    response = json.loads(open(os.environ['ARM_TOKEN_RESPONSE_FILE'], encoding='utf-8').read())
    token = response['accessToken']
    expected_audience = open(os.environ['EXPECTED_ARM_AUDIENCE_FILE'], encoding='utf-8').read().strip()
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
def normalize_audience(value):
    if not isinstance(value, str) or not value or value.endswith('//'):
        raise SystemExit(44)
    return value[:-1] if value.endswith('/') else value

if normalize_audience(claims.get('aud')) != normalize_audience(expected_audience):
    raise SystemExit(44)
if not re.fullmatch(r'[A-Za-z0-9._-]+', token):
    raise SystemExit(45)
with open(os.environ['ARM_AUTHORIZATION_CONFIG_FILE'], 'x', encoding='utf-8') as config:
    config.write(f'header = "Authorization: Bearer {token}"\n')
    config.write('header = "Accept: application/json"\n')
PY
claim_exit="$?"
set -e
unset ARM_TOKEN_RESPONSE_FILE EXPECTED_ARM_AUDIENCE_FILE ARM_AUTHORIZATION_CONFIG_FILE
case "$claim_exit" in
  0) ;;
  41) classification='claim_mismatch_tid'; exit 1 ;;
  42) classification='claim_mismatch_oid'; exit 1 ;;
  43) classification='claim_mismatch_client_id'; exit 1 ;;
  44) classification='claim_mismatch_aud'; exit 1 ;;
  *) classification='malformed_token'; exit 1 ;;
esac

if [ "$proof_mode" = 'identity-only' ]; then
  stage='complete'
  classification='identity_validated'
  write_state 0
  exit 0
fi

stage='resource_read_probe'
set +e
classification="$(.github/scripts/require-arm-authorization-denial.sh \
  "$authorization_config" \
  GET \
  "https://management.azure.com/subscriptions/$APPROVED_SUBSCRIPTION_ID/resourceGroups/$TARGET_RESOURCE_GROUP?api-version=2024-03-01" \
  '' \
  "$read_prefix")"
classification_exit="$?"
set -e
stage='resource_read_denial_classification'
if [ "$classification_exit" -ne 0 ]; then
  exit 1
fi

printf '%s\n' \
  '{"properties":{"mode":"Incremental","template":{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[]}}}' \
  >"$request_body"
stage='deployment_validation_probe'
set +e
classification="$(.github/scripts/require-arm-authorization-denial.sh \
  "$authorization_config" \
  POST \
  "https://management.azure.com/subscriptions/$APPROVED_SUBSCRIPTION_ID/resourceGroups/$TARGET_RESOURCE_GROUP/providers/Microsoft.Resources/deployments/federation-proof/validate?api-version=2022-09-01" \
  "$request_body" \
  "$write_prefix")"
classification_exit="$?"
set -e
stage='deployment_validation_denial_classification'
if [ "$classification_exit" -ne 0 ]; then
  exit 1
fi

stage='complete'
classification='complete'
write_state 0
