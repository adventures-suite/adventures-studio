#!/usr/bin/env bash
set -euo pipefail
umask 077

fail(){ printf '%s\n' "$1" >&2; exit 1; }
[[ $# -eq 8 ]] || fail closed-residue-arguments
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
for path in "$1" "$3" "$7"; do [[ -f "$path" && ! -L "$path" ]] || fail bound-file-required; done
exec node "$dir/foundation-authority-policy.mjs" residue "$@"
