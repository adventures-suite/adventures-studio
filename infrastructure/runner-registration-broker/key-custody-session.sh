#!/usr/bin/env bash
set -euo pipefail

fail(){ printf '%s\n' "$1" >&2; exit 1; }
operation_id="${1:-}"
action="${2:-}"
[[ "$operation_id" =~ ^broker-key-[a-z0-9]{16,64}$ ]] || fail operation-id-binding

mount_root='/Volumes'
importer="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/import-app-key.mjs"
if [[ "${KEY_CUSTODY_TEST_MODE:-}" == fictional ]]; then
  [[ "${KEY_CUSTODY_TEST_MOUNT_ROOT:-}" == /tmp/* || "${KEY_CUSTODY_TEST_MOUNT_ROOT:-}" == /private/tmp/* ]] || fail test-root-binding
  mount_root="$KEY_CUSTODY_TEST_MOUNT_ROOT"
elif [[ -n "${KEY_CUSTODY_TEST_MODE:-}${KEY_CUSTODY_TEST_MOUNT_ROOT:-}" ]]; then
  fail test-override-denied
fi
mount_path="${mount_root}/adventures-suite-key-custody-${operation_id}"

overwrite_source(){
  local path="${1:-}"
  [[ -f "$path" && ! -L "$path" ]] || return 0
  local size
  size="$(stat -f %z "$path")" || return 1
  dd if=/dev/zero of="$path" bs=4096 count="$(( (size + 4095) / 4096 ))" conv=notrunc status=none || return 1
  rm -f -- "$path"
}

prove_absent(){
  local device="$1"
  [[ ! -e "$mount_path" ]] || return 1
  ! mount | grep -Fq -- "$device" || return 1
  ! pgrep -f -- 'import-app-key.mjs' >/dev/null || return 1
}

cleanup_volume(){
  local device="$1"
  local source_path="${2:-}"
  local failed=0
  overwrite_source "$source_path" || failed=1
  if [[ -d "$mount_path" ]]; then
    local path
    for path in "$mount_path"/*; do
      [[ -f "$path" && ! -L "$path" ]] || continue
      overwrite_source "$path" || failed=1
    done
  fi
  diskutil unmountDisk force "$device" >/dev/null 2>&1 || failed=1
  hdiutil detach "$device" >/dev/null 2>&1 || failed=1
  if [[ -d "$mount_path" ]]; then
    rmdir -- "$mount_path" >/dev/null 2>&1 || failed=1
  fi
  prove_absent "$device" || failed=1
  [[ "$failed" -eq 0 ]]
}

resolve_device(){
  local device
  device="$(diskutil info "$mount_path" | awk -F: '/Device Node/ {sub(/^[[:space:]]+/,"",$2); print $2}')"
  [[ "$device" =~ ^/dev/disk[0-9]+$ ]] || return 1
  printf '%s\n' "$device"
}

case "$action" in
  prepare)
    [[ "$(uname -s)" == Darwin ]] || fail ram-volume-platform
    [[ ! -e "$mount_path" ]] || fail custody-volume-exists
    device="$(hdiutil attach -nomount 'ram://65536' | awk 'NR==1 {print $1}')"
    [[ "$device" =~ ^/dev/disk[0-9]+$ ]] || fail ram-device-ambiguous
    cleanup_device(){ hdiutil detach "$device" >/dev/null 2>&1 || true; }
    trap cleanup_device ERR INT TERM HUP
    diskutil eraseVolume APFS "adventures-suite-key-custody-${operation_id}" "$device" >/dev/null
    actual_mount="$(diskutil info "$device" | awk -F: '/Mount Point/ {sub(/^[[:space:]]+/,"",$2); print $2}')"
    [[ "$actual_mount" == "$mount_path" ]] || fail ram-mount-binding
    printf '{"schemaVersion":1,"operationId":"%s","device":"%s","mountPath":"%s","state":"Ready"}\n' "$operation_id" "$device" "$mount_path"
    trap - ERR INT TERM HUP
    ;;
  import)
    shift 2
    [[ $# -ge 1 ]] || fail closed-import-arguments
    pem_path="$1"
    shift
    [[ "$(dirname -- "$pem_path")" == "$mount_path" ]] || fail ram-source-binding
    [[ "$(basename -- "$pem_path")" =~ ^adventures-suite-runner-broker-dev\.[0-9]+\.private-key\.pem$ ]] || fail pem-name-binding
    mount | grep -Fq -- "on ${mount_path} (apfs" || fail ram-volume-required
    [[ -f "$pem_path" && ! -L "$pem_path" ]] || fail pem-source-required
    device="$(resolve_device)" || fail ram-device-ambiguous
    import_abort(){
      local code="$1"
      if cleanup_volume "$device" "$pem_path"; then
        printf '%s\n' "$code" >&2
      else
        printf '%s\n' custody-cleanup-failed >&2
      fi
      exit 1
    }
    [[ $# -eq 10 ]] || import_abort closed-import-arguments

    expected='|--operation-id|--vault-id|--github-key-id|--importer-client-id|--started-utc|'
    seen='|'
    importer_args=( "$@" )
    while [[ $# -gt 0 ]]; do
      option="$1"; value="${2:-}"; shift 2 || import_abort closed-import-arguments
      [[ "$expected" == *"|${option}|"* && -n "$value" ]] || import_abort closed-import-arguments
      [[ "$seen" != *"|${option}|"* ]] || import_abort closed-import-arguments
      seen="${seen}${option}|"
    done
    for required in --operation-id --vault-id --github-key-id --importer-client-id --started-utc; do
      [[ "$seen" == *"|${required}|"* ]] || import_abort closed-import-arguments
    done

    importer_pid=''
    signal_cleanup(){
      trap - INT TERM HUP
      [[ -z "$importer_pid" ]] || kill "$importer_pid" >/dev/null 2>&1 || true
      [[ -z "$importer_pid" ]] || wait "$importer_pid" >/dev/null 2>&1 || true
      if cleanup_volume "$device" "$pem_path"; then
        printf '%s\n' key-import-interrupted >&2
      else
        printf '%s\n' custody-cleanup-failed >&2
      fi
      exit 1
    }
    trap signal_cleanup INT TERM HUP
    producer(){ dd if="$pem_path" bs=16384 count=2 status=none; }
    set +e
    producer | node "$importer" "${importer_args[@]}" 3<&0 </dev/null &
    importer_pid=$!
    wait "$importer_pid"
    import_status=$?
    set -e
    trap - INT TERM HUP
    if ! cleanup_volume "$device" "$pem_path"; then
      fail custody-cleanup-failed
    fi
    [[ "$import_status" -eq 0 ]] || fail key-import-failed
    printf '{"schemaVersion":1,"operationId":"%s","state":"Absent"}\n' "$operation_id"
    ;;
  cleanup)
    [[ $# -eq 3 ]] || fail closed-cleanup-arguments
    device="$3"
    [[ "$device" =~ ^/dev/disk[0-9]+$ ]] || fail ram-device-binding
    cleanup_volume "$device" '' || fail custody-cleanup-failed
    printf '{"schemaVersion":1,"operationId":"%s","state":"Absent"}\n' "$operation_id"
    ;;
  residue)
    [[ $# -eq 3 ]] || fail closed-residue-arguments
    device="$3"
    [[ "$device" =~ ^/dev/disk[0-9]+$ ]] || fail ram-device-binding
    prove_absent "$device" || fail cleanup-residue
    printf '{"schemaVersion":1,"operationId":"%s","state":"Absent"}\n' "$operation_id"
    ;;
  *) fail closed-action ;;
esac
