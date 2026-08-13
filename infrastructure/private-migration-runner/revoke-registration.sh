#!/usr/bin/env bash
set -euo pipefail
umask 077
: "${RUNNER_BROKER_URL:?}" "${RUNNER_OPERATION_ID:?}" "${CLEANUP_OIDC_TOKEN:?}"
[[ "$RUNNER_BROKER_URL" =~ ^https://[^/?#]+/v1/runner-registration$ ]]
curl --fail --silent --show-error --connect-timeout 5 --max-time 10 --proto '=https' --tlsv1.2 \
  -X DELETE -H "Authorization: Bearer ${CLEANUP_OIDC_TOKEN}" -H 'Content-Type: application/json' \
  --data "$(jq -nc --arg operation "$RUNNER_OPERATION_ID" '{operationId:$operation}')" "$RUNNER_BROKER_URL" >/dev/null
