CREATE TABLE planning.TravelerParticipations
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    TravelerId nvarchar(64) NOT NULL,
    UserId nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Status varchar(16) NOT NULL,
    Version bigint NOT NULL,
    EffectiveFromUtc datetimeoffset(7) NOT NULL,
    ExpiresAtUtc datetimeoffset(7) NULL,
    CreatedAtUtc datetimeoffset(7) NOT NULL,
    UpdatedAtUtc datetimeoffset(7) NOT NULL,
    CONSTRAINT PK_TravelerParticipations
        PRIMARY KEY (CreatorId, AdventurePlanId, UserId),
    CONSTRAINT UQ_TravelerParticipations_Traveler
        UNIQUE (CreatorId, AdventurePlanId, TravelerId),
    CONSTRAINT FK_TravelerParticipations_Traveler FOREIGN KEY
        (CreatorId, AdventurePlanId, TravelerId)
        REFERENCES planning.Travelers (CreatorId, AdventurePlanId, TravelerId),
    CONSTRAINT FK_TravelerParticipations_User FOREIGN KEY (UserId)
        REFERENCES auth.Users (UserId),
    CONSTRAINT CK_TravelerParticipations_Status
        CHECK (Status IN ('Invited', 'Accepted', 'Revoked')),
    CONSTRAINT CK_TravelerParticipations_Version CHECK (Version > 0),
    CONSTRAINT CK_TravelerParticipations_Lifecycle CHECK
    (
        DATEPART(TZOFFSET, EffectiveFromUtc) = 0
        AND (ExpiresAtUtc IS NULL OR DATEPART(TZOFFSET, ExpiresAtUtc) = 0)
        AND DATEPART(TZOFFSET, CreatedAtUtc) = 0
        AND DATEPART(TZOFFSET, UpdatedAtUtc) = 0
        AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > EffectiveFromUtc)
        AND UpdatedAtUtc >= CreatedAtUtc
    )
);

CREATE INDEX IX_TravelerParticipations_AuthorizedList
    ON planning.TravelerParticipations
        (CreatorId, UserId, Status, EffectiveFromUtc, ExpiresAtUtc, AdventurePlanId)
    INCLUDE (TravelerId, Version, UpdatedAtUtc);

-- Deliberately no runtime grant. The deployed API remains disconnected until
-- its Managed Identity and narrowly scoped read role are separately approved.
