#!/usr/bin/env bash
set -uo pipefail

operation_id="${ADVENTURESSUITE_MIGRATION_OPERATION_ID:-}"
release_sha="${ADVENTURESSUITE_RELEASE_SHA:-}"
artifact_checksum="${ADVENTURESSUITE_ARTIFACT_SHA256:-}"
evidence_file="${ADVENTURESSUITE_EXECUTION_EVIDENCE_FILE:-}"
signing_key_file="${ADVENTURESSUITE_COMPLETION_SIGNING_KEY_FILE:-}"

if ! [[ "$operation_id" =~ ^[a-z0-9][a-z0-9-]{7,63}$ ]] \
  || ! [[ "$release_sha" =~ ^[0-9a-f]{40}$ ]] \
  || ! [[ "$artifact_checksum" =~ ^[0-9a-f]{64}$ ]] \
  || [[ -z "$evidence_file" || -z "$signing_key_file" ]] \
  || [[ ! -f "$signing_key_file" ]]; then
  echo '{"eventName":"execution-channel-proof-rejected"}'
  exit 2
fi

umask 077
started_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
envelope_file="${evidence_file}.envelope"
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

"$script_dir/AdventuresSuite.DatabaseMigrator" --verify-execution-channel \
  >"$evidence_file" 2>&1
process_exit_code=$?
completed_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
payload_checksum="$(sha256sum "$evidence_file" | cut -d ' ' -f 1)"
classification="Complete"
if (( process_exit_code != 0 )); then classification="Failed"; fi

printf '{"operationId":"%s","releaseSha":"%s","artifactChecksum":"%s","processStartedAt":"%s","processCompletedAt":"%s","exitCode":%s,"classification":"%s","evidenceFileChecksum":"%s"}' \
  "$operation_id" "$release_sha" "$artifact_checksum" "$started_at" "$completed_at" \
  "$process_exit_code" "$classification" "$payload_checksum" >"$envelope_file"

signature="$(openssl dgst -sha256 -hmac "$(<"$signing_key_file")" -binary "$envelope_file" \
  | base64 | tr -d '\n')"
{
  printf '{"eventName":"execution-channel-completion","envelope":'
  tr -d '\n' <"$envelope_file"
  printf ',"signature":"%s"}\n' "$signature"
} | tee "${evidence_file}.envelope"

rm -f "$signing_key_file"
exit "$process_exit_code"
