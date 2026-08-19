IF OBJECT_ID(N'dbo.AdventuresSuiteSchemaVersions', N'U') IS NULL
   OR (SELECT COUNT(*) FROM dbo.AdventuresSuiteSchemaVersions) <> 10
   OR EXISTS
   (
       SELECT Expected.ScriptName
       FROM (VALUES
         (N'0001_create_planning_schema.sql'),
         (N'0002_create_adventure_plans.sql'),
         (N'0003_create_planning_children.sql'),
         (N'0004_create_authentication_persistence.sql'),
         (N'0005_bind_sessions_to_external_identities.sql'),
         (N'0006_create_creator_memberships.sql'),
         (N'0007_create_traveler_participations.sql'),
         (N'0008_create_companion_read_role.sql'),
         (N'0009_create_adventure_plan_create_results.sql'),
         (N'0010_create_companion_policy_assignments.sql')) AS Expected(ScriptName)
       WHERE NOT EXISTS
       (
           SELECT 1 FROM dbo.AdventuresSuiteSchemaVersions AS Journal
           WHERE RIGHT(Journal.ScriptName, LEN(Expected.ScriptName)) = Expected.ScriptName
       )
   )
    THROW 51000, 'Migration 0011 requires the exact complete 0010 journal.', 1;

IF NOT EXISTS
      (SELECT 1 FROM dbo.AdventuresSuiteSchemaVersions
       WHERE ScriptName LIKE N'%0010_create_companion_policy_assignments.sql')
    THROW 51000, 'Migration 0011 requires the Companion policy assignment prerequisite.', 1;

IF OBJECT_ID(N'planning.AdventurePlans', N'U') IS NULL
   OR OBJECT_ID(N'planning.AdventurePlanCreateResults', N'U') IS NULL
   OR DATABASE_PRINCIPAL_ID(N'AdventuresSuitePlanningRuntime') IS NULL
   OR OBJECT_ID(N'planning.AdventurePlanTemplateOrigins', N'U') IS NOT NULL
    THROW 51000, 'Adventure Template provenance prerequisites are not exact.', 1;

IF EXISTS
(
    SELECT 1 FROM sys.database_principals AS roles
    LEFT JOIN sys.database_principals AS owners
      ON owners.principal_id = roles.owning_principal_id
    WHERE roles.name = N'AdventuresSuitePlanningRuntime'
      AND (roles.type <> N'R' OR roles.is_fixed_role <> 0 OR owners.name <> N'dbo')
)
OR (SELECT COUNT(*) FROM sys.database_role_members AS memberships
    INNER JOIN sys.database_principals AS roles
      ON roles.principal_id = memberships.role_principal_id
    WHERE roles.name = N'AdventuresSuitePlanningRuntime') <> 0
OR (SELECT COUNT(*) FROM sys.database_role_members AS memberships
    INNER JOIN sys.database_principals AS members
      ON members.principal_id = memberships.member_principal_id
    WHERE members.name = N'AdventuresSuitePlanningRuntime') <> 0
    THROW 51000, 'The Planning runtime role is not the exact authority-free prerequisite.', 1;

ALTER TABLE planning.AdventurePlanCreateResults
    DROP CONSTRAINT CK_AdventurePlanCreateResults_Operation;

ALTER TABLE planning.AdventurePlanCreateResults
    ADD CONSTRAINT CK_AdventurePlanCreateResults_Operation CHECK
        (Operation IN ('AdventurePlan.Create.v1', 'AdventurePlan.TemplateInstantiate.v1'));

CREATE TABLE planning.AdventurePlanTemplateOrigins
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    TemplateId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    TemplateVersion nvarchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    TemplateOwnerType varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    TemplateOwnerId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    SourceLocale varchar(35) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Attribution nvarchar(300) NOT NULL,
    UseDecisionReference nvarchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
    ParameterFingerprintVersion int NOT NULL,
    ParameterFingerprint binary(32) NOT NULL,
    InstantiatedAtUtc datetimeoffset(7) NOT NULL,
    CONSTRAINT PK_AdventurePlanTemplateOrigins PRIMARY KEY (CreatorId, AdventurePlanId),
    CONSTRAINT FK_AdventurePlanTemplateOrigins_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId),
    CONSTRAINT CK_AdventurePlanTemplateOrigins_Template CHECK
        (LEN(TemplateId) BETWEEN 1 AND 64 AND LEN(TemplateVersion) BETWEEN 1 AND 32),
    CONSTRAINT CK_AdventurePlanTemplateOrigins_Owner CHECK
        (TemplateOwnerType IN ('Platform', 'Creator', 'Agency')
         AND LEN(TemplateOwnerId) BETWEEN 1 AND 64),
    CONSTRAINT CK_AdventurePlanTemplateOrigins_Locale CHECK
        (LEN(SourceLocale) BETWEEN 2 AND 35),
    CONSTRAINT CK_AdventurePlanTemplateOrigins_Attribution CHECK
        (LEN(Attribution) BETWEEN 1 AND 300 AND Attribution = LTRIM(RTRIM(Attribution))),
    CONSTRAINT CK_AdventurePlanTemplateOrigins_Decision CHECK
        (LEN(UseDecisionReference) BETWEEN 1 AND 128),
    CONSTRAINT CK_AdventurePlanTemplateOrigins_Fingerprint CHECK
        (ParameterFingerprintVersion > 0),
    CONSTRAINT CK_AdventurePlanTemplateOrigins_Time CHECK
        (DATEPART(TZOFFSET, InstantiatedAtUtc) = 0)
);

CREATE INDEX IX_AdventurePlanTemplateOrigins_TemplateVersion
    ON planning.AdventurePlanTemplateOrigins (TemplateId, TemplateVersion, CreatorId);

GRANT SELECT, INSERT ON OBJECT::planning.AdventurePlanTemplateOrigins
    TO AdventuresSuitePlanningRuntime;
DENY UPDATE, DELETE ON OBJECT::planning.AdventurePlanTemplateOrigins
    TO AdventuresSuitePlanningRuntime;
DENY ALTER ON SCHEMA::planning TO AdventuresSuitePlanningRuntime;
