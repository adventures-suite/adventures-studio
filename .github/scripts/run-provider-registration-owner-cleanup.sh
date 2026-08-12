#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

if [ "$#" -ne 3 ]; then exit 2; fi
expected_assignment_id="$1"; recorded_deadline="$2"; evidence_file="$3"
subscription='5ace9cdd-06d1-47d9-8214-1e7c756d076a'
scope="/subscriptions/$subscription"
assignment_uuid='3327e40f-74ee-42e5-a0ee-e8002b125cb3'
assignment_id="$scope/providers/Microsoft.Authorization/roleAssignments/$assignment_uuid"
temporary="$(mktemp -d)"
cleanup() { task_exit="$?"; rm -f "$temporary/assignment.json" "$temporary/residue.json" "$temporary/error"; rmdir "$temporary" 2>/dev/null || true; exit "$task_exit"; }
trap cleanup EXIT

test "${APPROVED_SUBSCRIPTION_ID:-}" = "$subscription"
test "${expected_assignment_id,,}" = "${assignment_id,,}"
[[ "$recorded_deadline" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$ ]]
date -u -d "$recorded_deadline" +%s >/dev/null

# Listing makes absence an idempotent success while authentication or read
# failures remain distinguishable command failures.
az role assignment list --scope "$scope" --subscription "$subscription" --all --only-show-errors --output json \
  >"$temporary/assignment.json" 2> >( { head -c 65537; cat >/dev/null; } >"$temporary/error")
ASSIGNMENT_FILE="$temporary/assignment.json" node --input-type=module <<'JS'
import fs from 'node:fs';
const values=JSON.parse(fs.readFileSync(process.env.ASSIGNMENT_FILE,'utf8'));
const matches=values.filter(a => a.id?.split('/').at(-1)?.toLowerCase() === '3327e40f-74ee-42e5-a0ee-e8002b125cb3');
if (matches.length > 1) process.exit(1);
if (matches.length === 1) {
  const a=matches[0];
  if (a.scope?.toLowerCase() !== '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a' || a.principalId?.toLowerCase() !== 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8' || a.principalType !== 'ServicePrincipal' || a.roleDefinitionId?.split('/').at(-1)?.toLowerCase() !== 'fcdbbdc4-b56a-4863-aebb-32790e5b1a51' || a.condition) process.exit(1);
}
fs.writeFileSync(process.env.ASSIGNMENT_FILE, String(matches.length));
JS
if [ "$(cat "$temporary/assignment.json")" -eq 1 ]; then
  az role assignment delete --ids "$assignment_id" --only-show-errors \
    2> >( { head -c 65537; cat >/dev/null; } >"$temporary/error")
fi

az role assignment list --subscription "$subscription" --assignee-object-id b77b6201-ad26-4f77-8f88-6d0d43f7dbb8 \
  --include-inherited --all --only-show-errors --output json >"$temporary/residue.json" \
  2> >( { head -c 65537; cat >/dev/null; } >"$temporary/error")
RESIDUE_FILE="$temporary/residue.json" node --input-type=module <<'JS'
import fs from 'node:fs'; const values=JSON.parse(fs.readFileSync(process.env.RESIDUE_FILE,'utf8'));
if (!Array.isArray(values) || values.some(a => a.roleDefinitionId?.split('/').at(-1)?.toLowerCase() === 'fcdbbdc4-b56a-4863-aebb-32790e5b1a51')) process.exit(1);
JS
printf '{"classification":"owner_cleanup_complete","assignmentId":"%s","residualAssignments":0}\n' "$assignment_uuid" >"$evidence_file"
node .github/scripts/provider-registration-policy.mjs cleanup-evidence "$evidence_file" >/dev/null
