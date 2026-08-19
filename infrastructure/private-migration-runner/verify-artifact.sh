#!/usr/bin/env bash
set -euo pipefail
umask 077
: "${PACKAGE_PATH:?}" "${EVIDENCE_PATH:?}" "${EXPECTED_SOURCE_SHA:?}" "${EXPECTED_PACKAGE_SHA256:?}" "${EXPECTED_CATALOG_SHA256:?}" "${EXPECTED_RUN_ID:?}"
[[ "$EXPECTED_SOURCE_SHA" =~ ^[0-9a-f]{40}$ ]]; [[ "$EXPECTED_PACKAGE_SHA256" =~ ^[0-9a-f]{64}$ ]]; [[ "$EXPECTED_CATALOG_SHA256" =~ ^[0-9a-f]{64}$ ]]; [[ "$EXPECTED_RUN_ID" =~ ^[1-9][0-9]*$ ]]
actual_sha="$(sha256sum "$PACKAGE_PATH" | cut -d' ' -f1)"; test "$actual_sha" = "$EXPECTED_PACKAGE_SHA256"
jq -e --arg source "$EXPECTED_SOURCE_SHA" --arg package "$actual_sha" --arg catalog "$EXPECTED_CATALOG_SHA256" --arg run "$EXPECTED_RUN_ID" '.schemaVersion==1 and .sourceSha==$source and .packageSha256==$package and .orderedMigrationCatalogSha256==$catalog and .buildRunId==$run and .toolchain=={dotnetSdkVersion:"10.0.303",runtimeIdentifier:"linux-x64",selfContained:true} and (.dependencyLocks|length)==6 and .attestation.required==true' "$EVIDENCE_PATH" >/dev/null
gh attestation verify "$PACKAGE_PATH" --repo adventures-suite/adventures-studio --source-ref refs/heads/main --source-digest "$EXPECTED_SOURCE_SHA" >/dev/null
install -d -m 0700 verified; tar -xzf "$PACKAGE_PATH" -C verified
test -x verified/AdventuresSuite.DatabaseMigrator; test -x verified/run-reviewed-migration-operation.sh
