#!/usr/bin/env bash
set -euo pipefail
sql="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/baseline.sql"
reject='\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|GRANT|DENY|REVOKE|EXECUTE?|DBCC|BACKUP|RESTORE|OPENROWSET|OPENDATASOURCE|BULK)\b'
sql_without_literals="$(sed "s/N*'[^']*'//g" "$sql")"
! printf '%s\n' "$sql_without_literals" | rg -i "$reject"
test "$(rg -ic '^SELECT |^FROM |^WHERE |^ORDER BY |^GROUP BY |^LEFT JOIN |^INNER JOIN |^  AND |^  WHEN |^       CASE |^       CONVERT' "$sql")" -gt 0
test "$(rg -c '^SELECT |^    SELECT ' "$sql")" = 7
rg -q --fixed-strings "IF OBJECT_ID" "$sql"
rg -q --fixed-strings "SELECT ScriptName" "$sql"
rg -q --fixed-strings "sys.database_permissions" "$sql"
rg -q --fixed-strings "sys.database_role_members" "$sql"
rg -q --fixed-strings "sys.objects" "$sql"
