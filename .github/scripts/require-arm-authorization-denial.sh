#!/usr/bin/env bash
set -euo pipefail

classification='malformed_or_ambiguous'

if [ "$#" -eq 2 ] && [[ "$2" =~ ^[0-9]+$ ]]; then
  error_file="$1"
  command_exit="$2"

  if [ "$command_exit" -eq 0 ]; then
    classification='unexpected_success'
  elif [ -f "$error_file" ] && [ -s "$error_file" ] &&
    [ "$(wc -c < "$error_file")" -le 8192 ]; then
    first_line="$(sed -n '/[^[:space:]]/p' "$error_file" | head -n 1)"
    if [[ "$first_line" =~ ^ERROR:[[:space:]]+\(AuthorizationFailed\) ]] &&
      ! grep -Eqi 'AuthenticationFailed|InvalidAuthenticationToken|SubscriptionNotFound|MissingSubscription|ResourceNotFound|BadRequest|TooManyRequests|throttl|timed out|timeout|connection|network|name resolution|malformed|Traceback' "$error_file"; then
      classification='authorization_failed'
    elif grep -Eqi 'AuthenticationFailed|InvalidAuthenticationToken' "$error_file"; then
      classification='authentication_failed'
    elif grep -Eqi 'SubscriptionNotFound|MissingSubscription' "$error_file"; then
      classification='subscription_resolution_failed'
    elif grep -Eqi 'ResourceNotFound' "$error_file"; then
      classification='resource_not_found'
    elif grep -Eqi 'TooManyRequests|throttl|HTTP[[:space:]]+429' "$error_file"; then
      classification='throttled'
    elif grep -Eqi 'timed out|timeout|connection|network|name resolution|could not resolve|unreachable' "$error_file"; then
      classification='network_failed'
    fi
  fi
fi

printf '%s\n' "$classification"
test "$classification" = 'authorization_failed'
