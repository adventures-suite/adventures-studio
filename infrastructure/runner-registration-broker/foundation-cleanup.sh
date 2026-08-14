#!/usr/bin/env bash
set -euo pipefail
umask 077

fail(){ printf '%s\n' "$1" >&2; exit 1; }
[[ $# -eq 6 ]] || fail closed-cleanup-arguments
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
for path in "$1" "$3" "$5"; do [[ -f "$path" && ! -L "$path" ]] || fail bound-file-required; done
exec node "$dir/foundation-authority-policy.mjs" cleanup "$@"
