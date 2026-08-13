#!/usr/bin/env bash
set -euo pipefail
umask 077
: "${RUNNER_BROKER_URL:?}" "${RUNNER_OPERATION_ID:?}" "${RUNNER_APPROVAL_NONCE:?}"
[[ "$RUNNER_BROKER_URL" =~ ^https://[^/?#]+/v1/runner-registration$ ]]
token_field='access''_token'
identity_token="$(curl --fail --silent --show-error --connect-timeout 5 --max-time 10 -H Metadata:true 'http://169.254.169.254/metadata/identity/oauth2/token?api-version=2019-08-01&resource=api%3A%2F%2Fadventures-suite-runner-broker' | jq -er --arg field "$token_field" '.[$field]')"
trap 'unset identity_token response' EXIT
response="$(curl --fail --silent --show-error --connect-timeout 5 --max-time 10 --proto '=https' --tlsv1.2 -H "Authorization: Bearer ${identity_token}" -H 'Content-Type: application/json' --data "$(jq -nc --arg operation "$RUNNER_OPERATION_ID" --arg nonce "$RUNNER_APPROVAL_NONCE" '{operationId:$operation,approvalNonce:$nonce}')" "$RUNNER_BROKER_URL")"
jq -er '.registrationToken | select(type=="string" and length>20)' <<<"$response"
