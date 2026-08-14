#!/usr/bin/env bash
set -euo pipefail
umask 077

fail(){ printf '%s\n' "$1" >&2; exit 1; }
[[ $# -eq 4 ]] || fail closed-inventory-arguments
[[ -f "$1" && ! -L "$1" ]] || fail catalog-file-required
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec node "$dir/foundation-authority-policy.mjs" inventory "$1" "$2" "$3" "$4" json
