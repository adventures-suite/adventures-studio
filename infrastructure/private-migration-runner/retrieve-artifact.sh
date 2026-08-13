#!/usr/bin/env bash
set -euo pipefail
umask 077
: "${ARTIFACT_ID:?}" "${PACKAGE_RUN_ID:?}" "${EXPECTED_SOURCE_SHA:?}" "${WORK_DIRECTORY:?}"
[[ "$ARTIFACT_ID" =~ ^[1-9][0-9]*$ ]]; [[ "$PACKAGE_RUN_ID" =~ ^[1-9][0-9]*$ ]]; [[ "$EXPECTED_SOURCE_SHA" =~ ^[0-9a-f]{40}$ ]]
install -d -m 0700 "$WORK_DIRECTORY"
metadata="$(gh api "repos/adventures-suite/adventures-studio/actions/artifacts/${ARTIFACT_ID}")"
jq -e --arg id "$ARTIFACT_ID" --arg run "$PACKAGE_RUN_ID" --arg sha "$EXPECTED_SOURCE_SHA" '.id|tostring==$id' <<<"$metadata" >/dev/null
jq -e --arg run "$PACKAGE_RUN_ID" --arg sha "$EXPECTED_SOURCE_SHA" '.expired==false and (.workflow_run.id|tostring)==$run and .workflow_run.head_sha==$sha and .workflow_run.repository_id==1317655952' <<<"$metadata" >/dev/null
gh api -H 'Accept: application/vnd.github+json' "repos/adventures-suite/adventures-studio/actions/artifacts/${ARTIFACT_ID}/zip" >"$WORK_DIRECTORY/artifact.zip"
unzip -q "$WORK_DIRECTORY/artifact.zip" -d "$WORK_DIRECTORY/artifact"
rm -f "$WORK_DIRECTORY/artifact.zip"
