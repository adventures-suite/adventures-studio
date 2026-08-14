#!/usr/bin/env bash
set -euo pipefail
umask 077
: "${RUNNER_OPERATION_ID:?}" "${RUNNER_REGISTRATION_URL:?}" "${RUNNER_BROKER_URL:?}" "${RUNNER_BROKER_HOST:?}" "${RUNNER_APPROVAL_NONCE:?}" "${RUNNER_DEADLINE_EPOCH:?}"
[[ "$RUNNER_BROKER_URL" == "https://${RUNNER_BROKER_HOST}/v1/runner-registration" ]]
[[ "$RUNNER_OPERATION_ID" =~ ^[a-z0-9][a-z0-9-]{7,31}$ ]]
now="$(date +%s)"; (( RUNNER_DEADLINE_EPOCH > now && RUNNER_DEADLINE_EPOCH <= now + 2700 ))
work_dir="/var/lib/adventures-suite-runner/${RUNNER_OPERATION_ID}"; install -d -m 0700 -o runner -g runner "$work_dir"
trap 'unset RUNNER_REGISTRATION_TOKEN RUNNER_PACKAGE_URL; find "$work_dir" -mindepth 1 -delete' EXIT HUP INT TERM
"$work_dir/install-reviewed-egress-policy.sh"
RUNNER_REGISTRATION_TOKEN="$("$work_dir/acquire-registration.sh")"
export RUNNER_REGISTRATION_TOKEN
timeout_seconds=$((RUNNER_DEADLINE_EPOCH - now))
timeout --signal=TERM --kill-after=30s "${timeout_seconds}s" "$work_dir/config.sh" --unattended --ephemeral --disableupdate --url "$RUNNER_REGISTRATION_URL" --token "$RUNNER_REGISTRATION_TOKEN" --name "migration-${RUNNER_OPERATION_ID}" --labels "migration-${RUNNER_OPERATION_ID}" --work "$work_dir/work" --replace
unset RUNNER_REGISTRATION_TOKEN
timeout --signal=TERM --kill-after=30s "${timeout_seconds}s" "$work_dir/run.sh"
