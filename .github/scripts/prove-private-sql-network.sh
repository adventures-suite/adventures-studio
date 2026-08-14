#!/usr/bin/env bash
set -euo pipefail
trap 'printf '\''{"classification":"private_sql_network_proof_failed","sqlCommandAttempted":false}\n'\'' >&2' ERR
: "${EXPECTED_SQL_HOST:?}" "${EXPECTED_PRIVATE_IP:?}"
[[ "$EXPECTED_SQL_HOST" =~ ^[a-z0-9-]+\.database\.windows\.net$ ]]
[[ "$EXPECTED_PRIVATE_IP" =~ ^10\.40\.1\.4$ ]]
mapfile -t resolved < <(getent ahostsv4 "$EXPECTED_SQL_HOST" | awk '{print $1}' | sort -u)
test "${#resolved[@]}" -eq 1
test "${resolved[0]}" = "$EXPECTED_PRIVATE_IP"
timeout 10 bash -c 'exec 3<>"/dev/tcp/$1/1433"; exec 3>&-; exec 3<&-' _ "$EXPECTED_SQL_HOST" 2>/dev/null
printf '{"classification":"private_sql_network_verified","dnsPrivate":true,"tcp1433Reachable":true,"sqlCommandAttempted":false}\n'
