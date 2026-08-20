SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.AdventuresSuiteSchemaVersions', N'U') IS NULL
   OR (SELECT COUNT(*) FROM dbo.AdventuresSuiteSchemaVersions) <> 12
   OR NOT EXISTS
   (
       SELECT 1 FROM dbo.AdventuresSuiteSchemaVersions
       WHERE ScriptName LIKE N'%0012_create_planner_footstep_applications.sql'
   )
    THROW 51000, 'Migration 0013 requires the exact complete 0012 journal.', 1;

IF SCHEMA_ID(N'planning') IS NULL
   OR DATABASE_PRINCIPAL_ID(N'AdventuresSuitePlanningRuntime') IS NULL
    THROW 51000, 'The Planning runtime permission prerequisites are unavailable.', 1;

IF EXISTS
(
    SELECT 1 FROM sys.database_principals AS roles
    LEFT JOIN sys.database_principals AS owners
      ON owners.principal_id = roles.owning_principal_id
    WHERE roles.name = N'AdventuresSuitePlanningRuntime'
      AND (roles.type <> N'R' OR roles.is_fixed_role <> 0 OR owners.name <> N'dbo')
)
OR EXISTS
(
    SELECT 1 FROM sys.database_role_members AS memberships
    INNER JOIN sys.database_principals AS roles
      ON roles.principal_id = memberships.role_principal_id
    WHERE roles.name = N'AdventuresSuitePlanningRuntime'
)
OR EXISTS
(
    SELECT 1 FROM sys.database_role_members AS memberships
    INNER JOIN sys.database_principals AS members
      ON members.principal_id = memberships.member_principal_id
    WHERE members.name = N'AdventuresSuitePlanningRuntime'
)
    THROW 51000, 'The Planning runtime role is not the exact unbound prerequisite.', 1;

GRANT SELECT, INSERT, UPDATE ON SCHEMA::planning TO AdventuresSuitePlanningRuntime;
DENY DELETE, ALTER ON SCHEMA::planning TO AdventuresSuitePlanningRuntime;
