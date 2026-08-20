IF OBJECT_ID(N'dbo.AdventuresSuiteSchemaVersions', N'U') IS NULL
   OR (SELECT COUNT(*) FROM dbo.AdventuresSuiteSchemaVersions) <> 11
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
         (N'0010_create_companion_policy_assignments.sql'),
         (N'0011_create_adventure_plan_template_origins.sql')) AS Expected(ScriptName)
       WHERE NOT EXISTS
       (
           SELECT 1 FROM dbo.AdventuresSuiteSchemaVersions AS Journal
           WHERE RIGHT(Journal.ScriptName, LEN(Expected.ScriptName)) = Expected.ScriptName
       )
   )
    THROW 51000, 'Migration 0012 requires the exact complete 0011 journal.', 1;

IF OBJECT_ID(N'planning.AdventurePlans', N'U') IS NULL
   OR OBJECT_ID(N'planning.DestinationVisits', N'U') IS NULL
   OR DATABASE_PRINCIPAL_ID(N'AdventuresSuitePlanningRuntime') IS NULL
   OR OBJECT_ID(N'planning.PlannerFootStepApplications', N'U') IS NOT NULL
    THROW 51000, 'Planner FootStep application prerequisites are not exact.', 1;

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

CREATE TABLE planning.PlannerFootStepApplications
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    IdempotencyKey nvarchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
    FingerprintVersion int NOT NULL,
    RequestFingerprint binary(32) NOT NULL,
    FootStepId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    FootStepVersion nvarchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    TargetType varchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    TargetId nvarchar(64) NOT NULL,
    ResultingVersion bigint NOT NULL,
    Attribution nvarchar(300) NOT NULL,
    UseDecisionReference nvarchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
    AppliedAtUtc datetimeoffset(7) NOT NULL,
    CONSTRAINT PK_PlannerFootStepApplications
        PRIMARY KEY (CreatorId, AdventurePlanId, IdempotencyKey),
    CONSTRAINT FK_PlannerFootStepApplications_Plan
        FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId),
    CONSTRAINT FK_PlannerFootStepApplications_Destination
        FOREIGN KEY (CreatorId, AdventurePlanId, TargetId)
        REFERENCES planning.DestinationVisits (CreatorId, AdventurePlanId, DestinationVisitId),
    CONSTRAINT CK_PlannerFootStepApplications_Key CHECK (LEN(IdempotencyKey) BETWEEN 16 AND 128),
    CONSTRAINT CK_PlannerFootStepApplications_Source CHECK
        (LEN(FootStepId) BETWEEN 1 AND 64 AND LEN(FootStepVersion) BETWEEN 1 AND 32),
    CONSTRAINT CK_PlannerFootStepApplications_Target CHECK
        (TargetType = 'DestinationVisit' AND LEN(TargetId) BETWEEN 1 AND 64),
    CONSTRAINT CK_PlannerFootStepApplications_Fingerprint CHECK
        (FingerprintVersion > 0),
    CONSTRAINT CK_PlannerFootStepApplications_Version CHECK
        (ResultingVersion >= 2),
    CONSTRAINT CK_PlannerFootStepApplications_Attribution CHECK
        (LEN(Attribution) BETWEEN 1 AND 300 AND Attribution = LTRIM(RTRIM(Attribution))),
    CONSTRAINT CK_PlannerFootStepApplications_Decision CHECK
        (LEN(UseDecisionReference) BETWEEN 1 AND 128),
    CONSTRAINT CK_PlannerFootStepApplications_Time CHECK
        (DATEPART(TZOFFSET, AppliedAtUtc) = 0)
);

CREATE UNIQUE INDEX UX_PlannerFootStepApplications_Target
    ON planning.PlannerFootStepApplications (CreatorId, AdventurePlanId, TargetType, TargetId);
CREATE INDEX IX_PlannerFootStepApplications_Source
    ON planning.PlannerFootStepApplications (FootStepId, FootStepVersion, CreatorId);

GRANT SELECT, INSERT ON OBJECT::planning.PlannerFootStepApplications
    TO AdventuresSuitePlanningRuntime;
DENY UPDATE, DELETE ON OBJECT::planning.PlannerFootStepApplications
    TO AdventuresSuitePlanningRuntime;
DENY ALTER ON SCHEMA::planning TO AdventuresSuitePlanningRuntime;
