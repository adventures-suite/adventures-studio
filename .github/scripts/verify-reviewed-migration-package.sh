#!/usr/bin/env bash
set -euo pipefail
umask 077
trap 'printf '\''{"classification":"migration_package_verification_failed"}\n'\'' >&2' ERR
: "${ARTIFACT_DIRECTORY:?}" "${EXPECTED_SOURCE_SHA:?}" "${EXPECTED_PACKAGE_SHA256:?}" \
  "${EXPECTED_CATALOG_SHA256:?}" "${EXPECTED_RUN_ID:?}" "${VERIFIED_DIRECTORY:?}"
mapfile -t packages < <(find "$ARTIFACT_DIRECTORY" -type f -name "adventures-suite-database-migrator-${EXPECTED_SOURCE_SHA}.tar.gz")
test "${#packages[@]}" -eq 1
package="${packages[0]}"
evidence="${package}.evidence.json"
test -f "$evidence"
actual_sha="$(sha256sum "$package" | cut -d' ' -f1)"
test "$actual_sha" = "$EXPECTED_PACKAGE_SHA256"
jq -e --arg source "$EXPECTED_SOURCE_SHA" --arg package "$actual_sha" \
  --arg catalog "$EXPECTED_CATALOG_SHA256" --arg run "$EXPECTED_RUN_ID" '
  .schemaVersion==1 and .sourceSha==$source and .packageSha256==$package
  and .orderedMigrationCatalogSha256==$catalog and (.buildRunId|tostring)==$run
  and .toolchain=={dotnetSdkVersion:"10.0.302",runtimeIdentifier:"linux-x64",selfContained:true}
  and (.dependencyLocks|length)==6 and .attestation.required==true
' "$evidence" >/dev/null
node .github/scripts/migration-package-evidence.mjs --verify-locks "$evidence" --root
gh attestation verify "$package" --repo adventures-suite/adventures-studio \
  --source-ref refs/heads/main --source-digest "$EXPECTED_SOURCE_SHA" >/dev/null 2>&1
install -d -m 0700 "$VERIFIED_DIRECTORY"
tar -xzf "$package" -C "$VERIFIED_DIRECTORY"
test -x "$VERIFIED_DIRECTORY/AdventuresSuite.DatabaseMigrator"
test -x "$VERIFIED_DIRECTORY/run-reviewed-migration-operation.sh"
printf '{"classification":"migration_package_verified","sourceSha":"%s","packageSha256":"%s","catalogSha256":"%s","buildRunId":"%s"}\n' \
  "$EXPECTED_SOURCE_SHA" "$actual_sha" "$EXPECTED_CATALOG_SHA256" "$EXPECTED_RUN_ID"
