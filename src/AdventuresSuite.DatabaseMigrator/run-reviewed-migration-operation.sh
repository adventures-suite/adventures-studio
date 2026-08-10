#!/usr/bin/env bash
set -uo pipefail

operation_id="${ADVENTURESSUITE_MIGRATION_OPERATION_ID:-unavailable}"
timeout_seconds="${ADVENTURESSUITE_MIGRATION_TIMEOUT_SECONDS:-900}"
if ! [[ "$operation_id" =~ ^[a-z0-9][a-z0-9-]{7,63}$ ]]; then
  echo '{"eventName":"migration-wrapper-rejected","reason":"invalid-operation-id"}'
  exit 2
fi
if ! [[ "$timeout_seconds" =~ ^[0-9]+$ ]] || (( timeout_seconds < 60 || timeout_seconds > 1800 )); then
  echo '{"eventName":"migration-wrapper-rejected","reason":"invalid-timeout"}'
  exit 2
fi

started_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf '{"eventName":"migration-wrapper-started","operationId":"%s","startedAt":"%s","timeoutSeconds":%s}\n' \
  "$operation_id" "$started_at" "$timeout_seconds"

exit_code=125
on_exit() {
  original_exit_code="$exit_code"
  completed_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  printf '{"eventName":"migration-wrapper-finished","operationId":"%s","completedAt":"%s","exitCode":%s}\n' \
    "$operation_id" "$completed_at" "$original_exit_code"
  exit "$original_exit_code"
}
trap on_exit EXIT HUP INT TERM

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
timeout --signal=TERM --kill-after=30s "${timeout_seconds}s" \
  "$script_dir/AdventuresSuite.DatabaseMigrator" --run-reviewed-operation
exit_code=$?
