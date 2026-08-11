#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

if [ "$#" -ne 5 ]; then
  exit 2
fi

authorization_config="$1"
method="$2"
url="$3"
request_body="$4"
evidence_prefix="$5"
response_body="${evidence_prefix}.body"
http_status="${evidence_prefix}.status"
transport_error="${evidence_prefix}.transport.err"

case "$method" in
  GET|POST) ;;
  *) exit 2 ;;
esac
case "$url" in
  https://management.azure.com/*) ;;
  *) exit 2 ;;
esac
if [ "$method" = 'POST' ] && [ ! -f "$request_body" ]; then
  exit 2
fi

curl_arguments=(
  --silent
  --show-error
  --request "$method"
  --url "$url"
  --config "$authorization_config"
  --proto '=https'
  --proto-redir '=https'
  --max-redirs 0
  --connect-timeout 10
  --max-time 20
  --max-filesize 65536
  --output "$response_body"
  --write-out '%{http_code}'
)
if [ "$method" = 'POST' ]; then
  curl_arguments+=(
    --header 'Content-Type: application/json'
    --data-binary "@$request_body"
  )
fi

set +e
curl "${curl_arguments[@]}" >"$http_status" 2>"$transport_error"
transport_exit="$?"
set -e

ARM_PROBE_TRANSPORT_EXIT="$transport_exit" \
ARM_PROBE_BODY_FILE="$response_body" \
ARM_PROBE_STATUS_FILE="$http_status" \
python3 - <<'PY'
import json
import os
from pathlib import Path

transport_exit = int(os.environ['ARM_PROBE_TRANSPORT_EXIT'])
body_path = Path(os.environ['ARM_PROBE_BODY_FILE'])
status_path = Path(os.environ['ARM_PROBE_STATUS_FILE'])

if transport_exit == 63:
    classification = 'oversized_response'
elif transport_exit == 28:
    classification = 'transport_timeout'
elif transport_exit != 0:
    classification = 'network_failed'
else:
    try:
        status_text = status_path.read_text(encoding='utf-8').strip()
        if len(status_text) != 3 or not status_text.isascii() or not status_text.isdigit():
            raise ValueError()
        status = int(status_text)
    except Exception:
        classification = 'malformed_or_ambiguous'
    else:
        size = body_path.stat().st_size if body_path.exists() else 0
        if size > 65536:
            classification = 'oversized_response'
        elif 200 <= status <= 299:
            classification = 'unexpected_success'
        elif 300 <= status <= 399:
            classification = 'redirect'
        elif status == 401:
            classification = 'authentication_failed'
        elif status == 404:
            classification = 'resource_not_found'
        elif status == 408:
            classification = 'request_timeout'
        elif status == 409:
            classification = 'conflict'
        elif status == 429:
            classification = 'throttled'
        elif 500 <= status <= 599:
            classification = 'server_error'
        elif status != 403:
            classification = 'unexpected_http_status'
        else:
            try:
                document = json.loads(body_path.read_text(encoding='utf-8'))
            except Exception:
                classification = 'malformed_json'
            else:
                if not isinstance(document, dict) or not isinstance(document.get('error'), dict):
                    classification = 'missing_error_code'
                else:
                    code = document['error'].get('code')
                    if code == 'AuthorizationFailed':
                        classification = 'authorization_failed'
                    elif code is None:
                        classification = 'missing_error_code'
                    else:
                        classification = 'unexpected_error_code'

print(classification)
raise SystemExit(0 if classification == 'authorization_failed' else 1)
PY
