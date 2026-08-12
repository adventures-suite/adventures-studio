#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

if [ "$#" -ne 6 ]; then exit 2; fi
expected_assignment_id="$1"; assignment_timestamp="$2"; deadline="$3"; state_file="$4"; evidence_file="$5"; prefix="$6"
assignment_file="${prefix}.assignment.json"; assignments_file="${prefix}.assignments.json"; provider_file="${prefix}.provider.json"; error_file="${prefix}.err"
subscription='5ace9cdd-06d1-47d9-8214-1e7c756d076a'
scope="/subscriptions/$subscription"
principal='b77b6201-ad26-4f77-8f88-6d0d43f7dbb8'
role='fcdbbdc4-b56a-4863-aebb-32790e5b1a51'
assignment="$scope/providers/Microsoft.Authorization/roleAssignments/3327e40f-74ee-42e5-a0ee-e8002b125cb3"
stage='input_validation'; classification='operation_failed'
write_state() { printf 'stage=%s\nclassification=%s\nexit_code=%s\n' "$stage" "$classification" "$1" >"$state_file"; }
cleanup() { code="$?"; rm -f "$assignment_file" "$assignments_file" "$provider_file" "$error_file"; [ "$code" -eq 0 ] || write_state "$code"; exit "$code"; }
trap cleanup EXIT

test "${APPROVED_SUBSCRIPTION_ID:-}" = "$subscription"
test "${APPROVED_TENANT_ID:-}" = 'd7add2bb-ac03-49a8-9377-d0bf6a012f2f'
test "${AZURE_CLIENT_ID:-}" = '223af00d-69e5-4302-9ac5-6b338f3ea2e5'
test "${EXPECTED_PRINCIPAL_ID:-}" = "$principal"
test "${expected_assignment_id,,}" = "${assignment,,}"
node --input-type=module -e "import {validateAuthorityWindow} from './.github/scripts/provider-registration-policy.mjs'; validateAuthorityWindow(process.argv[1], process.argv[2])" "$assignment_timestamp" "$deadline"

stage='assignment_validation'; classification='assignment_ambiguous'
az role assignment show --ids "$assignment" --only-show-errors --output json >"$assignment_file" 2> >( { head -c 65537; cat >/dev/null; } >"$error_file")
ASSIGNMENT_FILE="$assignment_file" node --input-type=module <<'JS'
import fs from 'node:fs';
const a=JSON.parse(fs.readFileSync(process.env.ASSIGNMENT_FILE,'utf8'));
const scope='/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a';
if (a.id?.toLowerCase() !== `${scope}/providers/microsoft.authorization/roleassignments/3327e40f-74ee-42e5-a0ee-e8002b125cb3`.toLowerCase() || a.scope?.toLowerCase() !== scope.toLowerCase() || a.principalId?.toLowerCase() !== 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8' || a.principalType !== 'ServicePrincipal' || a.roleDefinitionId?.split('/').at(-1)?.toLowerCase() !== 'fcdbbdc4-b56a-4863-aebb-32790e5b1a51' || a.condition) process.exit(1);
JS
az role assignment list --subscription "$subscription" --assignee-object-id "$principal" --include-inherited --all --only-show-errors --output json >"$assignments_file" 2> >( { head -c 65537; cat >/dev/null; } >"$error_file")
ASSIGNMENTS_FILE="$assignments_file" node --input-type=module <<'JS'
import fs from 'node:fs'; const values=JSON.parse(fs.readFileSync(process.env.ASSIGNMENTS_FILE,'utf8'));
if (!Array.isArray(values) || values.length !== 1 || values[0].id?.split('/').at(-1)?.toLowerCase() !== '3327e40f-74ee-42e5-a0ee-e8002b125cb3') process.exit(1);
JS

providers=('Microsoft.App' 'Microsoft.ContainerRegistry')
initial=()
stage='initial_state'; classification='unexpected_initial_state'
for provider in "${providers[@]}"; do
  state="$(az provider show --namespace "$provider" --subscription "$subscription" --query registrationState --output tsv --only-show-errors 2> >( { head -c 65537; cat >/dev/null; } >"$error_file"))"
  test "$state" = 'NotRegistered'
  initial+=("$state")
done

stage='registration_request'; classification='registration_failed'
for provider in "${providers[@]}"; do
  az provider register --namespace "$provider" --subscription "$subscription" --only-show-errors --output none 2> >( { head -c 65537; cat >/dev/null; } >"$error_file")
done

stage='registration_poll'; classification='registration_timeout'
terminal=('' '')
for _ in $(seq 1 120); do
  now="$(date -u +%s)"; deadline_epoch="$(date -u -d "$deadline" +%s)"; test "$now" -lt "$deadline_epoch"
  complete=true
  for index in 0 1; do
    state="$(az provider show --namespace "${providers[$index]}" --subscription "$subscription" --query registrationState --output tsv --only-show-errors 2> >( { head -c 65537; cat >/dev/null; } >"$error_file"))"
    case "$state" in NotRegistered|Registering|Registered) ;; *) classification='ambiguous_provider_state'; exit 1 ;; esac
    terminal[$index]="$state"; [ "$state" = Registered ] || complete=false
  done
  [ "$complete" = true ] && break
  sleep 10
done
test "${terminal[0]}" = Registered && test "${terminal[1]}" = Registered
printf '{"assignmentId":"3327e40f-74ee-42e5-a0ee-e8002b125cb3","assignmentTimestamp":"%s","authorityDeadline":"%s","classification":"providers_registered","providers":[{"namespace":"Microsoft.App","initialState":"NotRegistered","terminalState":"Registered"},{"namespace":"Microsoft.ContainerRegistry","initialState":"NotRegistered","terminalState":"Registered"}]}\n' "$assignment_timestamp" "$deadline" >"$evidence_file"
node .github/scripts/provider-registration-policy.mjs registration-evidence "$evidence_file" >/dev/null
stage='complete'; classification='complete'; write_state 0
