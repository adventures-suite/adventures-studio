#!/usr/bin/env bash
set -euo pipefail

fail(){ printf '%s\n' "$1" >&2; exit 1; }
operation_id="${1:-}"
action="${2:-}"
[[ "$operation_id" =~ ^broker-key-[a-z0-9]{16,64}$ ]] || fail operation-id-binding
mount_path="/Volumes/adventures-suite-key-custody-${operation_id}"

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
    [[ $# -eq 12 ]] || fail closed-import-arguments
    pem_path="$1"; shift
    [[ "$(dirname -- "$pem_path")" == "$mount_path" ]] || fail ram-source-binding
    [[ "$(basename -- "$pem_path")" =~ ^adventures-suite-runner-broker-dev\.[0-9]+\.private-key\.pem$ ]] || fail pem-name-binding
    mount | rg -q --fixed-strings "on ${mount_path} (apfs" || fail ram-volume-required
    [[ -f "$pem_path" && ! -L "$pem_path" ]] || fail pem-source-required
    cleanup_source(){
      if [[ -f "$pem_path" && ! -L "$pem_path" ]]; then
        size="$(stat -f %z "$pem_path")"
        dd if=/dev/zero of="$pem_path" bs=4096 count="$(( (size + 4095) / 4096 ))" conv=notrunc status=none || true
        rm -f -- "$pem_path"
      fi
    }
    trap cleanup_source EXIT INT TERM HUP
    producer(){ dd if="$pem_path" bs=16384 count=2 status=none; }
    producer | node "$(dirname -- "${BASH_SOURCE[0]}")/import-app-key.mjs" "$@" 3<&0 </dev/null
    ;;
  cleanup)
    device="${3:-}"
    [[ "$device" =~ ^/dev/disk[0-9]+$ ]] || fail ram-device-binding
    if [[ -d "$mount_path" ]]; then
      for path in "$mount_path"/*; do
        [[ -f "$path" && ! -L "$path" ]] || continue
        size="$(stat -f %z "$path")"
        dd if=/dev/zero of="$path" bs=4096 count="$(( (size + 4095) / 4096 ))" conv=notrunc status=none
        rm -f -- "$path"
      done
    fi
    diskutil unmountDisk force "$device" >/dev/null
    hdiutil detach "$device" >/dev/null
    [[ ! -e "$mount_path" ]] || fail cleanup-residue
    ! mount | rg -q --fixed-strings "$device" || fail cleanup-residue
    printf '{"schemaVersion":1,"operationId":"%s","state":"Absent"}\n' "$operation_id"
    ;;
  residue)
    device="${3:-}"
    [[ "$device" =~ ^/dev/disk[0-9]+$ ]] || fail ram-device-binding
    [[ ! -e "$mount_path" ]] || fail cleanup-residue
    ! mount | rg -q --fixed-strings "$device" || fail cleanup-residue
    pgrep -f 'import-app-key.mjs' >/dev/null && fail importer-process-residue
    printf '{"schemaVersion":1,"operationId":"%s","state":"Absent"}\n' "$operation_id"
    ;;
  *) fail closed-action ;;
esac
