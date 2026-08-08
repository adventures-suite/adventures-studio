CREATE TABLE planning.AdventurePlans
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    Title nvarchar(200) NOT NULL,
    WorkingDescription nvarchar(2000) NULL,
    LifecycleStage varchar(16) NOT NULL,
    PlanningStatus varchar(16) NOT NULL,
    StartDate date NOT NULL,
    EndDate date NOT NULL,
    Version bigint NOT NULL,
    CreatedAtUtc datetimeoffset(0) NOT NULL,
    UpdatedAtUtc datetimeoffset(0) NOT NULL,
    CONSTRAINT PK_AdventurePlans PRIMARY KEY (CreatorId, AdventurePlanId),
    CONSTRAINT CK_AdventurePlans_CreatorId CHECK (LEN(CreatorId) BETWEEN 3 AND 64),
    CONSTRAINT CK_AdventurePlans_PlanId CHECK (LEN(AdventurePlanId) BETWEEN 3 AND 64),
    CONSTRAINT CK_AdventurePlans_Title CHECK (LEN(Title) BETWEEN 1 AND 200 AND Title = LTRIM(RTRIM(Title))),
    CONSTRAINT CK_AdventurePlans_Lifecycle CHECK
        (LifecycleStage IN ('Dream', 'Plan', 'Travel', 'Preserve', 'Publish', 'Share', 'Remember')),
    CONSTRAINT CK_AdventurePlans_Status CHECK
        (PlanningStatus IN ('Idea', 'Draft', 'Planned', 'Upcoming', 'InProgress', 'Completed', 'Archived')),
    CONSTRAINT CK_AdventurePlans_Dates CHECK (EndDate >= StartDate),
    CONSTRAINT CK_AdventurePlans_Version CHECK (Version > 0),
    CONSTRAINT CK_AdventurePlans_Audit CHECK
        (DATEPART(TZOFFSET, CreatedAtUtc) = 0
         AND DATEPART(TZOFFSET, UpdatedAtUtc) = 0
         AND UpdatedAtUtc >= CreatedAtUtc)
);

CREATE INDEX IX_AdventurePlans_Creator_Status_Dates
    ON planning.AdventurePlans (CreatorId, PlanningStatus, StartDate, EndDate);
