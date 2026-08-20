#!/usr/bin/env bash
set -euo pipefail
dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
policy="$dir/authority-policy.json"
operation="$dir/operate.sh"

jq -e '
  .schemaVersion == 1 and
  .normalAdministrator.displayName == "AdventuresSuite Development SQL Bootstrap Administrators" and
  .normalAdministrator.objectId == "99a35676-95b4-47d9-bbde-74ece42ebcae" and
  .temporaryAdministrator.displayName == "id-adventures-suite-sql-bootstrap-dev" and
  .temporaryAdministrator.principalId == "34069e5e-75f9-42ac-a7f8-f0115e9434bb" and
  .operations == ["prepare-establish", "establish", "prepare-restore", "restore"] and
  .automaticRetryCount == 0 and
  .publicNetworkChangesAllowed == false and
  .groupMembershipChangesAllowed == false and
  .sqlDataOperationsAllowed == false
' "$policy" >/dev/null

for required in \
  'git ls-remote origin refs/heads/main' \
  'git diff --quiet' \
  'userType' \
  'azureAdOnly' \
  'AUTHORITY_APPROVAL_SHA256' \
  'az sql server ad-admin create' \
  'require_admin "$desired_name" "$desired_id"'; do
  grep -F "$required" "$operation" >/dev/null
done

for prohibited in \
  'az ad group member add' \
  'public-network-access' \
  'firewall-rule' \
  'sqlcmd' \
  'DELETE FROM' \
  'INSERT INTO' \
  'UPDATE '; do
  ! grep -F "$prohibited" "$operation" >/dev/null
done

printf 'Private SQL administrator authority policy tests passed.\n'
