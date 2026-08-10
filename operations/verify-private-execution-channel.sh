#!/usr/bin/env bash
set -uo pipefail

# Managed Run Command appends protectedParameters after ordinary parameters.
# Preserve this exact order when creating the run-command resource.
operation_id="${1:-}"
release_sha="${2:-}"
artifact_checksum="${3:-}"
site_resource_id="${4:-}"
scm_host="${5:-}"
artifact_url="${6:-}"
container_sas_uri="${7:-}"
arm_token="${8:-}"
completion_key="${9:-}"

work_dir="/var/tmp/adventures-suite-channel-${operation_id}"
remote_dir="/home/data/adventures-suite-channel-${operation_id}"
package_name="database-migrator.tar.gz"
evidence_name="execution-channel-evidence.json"
envelope_name="execution-channel-completion.json"
cleanup() {
  original_exit_code=$?
  rm -rf -- "$work_dir"
  exit "$original_exit_code"
}
trap cleanup EXIT HUP INT TERM

if ! [[ "$operation_id" =~ ^[a-z0-9][a-z0-9-]{7,63}$ ]] \
  || ! [[ "$release_sha" =~ ^[0-9a-f]{40}$ ]] \
  || ! [[ "$artifact_checksum" =~ ^[0-9a-f]{64}$ ]] \
  || [[ "$scm_host" != *.scm.azurewebsites.net ]] \
  || [[ "$site_resource_id" != /subscriptions/*/providers/Microsoft.Web/sites/* ]]; then
  echo '{"eventName":"managed-channel-proof-rejected"}'
  exit 2
fi

umask 077
mkdir -p "$work_dir"
archive="$work_dir/artifact.zip"
package="$work_dir/$package_name"
downloaded="$work_dir/downloaded-$package_name"
key_file="$work_dir/completion.key"
printf '%s' "$completion_key" >"$key_file"

curl --fail --silent --show-error --location --max-time 120 \
  --header 'Accept: application/vnd.github+json' "$artifact_url" --output "$archive"
unzip -q "$archive" -d "$work_dir/artifact"
found_package="$(find "$work_dir/artifact" -type f -name "adventures-suite-database-migrator-${release_sha}-*.tar.gz" -print -quit)"
[[ -n "$found_package" ]] || { echo '{"eventName":"managed-channel-proof-failed","reason":"package-not-found"}'; exit 3; }
cp "$found_package" "$package"
printf '%s  %s\n' "$artifact_checksum" "$package" | sha256sum -c - >/dev/null

container_base="${container_sas_uri%%\?*}"
container_query="${container_sas_uri#*\?}"
package_blob_uri="$container_base/$operation_id-$package_name?$container_query"
evidence_blob_uri="$container_base/$operation_id-$evidence_name?$container_query"
envelope_blob_uri="$container_base/$operation_id-$envelope_name?$container_query"
curl --fail --silent --show-error --request PUT \
  --header 'x-ms-blob-type: BlockBlob' --header 'x-ms-version: 2023-11-03' \
  --upload-file "$package" "$package_blob_uri"
rm -f "$package"
curl --fail --silent --show-error --location "$package_blob_uri" --output "$downloaded"
printf '%s  %s\n' "$artifact_checksum" "$downloaded" | sha256sum -c - >/dev/null

credentials="$work_dir/scm-credentials.json"
curl --fail --silent --show-error --request POST \
  --header "Authorization: Bearer $arm_token" --header 'Content-Length: 0' \
  "https://management.azure.com${site_resource_id}/config/publishingcredentials/list?api-version=2024-04-01" \
  --output "$credentials"
scm_user="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["properties"]["publishingUserName"])' "$credentials")"
scm_password="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["properties"]["publishingPassword"])' "$credentials")"
rm -f "$credentials"

scm_base="https://$scm_host"
curl --fail --silent --show-error --user "$scm_user:$scm_password" \
  --output /dev/null "$scm_base/api/settings"
curl --fail --silent --show-error --request PUT --user "$scm_user:$scm_password" \
  --header 'If-Match: *' --upload-file "$downloaded" \
  "$scm_base/api/vfs/${remote_dir#/}/$package_name"
curl --fail --silent --show-error --request PUT --user "$scm_user:$scm_password" \
  --header 'If-Match: *' --upload-file "$key_file" \
  "$scm_base/api/vfs/${remote_dir#/}/completion.key"

command_json="$work_dir/command.json"
python3 - "$command_json" "$remote_dir" "$operation_id" "$release_sha" "$artifact_checksum" <<'PY'
import json, sys
path, remote, operation, release, checksum = sys.argv[1:]
command = (
    f"mkdir -p {remote}/package && tar -xzf {remote}/database-migrator.tar.gz -C {remote}/package && "
    f"chmod 0755 {remote}/package/AdventuresSuite.DatabaseMigrator {remote}/package/run-execution-channel-proof.sh && "
    f"ADVENTURESSUITE_MIGRATION_OPERATION_ID={operation} "
    f"ADVENTURESSUITE_RELEASE_SHA={release} "
    f"ADVENTURESSUITE_ARTIFACT_SHA256={checksum} "
    f"ADVENTURESSUITE_EXECUTION_EVIDENCE_FILE={remote}/execution-channel-evidence.json "
    f"ADVENTURESSUITE_COMPLETION_SIGNING_KEY_FILE={remote}/completion.key "
    f"{remote}/package/run-execution-channel-proof.sh"
)
with open(path, "w") as output:
    json.dump({"command": command, "dir": remote}, output, separators=(",", ":"))
PY

command_result="$work_dir/command-result.json"
curl --fail --silent --show-error --request POST --user "$scm_user:$scm_password" \
  --header 'Content-Type: application/json' --data-binary "@$command_json" \
  "$scm_base/api/command" --output "$command_result"
command_exit="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1])).get("ExitCode",-1))' "$command_result")"
[[ "$command_exit" == 0 ]] || { echo '{"eventName":"managed-channel-proof-failed","reason":"scm-command-failed"}'; exit 4; }

evidence="$work_dir/$evidence_name"
envelope="$work_dir/$envelope_name"
curl --fail --silent --show-error --user "$scm_user:$scm_password" \
  "$scm_base/api/vfs/${remote_dir#/}/$evidence_name" --output "$evidence"
curl --fail --silent --show-error --user "$scm_user:$scm_password" \
  "$scm_base/api/vfs/${remote_dir#/}/$evidence_name.envelope" --output "$envelope"
curl --fail --silent --show-error --request PUT \
  --header 'x-ms-blob-type: BlockBlob' --header 'x-ms-version: 2023-11-03' \
  --upload-file "$evidence" "$evidence_blob_uri"
curl --fail --silent --show-error --request PUT \
  --header 'x-ms-blob-type: BlockBlob' --header 'x-ms-version: 2023-11-03' \
  --upload-file "$envelope" "$envelope_blob_uri"
evidence_checksum="$(sha256sum "$evidence" | cut -d ' ' -f 1)"
envelope_checksum="$(sha256sum "$envelope" | cut -d ' ' -f 1)"
rm -f "$evidence" "$envelope"
curl --fail --silent --show-error --location "$evidence_blob_uri" --output "$evidence"
curl --fail --silent --show-error --location "$envelope_blob_uri" --output "$envelope"
[[ "$(sha256sum "$evidence" | cut -d ' ' -f 1)" == "$evidence_checksum" ]]
[[ "$(sha256sum "$envelope" | cut -d ' ' -f 1)" == "$envelope_checksum" ]]
cat "$envelope"

cleanup_json="$work_dir/cleanup.json"
printf '{"command":"rm -rf %s","dir":"/home/data"}' "$remote_dir" >"$cleanup_json"
curl --fail --silent --show-error --request POST --user "$scm_user:$scm_password" \
  --header 'Content-Type: application/json' --data-binary "@$cleanup_json" \
  "$scm_base/api/command" --output /dev/null
curl --fail --silent --show-error --request DELETE \
  --header 'x-ms-version: 2023-11-03' "$package_blob_uri"

echo '{"eventName":"managed-channel-cleanup-complete"}'
