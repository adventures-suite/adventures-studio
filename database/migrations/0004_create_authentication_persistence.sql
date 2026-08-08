IF SCHEMA_ID(N'auth') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA auth AUTHORIZATION dbo;');
END;

CREATE TABLE auth.Users
(
    UserId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Status varchar(16) NOT NULL,
    SecurityVersion bigint NOT NULL,
    CreatedAtUtc datetimeoffset(7) NOT NULL,
    UpdatedAtUtc datetimeoffset(7) NOT NULL,
    DisabledAtUtc datetimeoffset(7) NULL,
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT CK_Users_Status CHECK (Status IN ('Onboarding', 'Active', 'Disabled')),
    CONSTRAINT CK_Users_SecurityVersion CHECK (SecurityVersion > 0),
    CONSTRAINT CK_Users_Lifecycle CHECK
    (
        DATEPART(TZOFFSET, CreatedAtUtc) = 0
        AND DATEPART(TZOFFSET, UpdatedAtUtc) = 0
        AND (DisabledAtUtc IS NULL OR DATEPART(TZOFFSET, DisabledAtUtc) = 0)
        AND UpdatedAtUtc >= CreatedAtUtc
        AND (DisabledAtUtc IS NULL OR DisabledAtUtc BETWEEN CreatedAtUtc AND UpdatedAtUtc)
        AND ((Status = 'Disabled' AND DisabledAtUtc IS NOT NULL)
             OR (Status <> 'Disabled' AND DisabledAtUtc IS NULL))
    )
);

CREATE TABLE auth.ExternalIdentities
(
    ExternalIdentityId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    UserId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Provider varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Issuer nvarchar(2048) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Subject nvarchar(255) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CreatedAtUtc datetimeoffset(7) NOT NULL,
    LastAuthenticatedAtUtc datetimeoffset(7) NULL,
    DisabledAtUtc datetimeoffset(7) NULL,
    IdentityKeyHash AS CONVERT(binary(32), HASHBYTES(
        'SHA2_256',
        CONCAT(
            DATALENGTH(Provider), ':', Provider, '|',
            DATALENGTH(Issuer), ':', Issuer, '|',
            DATALENGTH(Subject), ':', Subject))) PERSISTED,
    CONSTRAINT PK_ExternalIdentities PRIMARY KEY (ExternalIdentityId),
    CONSTRAINT FK_ExternalIdentities_User FOREIGN KEY (UserId)
        REFERENCES auth.Users (UserId),
    CONSTRAINT UQ_ExternalIdentities_ExactKeyHash UNIQUE (IdentityKeyHash),
    CONSTRAINT CK_ExternalIdentities_Lifecycle CHECK
    (
        DATEPART(TZOFFSET, CreatedAtUtc) = 0
        AND (LastAuthenticatedAtUtc IS NULL OR DATEPART(TZOFFSET, LastAuthenticatedAtUtc) = 0)
        AND (DisabledAtUtc IS NULL OR DATEPART(TZOFFSET, DisabledAtUtc) = 0)
        AND (LastAuthenticatedAtUtc IS NULL OR LastAuthenticatedAtUtc >= CreatedAtUtc)
        AND (DisabledAtUtc IS NULL OR DisabledAtUtc >= CreatedAtUtc)
        AND (LastAuthenticatedAtUtc IS NULL OR DisabledAtUtc IS NULL
             OR LastAuthenticatedAtUtc <= DisabledAtUtc)
    )
);

CREATE INDEX IX_ExternalIdentities_User
    ON auth.ExternalIdentities (UserId, DisabledAtUtc);

CREATE TABLE auth.UserSessions
(
    UserSessionId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    UserId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    SecurityVersion bigint NOT NULL,
    CreatedAtUtc datetimeoffset(7) NOT NULL,
    LastSeenAtUtc datetimeoffset(7) NOT NULL,
    AbsoluteExpiresAtUtc datetimeoffset(7) NOT NULL,
    RevokedAtUtc datetimeoffset(7) NULL,
    RevocationReason varchar(32) NULL,
    CONSTRAINT PK_UserSessions PRIMARY KEY (UserSessionId),
    CONSTRAINT FK_UserSessions_User FOREIGN KEY (UserId)
        REFERENCES auth.Users (UserId),
    CONSTRAINT CK_UserSessions_SecurityVersion CHECK (SecurityVersion > 0),
    CONSTRAINT CK_UserSessions_RevocationReason CHECK
    (
        RevocationReason IS NULL OR RevocationReason IN
        ('SignedOut', 'SignedOutEverywhere', 'UserDisabled',
         'SecurityVersionChanged', 'IdentityCompromised')
    ),
    CONSTRAINT CK_UserSessions_Lifecycle CHECK
    (
        DATEPART(TZOFFSET, CreatedAtUtc) = 0
        AND DATEPART(TZOFFSET, LastSeenAtUtc) = 0
        AND DATEPART(TZOFFSET, AbsoluteExpiresAtUtc) = 0
        AND (RevokedAtUtc IS NULL OR DATEPART(TZOFFSET, RevokedAtUtc) = 0)
        AND LastSeenAtUtc >= CreatedAtUtc
        AND LastSeenAtUtc < AbsoluteExpiresAtUtc
        AND (RevokedAtUtc IS NULL OR RevokedAtUtc >= LastSeenAtUtc)
        AND ((RevokedAtUtc IS NULL AND RevocationReason IS NULL)
             OR (RevokedAtUtc IS NOT NULL AND RevocationReason IS NOT NULL))
    )
);

CREATE INDEX IX_UserSessions_User_Revocation_Expiry
    ON auth.UserSessions (UserId, RevokedAtUtc, AbsoluteExpiresAtUtc)
    INCLUDE (SecurityVersion, LastSeenAtUtc);

IF DATABASE_PRINCIPAL_ID(N'AdventuresSuiteAuthenticationRuntime') IS NULL
BEGIN
    CREATE ROLE AdventuresSuiteAuthenticationRuntime AUTHORIZATION dbo;
END;

GRANT SELECT, INSERT, UPDATE ON SCHEMA::auth
    TO AdventuresSuiteAuthenticationRuntime;
DENY ALTER ON SCHEMA::auth
    TO AdventuresSuiteAuthenticationRuntime;
