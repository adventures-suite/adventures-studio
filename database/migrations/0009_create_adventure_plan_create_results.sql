CREATE TABLE planning.AdventurePlanCreateResults
(
    CreatorId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Operation varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    IdempotencyKey nvarchar(128) COLLATE Latin1_General_100_BIN2 NOT NULL,
    FingerprintVersion int NOT NULL,
    RequestFingerprint binary(32) NOT NULL,
    AdventurePlanId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    ResultingVersion bigint NOT NULL,
    CreatedAtUtc datetimeoffset(7) NOT NULL,
    ExpiresAtUtc datetimeoffset(7) NOT NULL,
    CONSTRAINT PK_AdventurePlanCreateResults
        PRIMARY KEY (CreatorId, Operation, IdempotencyKey),
    CONSTRAINT CK_AdventurePlanCreateResults_CreatorId CHECK
        (LEN(CreatorId) BETWEEN 3 AND 64
         AND CreatorId LIKE '[a-z]%' COLLATE Latin1_General_100_BIN2
         AND CreatorId NOT LIKE '%[^a-z0-9_]%' COLLATE Latin1_General_100_BIN2),
    CONSTRAINT CK_AdventurePlanCreateResults_Operation CHECK
        (Operation = 'AdventurePlan.Create.v1'),
    CONSTRAINT CK_AdventurePlanCreateResults_IdempotencyKey CHECK
        (LEN(IdempotencyKey) BETWEEN 16 AND 128
         AND IdempotencyKey = LTRIM(RTRIM(IdempotencyKey))),
    CONSTRAINT CK_AdventurePlanCreateResults_FingerprintVersion CHECK
        (FingerprintVersion > 0),
    CONSTRAINT CK_AdventurePlanCreateResults_Result CHECK
        (LEN(AdventurePlanId) BETWEEN 3 AND 64 AND ResultingVersion = 1),
    CONSTRAINT CK_AdventurePlanCreateResults_Time CHECK
        (DATEPART(TZOFFSET, CreatedAtUtc) = 0
         AND DATEPART(TZOFFSET, ExpiresAtUtc) = 0
         AND ExpiresAtUtc > CreatedAtUtc)
);

CREATE INDEX IX_AdventurePlanCreateResults_Expiry
    ON planning.AdventurePlanCreateResults (ExpiresAtUtc, CreatorId);

IF DATABASE_PRINCIPAL_ID(N'AdventuresSuitePlanningRuntime') IS NULL
BEGIN
    CREATE ROLE AdventuresSuitePlanningRuntime AUTHORIZATION dbo;
END;

GRANT SELECT, INSERT ON OBJECT::planning.AdventurePlanCreateResults
    TO AdventuresSuitePlanningRuntime;
DENY UPDATE, DELETE ON OBJECT::planning.AdventurePlanCreateResults
    TO AdventuresSuitePlanningRuntime;
DENY ALTER ON SCHEMA::planning TO AdventuresSuitePlanningRuntime;
