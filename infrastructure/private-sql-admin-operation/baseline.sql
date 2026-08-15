SET NOCOUNT ON;

SELECT name, type_desc,
       CASE USER_NAME(principal_id) WHEN N'db_ddladmin' THEN N'db_ddladmin' ELSE N'unexpected-redacted' END AS owner
FROM sys.schemas
WHERE name IN (N'planning', N'auth', N'audit')
ORDER BY name;

SELECT roles.name,
       CASE USER_NAME(roles.owning_principal_id) WHEN N'dbo' THEN N'dbo' ELSE N'unexpected-redacted' END AS owner,
       CASE WHEN members.sid IS NULL THEN NULL ELSE CONVERT(varchar(64), HASHBYTES('SHA2_256', members.sid), 2) END AS member_sid_sha256
FROM sys.database_principals AS roles
LEFT JOIN sys.database_role_members AS memberships ON memberships.role_principal_id = roles.principal_id
LEFT JOIN sys.database_principals AS members ON members.principal_id = memberships.member_principal_id
WHERE roles.type = N'R'
  AND roles.name IN (N'AdventuresSuiteAuthenticationRuntime', N'AdventuresSuiteMembershipRuntime',
                     N'AdventuresSuiteCompanionReadRuntime', N'AdventuresSuitePlanningRuntime')
ORDER BY roles.name, member_sid_sha256;

SELECT name, type_desc, authentication_type_desc, default_schema_name,
       CONVERT(varchar(64), HASHBYTES('SHA2_256', sid), 2) AS sid_sha256
FROM sys.database_principals
WHERE name = N'AdventuresSuiteMigrationDev-ffc9a'
ORDER BY name;

SELECT grantee.name AS grantee, permissions.state_desc,
       CASE WHEN permissions.permission_name IN (N'CONNECT', N'CREATE TABLE', N'VIEW DEFINITION', N'CONTROL', N'SELECT', N'INSERT', N'UPDATE', N'DELETE', N'ALTER', N'REFERENCES', N'EXECUTE', N'ALTER ANY USER', N'ALTER ANY ROLE', N'CREATE USER', N'CREATE ROLE', N'CREATE SCHEMA') THEN permissions.permission_name ELSE N'unexpected-redacted' END AS permission_name,
       CASE WHEN permissions.class_desc IN (N'DATABASE', N'SCHEMA', N'OBJECT_OR_COLUMN', N'DATABASE_PRINCIPAL') THEN permissions.class_desc ELSE N'unexpected-redacted' END AS class_desc,
       CASE permissions.class
         WHEN 0 THEN DB_NAME()
         WHEN 1 THEN CASE WHEN OBJECT_SCHEMA_NAME(permissions.major_id) IN (N'dbo', N'planning', N'auth', N'audit') AND OBJECT_NAME(permissions.major_id) IN (N'AdventuresSuiteSchemaVersions', N'AuditEvents', N'CreatorMembershipPermissionGrants', N'CreatorMembershipRoles', N'CreatorMemberships', N'ExternalIdentities', N'UserSessions', N'Users', N'Accommodations', N'AdventurePlanCreateResults', N'AdventurePlans', N'BudgetItems', N'DestinationVisits', N'ItineraryDays', N'PackingItems', N'PlannedActivities', N'PlanningNotes', N'PlanningTasks', N'Reservations', N'TransportationSegments', N'TravelerParticipations', N'TravelerPreferences', N'Travelers') THEN QUOTENAME(OBJECT_SCHEMA_NAME(permissions.major_id)) + N'.' + QUOTENAME(OBJECT_NAME(permissions.major_id)) ELSE N'unexpected-redacted' END
         WHEN 3 THEN CASE WHEN SCHEMA_NAME(permissions.major_id) IN (N'dbo', N'planning', N'auth', N'audit') THEN QUOTENAME(SCHEMA_NAME(permissions.major_id)) ELSE N'unexpected-redacted' END
         WHEN 4 THEN CASE WHEN USER_NAME(permissions.major_id) IN (N'AdventuresSuiteAuthenticationRuntime', N'AdventuresSuiteMembershipRuntime', N'AdventuresSuiteCompanionReadRuntime', N'AdventuresSuitePlanningRuntime', N'AdventuresSuiteMigrationDev-ffc9a') THEN QUOTENAME(USER_NAME(permissions.major_id)) ELSE N'unexpected-redacted' END
         ELSE N'unexpected-redacted'
       END AS securable
FROM sys.database_permissions AS permissions
INNER JOIN sys.database_principals AS grantee ON grantee.principal_id = permissions.grantee_principal_id
WHERE grantee.name IN (
  N'AdventuresSuiteMigrationDev-ffc9a',
  N'AdventuresSuiteAuthenticationRuntime', N'AdventuresSuiteMembershipRuntime',
  N'AdventuresSuiteCompanionReadRuntime', N'AdventuresSuitePlanningRuntime')
ORDER BY grantee.name, permissions.class, securable, permissions.permission_name, permissions.state;

SELECT CASE WHEN OBJECT_ID(N'dbo.AdventuresSuiteSchemaVersions', N'U') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS journal_exists;
IF OBJECT_ID(N'dbo.AdventuresSuiteSchemaVersions', N'U') IS NOT NULL
    SELECT ScriptName FROM dbo.AdventuresSuiteSchemaVersions ORDER BY Id;

SELECT schema_name(schema_id) AS schema_name, type_desc, COUNT_BIG(*) AS object_count
FROM sys.objects
WHERE schema_name(schema_id) IN (N'dbo', N'planning', N'auth', N'audit')
  AND type IN (N'U', N'V', N'P', N'FN', N'IF', N'TF')
GROUP BY schema_name(schema_id), type_desc
ORDER BY schema_name, type_desc;
