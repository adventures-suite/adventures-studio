#!/usr/bin/env bash
set -euo pipefail
: "${RUNNER_BROKER_HOST:?}"
[[ "$RUNNER_BROKER_HOST" =~ ^[a-z0-9][a-z0-9.-]+[a-z0-9]$ ]]

# Resolve once during reviewed bootstrap, fail if any name is unavailable, and
# permit only those current addresses. The independent cleanup deadline limits
# exposure to DNS churn; any connection failure stops rather than broadens egress.
hosts=(github.com api.github.com objects.githubusercontent.com results-receiver.actions.githubusercontent.com actions.githubusercontent.com fulcio.sigstore.dev rekor.sigstore.dev timestamp.sigstore.dev "$RUNNER_BROKER_HOST")
addresses=()
for host in "${hosts[@]}"; do
  mapfile -t resolved < <(getent ahostsv4 "$host" | awk '{print $1}' | sort -u)
  ((${#resolved[@]} > 0))
  addresses+=("${resolved[@]}")
done
mapfile -t addresses < <(printf '%s\n' "${addresses[@]}" | sort -u)

nft -f - <<RULES
flush ruleset
table inet runner {
  set https4 { type ipv4_addr; elements = { $(IFS=,; echo "${addresses[*]}") } }
  chain output {
    type filter hook output priority 0; policy drop;
    oifname "lo" accept
    ct state established,related accept
    ip daddr 168.63.129.16 udp dport 53 accept
    ip daddr 169.254.169.254 tcp dport 80 accept
    ip daddr 10.40.1.4 tcp dport 1433 accept
    ip daddr @https4 tcp dport 443 accept
  }
  chain input { type filter hook input priority 0; policy drop; iifname "lo" accept; ct state established,related accept; }
}
RULES
