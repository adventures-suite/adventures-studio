#!/usr/bin/env bash
set -uo pipefail

case "${1:-}" in
  --verify-execution-channel|--capture-migration-state|--run-reviewed-operation|--verify-migration-state)
    ;;
  *)
    echo '{"eventName":"migration-job-rejected","reason":"unreviewed-entrypoint"}'
    exit 2
    ;;
esac

started_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf '{"eventName":"migration-job-started","startedAt":"%s"}\n' "$started_at"
set +e
timeout --signal=TERM --kill-after=30s 900s \
  /app/AdventuresSuite.DatabaseMigrator "$1"
exit_code=$?
set -e
completed_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf '{"eventName":"migration-job-process-exited","completedAt":"%s","exitCode":%s}\n' \
  "$completed_at" "$exit_code"
exit "$exit_code"
