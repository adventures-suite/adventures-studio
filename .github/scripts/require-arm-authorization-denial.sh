#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  exit 2
fi

error_file="$1"
command_exit="$2"

[[ "$command_exit" =~ ^[0-9]+$ ]]
test "$command_exit" -ne 0
test -f "$error_file"
test -s "$error_file"
test "$(wc -c < "$error_file")" -le 8192

# Azure CLI renders an ARM 403 authorization response with this stable error
# code. Accepting the code—not generic words such as "forbidden"—prevents DNS,
# authentication, subscription resolution, throttling, and malformed responses
# from being mistaken for the expected no-role state.
first_line="$(sed -n '/[^[:space:]]/p' "$error_file" | head -n 1)"
[[ "$first_line" =~ ^ERROR:[[:space:]]+\(AuthorizationFailed\) ]]
test "$(grep -c 'AuthorizationFailed' "$error_file")" -ge 1
! grep -Eqi 'AuthenticationFailed|InvalidAuthenticationToken|SubscriptionNotFound|MissingSubscription|ResourceNotFound|BadRequest|TooManyRequests|throttl|timed out|timeout|connection|network|name resolution|malformed|Traceback' "$error_file"
