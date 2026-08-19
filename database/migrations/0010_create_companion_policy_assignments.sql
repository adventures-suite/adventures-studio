IF (SELECT COUNT(*) FROM dbo.AdventuresSuiteSchemaVersions) <> 9
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
         (N'0009_create_adventure_plan_create_results.sql')) AS Expected(ScriptName)
       WHERE NOT EXISTS
       (
           SELECT 1 FROM dbo.AdventuresSuiteSchemaVersions AS Journal
           WHERE RIGHT(Journal.ScriptName, LEN(Expected.ScriptName)) = Expected.ScriptName
       )
   )
    THROW 51000, 'The exact reviewed 0001 through 0009 journal prerequisite is missing.', 1;

IF DATABASE_PRINCIPAL_ID(N'AdventuresSuiteCompanionReadRuntime') IS NULL
    THROW 51000, 'The reviewed Companion read runtime role prerequisite is missing.', 1;
IF DATABASE_PRINCIPAL_ID(N'AdventuresSuiteCompanionPolicyRuntime') IS NULL
    THROW 51000, 'The reviewed Companion policy runtime role prerequisite is missing.', 1;

IF EXISTS
(
    SELECT 1 FROM sys.database_principals AS roles
    LEFT JOIN sys.database_principals AS owners
      ON owners.principal_id = roles.owning_principal_id
    WHERE roles.name IN
        (N'AdventuresSuiteCompanionReadRuntime', N'AdventuresSuiteCompanionPolicyRuntime')
      AND (roles.type <> N'R' OR roles.is_fixed_role <> 0 OR owners.name <> N'dbo')
)
OR (SELECT COUNT(*) FROM sys.database_role_members AS memberships
    INNER JOIN sys.database_principals AS roles
      ON roles.principal_id = memberships.role_principal_id
    WHERE roles.name IN
        (N'AdventuresSuiteCompanionReadRuntime', N'AdventuresSuiteCompanionPolicyRuntime')) <> 0
OR (SELECT COUNT(*) FROM sys.database_role_members AS memberships
    INNER JOIN sys.database_principals AS members
      ON members.principal_id = memberships.member_principal_id
    WHERE members.name IN
        (N'AdventuresSuiteCompanionReadRuntime', N'AdventuresSuiteCompanionPolicyRuntime')) <> 0
OR (SELECT COUNT(*) FROM sys.database_permissions AS permissions
    INNER JOIN sys.database_principals AS principals
      ON principals.principal_id = permissions.grantee_principal_id
    WHERE principals.name = N'AdventuresSuiteCompanionPolicyRuntime') <> 0
    THROW 51000, 'A Companion runtime role is not the exact reviewed prerequisite.', 1;

IF OBJECT_ID(N'planning.AdventurePlans', N'U') IS NULL
   OR OBJECT_ID(N'planning.TravelerParticipations', N'U') IS NULL
   OR OBJECT_ID(N'auth.Users', N'U') IS NULL
   OR OBJECT_ID(N'audit.AuditEvents', N'U') IS NULL
   OR NOT EXISTS (SELECT 1 FROM sys.key_constraints
                  WHERE parent_object_id = OBJECT_ID(N'planning.AdventurePlans')
                    AND name = N'PK_AdventurePlans')
   OR NOT EXISTS (SELECT 1 FROM sys.key_constraints
                  WHERE parent_object_id = OBJECT_ID(N'planning.TravelerParticipations')
                    AND name = N'UQ_TravelerParticipations_Traveler')
    THROW 51000, 'A policy-assignment parent key prerequisite is missing.', 1;

DECLARE @PlanningCreatorCollation sysname =
    (SELECT collation_name FROM sys.columns
     WHERE object_id = OBJECT_ID(N'planning.AdventurePlans') AND name = N'CreatorId');
DECLARE @PlanningAdventureCollation sysname =
    (SELECT collation_name FROM sys.columns
     WHERE object_id = OBJECT_ID(N'planning.AdventurePlans') AND name = N'AdventurePlanId');
IF @PlanningCreatorCollation IS NULL OR @PlanningAdventureCollation IS NULL
   OR @PlanningCreatorCollation <>
      (SELECT collation_name FROM sys.columns
       WHERE object_id = OBJECT_ID(N'planning.TravelerParticipations') AND name = N'CreatorId')
   OR @PlanningAdventureCollation <>
      (SELECT collation_name FROM sys.columns
       WHERE object_id = OBJECT_ID(N'planning.TravelerParticipations') AND name = N'AdventurePlanId')
    THROW 51000, 'The policy-assignment parent identity collations are incompatible.', 1;

CREATE TABLE planning.CompanionInformationPolicyAssignments
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    TravelerId nvarchar(64) NOT NULL,
    ProfileKey varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    ProfileDefinitionVersion bigint NOT NULL,
    ParticipationVersion bigint NOT NULL,
    AssignmentVersion bigint NOT NULL,
    Status varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    EffectiveFromUtc datetimeoffset(7) NOT NULL,
    ExpiresAtUtc datetimeoffset(7) NULL,
    RevokedAtUtc datetimeoffset(7) NULL,
    CreatedAtUtc datetimeoffset(7) NOT NULL,
    UpdatedAtUtc datetimeoffset(7) NOT NULL,
    CreatedByUserId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    UpdatedByUserId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CONSTRAINT PK_CompanionInformationPolicyAssignments
        PRIMARY KEY (CreatorId, AdventurePlanId, TravelerId),
    CONSTRAINT FK_CompanionPolicyAssignments_AdventurePlan FOREIGN KEY
        (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId),
    CONSTRAINT FK_CompanionPolicyAssignments_Participation FOREIGN KEY
        (CreatorId, AdventurePlanId, TravelerId)
        REFERENCES planning.TravelerParticipations (CreatorId, AdventurePlanId, TravelerId),
    CONSTRAINT FK_CompanionPolicyAssignments_CreatedByUser FOREIGN KEY (CreatedByUserId)
        REFERENCES auth.Users (UserId),
    CONSTRAINT FK_CompanionPolicyAssignments_UpdatedByUser FOREIGN KEY (UpdatedByUserId)
        REFERENCES auth.Users (UserId),
    CONSTRAINT CK_CompanionPolicyAssignments_Profile CHECK
        (LEN(ProfileKey) BETWEEN 3 AND 64
         AND ProfileKey = LTRIM(RTRIM(ProfileKey))
         AND ProfileKey LIKE '[a-z]%' COLLATE Latin1_General_100_BIN2
         AND ProfileKey NOT LIKE '%[^a-z0-9_]%' COLLATE Latin1_General_100_BIN2),
    CONSTRAINT CK_CompanionPolicyAssignments_Versions CHECK
        (ProfileDefinitionVersion > 0 AND ParticipationVersion > 0 AND AssignmentVersion > 0),
    CONSTRAINT CK_CompanionPolicyAssignments_Status CHECK (Status IN ('Active', 'Revoked')),
    CONSTRAINT CK_CompanionPolicyAssignments_Lifecycle CHECK
    (
        DATEPART(TZOFFSET, EffectiveFromUtc) = 0
        AND (ExpiresAtUtc IS NULL OR DATEPART(TZOFFSET, ExpiresAtUtc) = 0)
        AND (RevokedAtUtc IS NULL OR DATEPART(TZOFFSET, RevokedAtUtc) = 0)
        AND DATEPART(TZOFFSET, CreatedAtUtc) = 0
        AND DATEPART(TZOFFSET, UpdatedAtUtc) = 0
        AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > EffectiveFromUtc)
        AND UpdatedAtUtc >= CreatedAtUtc
        AND ((Status = 'Active' AND RevokedAtUtc IS NULL)
             OR (Status = 'Revoked' AND RevokedAtUtc IS NOT NULL
                 AND RevokedAtUtc >= CreatedAtUtc
                 AND RevokedAtUtc >= EffectiveFromUtc
                 AND RevokedAtUtc <= UpdatedAtUtc))
    )
);

CREATE TABLE audit.CompanionInformationPolicyAssignmentEvents
(
    AuditEventId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    SchemaVersion int NOT NULL,
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    TravelerId nvarchar(64) NOT NULL,
    Operation varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    PreviousAssignmentVersion bigint NULL,
    ResultingAssignmentVersion bigint NOT NULL,
    PreviousParticipationVersion bigint NULL,
    ResultingParticipationVersion bigint NOT NULL,
    PreviousProfileKey varchar(64) COLLATE Latin1_General_100_BIN2 NULL,
    ResultingProfileKey varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    PreviousProfileDefinitionVersion bigint NULL,
    ResultingProfileDefinitionVersion bigint NOT NULL,
    PreviousStatus varchar(16) COLLATE Latin1_General_100_BIN2 NULL,
    ResultingStatus varchar(16) COLLATE Latin1_General_100_BIN2 NOT NULL,
    PreviousEffectiveFromUtc datetimeoffset(7) NULL,
    ResultingEffectiveFromUtc datetimeoffset(7) NOT NULL,
    PreviousExpiresAtUtc datetimeoffset(7) NULL,
    ResultingExpiresAtUtc datetimeoffset(7) NULL,
    PreviousRevokedAtUtc datetimeoffset(7) NULL,
    ResultingRevokedAtUtc datetimeoffset(7) NULL,
    CONSTRAINT PK_CompanionInformationPolicyAssignmentEvents PRIMARY KEY (AuditEventId),
    CONSTRAINT FK_CompanionPolicyAssignmentEvents_AuditEvent FOREIGN KEY (AuditEventId)
        REFERENCES audit.AuditEvents (AuditEventId),
    CONSTRAINT CK_CompanionPolicyAssignmentEvents_SchemaVersion CHECK (SchemaVersion = 1),
    CONSTRAINT CK_CompanionPolicyAssignmentEvents_Operation
        CHECK (Operation IN ('Created', 'Changed', 'Expired', 'Revoked')),
    CONSTRAINT CK_CompanionPolicyAssignmentEvents_Versions CHECK
        (ResultingAssignmentVersion > 0 AND ResultingParticipationVersion > 0
         AND ResultingProfileDefinitionVersion > 0
         AND (PreviousAssignmentVersion IS NULL OR PreviousAssignmentVersion > 0)
         AND (PreviousParticipationVersion IS NULL OR PreviousParticipationVersion > 0)
         AND (PreviousProfileDefinitionVersion IS NULL OR PreviousProfileDefinitionVersion > 0)),
    CONSTRAINT CK_CompanionPolicyAssignmentEvents_CreateShape CHECK
    (
        (Operation = 'Created'
         AND PreviousAssignmentVersion IS NULL
         AND PreviousParticipationVersion IS NULL
         AND PreviousProfileKey IS NULL
         AND PreviousProfileDefinitionVersion IS NULL
         AND PreviousStatus IS NULL
         AND PreviousEffectiveFromUtc IS NULL
         AND PreviousExpiresAtUtc IS NULL
         AND PreviousRevokedAtUtc IS NULL
         AND ResultingAssignmentVersion = 1)
        OR
        (Operation <> 'Created'
         AND PreviousAssignmentVersion IS NOT NULL
         AND PreviousParticipationVersion IS NOT NULL
         AND PreviousProfileKey IS NOT NULL
         AND PreviousProfileDefinitionVersion IS NOT NULL
         AND PreviousStatus IS NOT NULL
         AND PreviousEffectiveFromUtc IS NOT NULL
         AND ResultingAssignmentVersion = PreviousAssignmentVersion + 1)
    ),
    CONSTRAINT CK_CompanionPolicyAssignmentEvents_ResultLifecycle CHECK
    (
        ResultingStatus IN ('Active', 'Revoked')
        AND DATEPART(TZOFFSET, ResultingEffectiveFromUtc) = 0
        AND (ResultingExpiresAtUtc IS NULL OR DATEPART(TZOFFSET, ResultingExpiresAtUtc) = 0)
        AND (ResultingRevokedAtUtc IS NULL OR DATEPART(TZOFFSET, ResultingRevokedAtUtc) = 0)
        AND (ResultingExpiresAtUtc IS NULL OR ResultingExpiresAtUtc > ResultingEffectiveFromUtc)
        AND ((ResultingStatus = 'Active' AND ResultingRevokedAtUtc IS NULL)
             OR (ResultingStatus = 'Revoked' AND ResultingRevokedAtUtc IS NOT NULL))
        AND ((Operation IN ('Created', 'Changed') AND ResultingStatus = 'Active')
             OR (Operation = 'Expired' AND ResultingStatus = 'Active'
                 AND ResultingExpiresAtUtc IS NOT NULL)
             OR (Operation = 'Revoked' AND ResultingStatus = 'Revoked'))
    ),
    CONSTRAINT CK_CompanionPolicyAssignmentEvents_PreviousLifecycle CHECK
    (
        PreviousStatus IS NULL
        OR
        (PreviousStatus IN ('Active', 'Revoked')
         AND DATEPART(TZOFFSET, PreviousEffectiveFromUtc) = 0
         AND (PreviousExpiresAtUtc IS NULL OR DATEPART(TZOFFSET, PreviousExpiresAtUtc) = 0)
         AND (PreviousRevokedAtUtc IS NULL OR DATEPART(TZOFFSET, PreviousRevokedAtUtc) = 0)
         AND (PreviousExpiresAtUtc IS NULL OR PreviousExpiresAtUtc > PreviousEffectiveFromUtc)
         AND ((PreviousStatus = 'Active' AND PreviousRevokedAtUtc IS NULL)
              OR (PreviousStatus = 'Revoked' AND PreviousRevokedAtUtc IS NOT NULL)))
    )
);

GRANT SELECT ON OBJECT::planning.CompanionInformationPolicyAssignments
    TO AdventuresSuiteCompanionReadRuntime;
DENY INSERT, UPDATE, DELETE ON OBJECT::planning.CompanionInformationPolicyAssignments
    TO AdventuresSuiteCompanionReadRuntime;

GRANT SELECT, INSERT, UPDATE ON OBJECT::planning.CompanionInformationPolicyAssignments
    TO AdventuresSuiteCompanionPolicyRuntime;
DENY DELETE ON OBJECT::planning.CompanionInformationPolicyAssignments
    TO AdventuresSuiteCompanionPolicyRuntime;
GRANT SELECT ON OBJECT::planning.AdventurePlans TO AdventuresSuiteCompanionPolicyRuntime;
GRANT SELECT ON OBJECT::planning.TravelerParticipations TO AdventuresSuiteCompanionPolicyRuntime;
GRANT INSERT ON OBJECT::audit.AuditEvents TO AdventuresSuiteCompanionPolicyRuntime;
GRANT INSERT ON OBJECT::audit.CompanionInformationPolicyAssignmentEvents
    TO AdventuresSuiteCompanionPolicyRuntime;
DENY UPDATE, DELETE ON OBJECT::audit.AuditEvents TO AdventuresSuiteCompanionPolicyRuntime;
DENY UPDATE, DELETE ON OBJECT::audit.CompanionInformationPolicyAssignmentEvents
    TO AdventuresSuiteCompanionPolicyRuntime;
DENY ALTER ON SCHEMA::planning TO AdventuresSuiteCompanionPolicyRuntime;
DENY ALTER ON SCHEMA::audit TO AdventuresSuiteCompanionPolicyRuntime;
