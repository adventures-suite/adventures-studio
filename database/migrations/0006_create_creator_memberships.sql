IF SCHEMA_ID(N'audit') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA audit AUTHORIZATION db_ddladmin;');
END;

CREATE TABLE auth.CreatorMemberships
(
    CreatorId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CreatorMembershipId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    UserId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Status varchar(16) NOT NULL,
    Version bigint NOT NULL,
    EffectiveFromUtc datetimeoffset(7) NOT NULL,
    ExpiresAtUtc datetimeoffset(7) NULL,
    CreatedAtUtc datetimeoffset(7) NOT NULL,
    UpdatedAtUtc datetimeoffset(7) NOT NULL,
    CreatedByUserId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    UpdatedByUserId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CONSTRAINT PK_CreatorMemberships PRIMARY KEY (CreatorId, CreatorMembershipId),
    CONSTRAINT UQ_CreatorMemberships_Creator_User UNIQUE (CreatorId, UserId),
    CONSTRAINT FK_CreatorMemberships_User FOREIGN KEY (UserId) REFERENCES auth.Users (UserId),
    CONSTRAINT FK_CreatorMemberships_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES auth.Users (UserId),
    CONSTRAINT FK_CreatorMemberships_UpdatedByUser FOREIGN KEY (UpdatedByUserId) REFERENCES auth.Users (UserId),
    CONSTRAINT CK_CreatorMemberships_CreatorId CHECK
        (LEN(CreatorId) BETWEEN 3 AND 64
         AND CreatorId LIKE '[a-z]%' COLLATE Latin1_General_100_BIN2
         AND CreatorId NOT LIKE '%[^a-z0-9_]%' COLLATE Latin1_General_100_BIN2),
    CONSTRAINT CK_CreatorMemberships_MembershipId CHECK
        (LEN(CreatorMembershipId) BETWEEN 3 AND 64
         AND CreatorMembershipId LIKE '[a-z]%' COLLATE Latin1_General_100_BIN2
         AND CreatorMembershipId NOT LIKE '%[^a-z0-9_]%' COLLATE Latin1_General_100_BIN2),
    CONSTRAINT CK_CreatorMemberships_Status CHECK (Status IN ('Pending', 'Active', 'Disabled', 'Revoked')),
    CONSTRAINT CK_CreatorMemberships_Version CHECK (Version > 0),
    CONSTRAINT CK_CreatorMemberships_Lifecycle CHECK
    (
        DATEPART(TZOFFSET, EffectiveFromUtc) = 0
        AND (ExpiresAtUtc IS NULL OR DATEPART(TZOFFSET, ExpiresAtUtc) = 0)
        AND DATEPART(TZOFFSET, CreatedAtUtc) = 0
        AND DATEPART(TZOFFSET, UpdatedAtUtc) = 0
        AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > EffectiveFromUtc)
        AND UpdatedAtUtc >= CreatedAtUtc
    )
);

CREATE INDEX IX_CreatorMemberships_Creator_Status_Effective
    ON auth.CreatorMemberships (CreatorId, Status, EffectiveFromUtc, ExpiresAtUtc)
    INCLUDE (UserId, Version);

CREATE TABLE auth.CreatorMembershipRoles
(
    CreatorId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CreatorMembershipId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Role varchar(32) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CONSTRAINT PK_CreatorMembershipRoles PRIMARY KEY (CreatorId, CreatorMembershipId, Role),
    CONSTRAINT FK_CreatorMembershipRoles_Membership FOREIGN KEY (CreatorId, CreatorMembershipId)
        REFERENCES auth.CreatorMemberships (CreatorId, CreatorMembershipId),
    CONSTRAINT CK_CreatorMembershipRoles_Role CHECK
        (Role IN ('Owner', 'Administrator', 'Planner', 'Contributor', 'Viewer'))
);

CREATE TABLE auth.CreatorMembershipPermissionGrants
(
    CreatorId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CreatorMembershipId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Permission varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CONSTRAINT PK_CreatorMembershipPermissionGrants
        PRIMARY KEY (CreatorId, CreatorMembershipId, Permission),
    CONSTRAINT FK_CreatorMembershipPermissionGrants_Membership FOREIGN KEY (CreatorId, CreatorMembershipId)
        REFERENCES auth.CreatorMemberships (CreatorId, CreatorMembershipId),
    CONSTRAINT CK_CreatorMembershipPermissionGrants_Permission CHECK
        (LEN(Permission) BETWEEN 3 AND 100 AND Permission LIKE '%.%' COLLATE Latin1_General_100_BIN2)
);

CREATE TABLE audit.AuditEvents
(
    AuditEventId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    SchemaVersion int NOT NULL CONSTRAINT DF_AuditEvents_SchemaVersion DEFAULT (1),
    CreatorId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    ActorType varchar(32) NOT NULL,
    ActorUserId nvarchar(64) COLLATE Latin1_General_100_BIN2 NULL,
    Permission varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    ResourceType varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    ResourceId nvarchar(128) COLLATE Latin1_General_100_BIN2 NULL,
    Outcome varchar(16) NOT NULL,
    ReasonCategory varchar(32) NOT NULL,
    OccurredAtUtc datetimeoffset(7) NOT NULL,
    RecordedAtUtc datetimeoffset(7) NOT NULL CONSTRAINT DF_AuditEvents_RecordedAtUtc DEFAULT (SYSUTCDATETIME()),
    CorrelationId nvarchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
    PreviousVersion bigint NULL,
    ResultingVersion bigint NULL,
    CONSTRAINT PK_AuditEvents PRIMARY KEY (AuditEventId),
    CONSTRAINT CK_AuditEvents_SchemaVersion CHECK (SchemaVersion = 1),
    CONSTRAINT CK_AuditEvents_ActorType CHECK (ActorType IN ('Human', 'System', 'BackgroundJob', 'Support')),
    CONSTRAINT CK_AuditEvents_Outcome CHECK (Outcome IN ('Succeeded', 'Rejected', 'Failed')),
    CONSTRAINT CK_AuditEvents_Time CHECK
        (DATEPART(TZOFFSET, OccurredAtUtc) = 0 AND DATEPART(TZOFFSET, RecordedAtUtc) = 0),
    CONSTRAINT CK_AuditEvents_Versions CHECK
        ((PreviousVersion IS NULL OR PreviousVersion > 0)
         AND (ResultingVersion IS NULL OR ResultingVersion > 0)
         AND (PreviousVersion IS NULL OR ResultingVersion IS NULL OR ResultingVersion > PreviousVersion))
);

CREATE INDEX IX_AuditEvents_Creator_Occurred
    ON audit.AuditEvents (CreatorId, OccurredAtUtc, AuditEventId);

IF DATABASE_PRINCIPAL_ID(N'AdventuresSuiteMembershipRuntime') IS NULL
BEGIN
    CREATE ROLE AdventuresSuiteMembershipRuntime AUTHORIZATION dbo;
END;

GRANT SELECT, INSERT, UPDATE ON OBJECT::auth.CreatorMemberships TO AdventuresSuiteMembershipRuntime;
DENY DELETE ON OBJECT::auth.CreatorMemberships TO AdventuresSuiteMembershipRuntime;
GRANT SELECT, INSERT, DELETE ON OBJECT::auth.CreatorMembershipRoles TO AdventuresSuiteMembershipRuntime;
GRANT SELECT, INSERT, DELETE ON OBJECT::auth.CreatorMembershipPermissionGrants TO AdventuresSuiteMembershipRuntime;
GRANT SELECT, INSERT ON OBJECT::audit.AuditEvents TO AdventuresSuiteMembershipRuntime;
DENY UPDATE, DELETE ON OBJECT::audit.AuditEvents TO AdventuresSuiteMembershipRuntime;
DENY ALTER ON SCHEMA::auth TO AdventuresSuiteMembershipRuntime;
DENY ALTER ON SCHEMA::audit TO AdventuresSuiteMembershipRuntime;
