CREATE TABLE planning.Travelers
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    TravelerId nvarchar(64) NOT NULL,
    DisplayName nvarchar(200) NOT NULL,
    CONSTRAINT PK_Travelers PRIMARY KEY (CreatorId, AdventurePlanId, TravelerId),
    CONSTRAINT FK_Travelers_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId),
    CONSTRAINT CK_Travelers_DisplayName CHECK
        (LEN(DisplayName) BETWEEN 1 AND 200 AND DisplayName = LTRIM(RTRIM(DisplayName)))
);

CREATE TABLE planning.TravelerPreferences
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    TravelerId nvarchar(64) NOT NULL,
    Preference nvarchar(200) NOT NULL,
    CONSTRAINT PK_TravelerPreferences PRIMARY KEY
        (CreatorId, AdventurePlanId, TravelerId, Preference),
    CONSTRAINT FK_TravelerPreferences_Traveler FOREIGN KEY
        (CreatorId, AdventurePlanId, TravelerId)
        REFERENCES planning.Travelers (CreatorId, AdventurePlanId, TravelerId),
    CONSTRAINT CK_TravelerPreferences_Value CHECK
        (LEN(Preference) BETWEEN 1 AND 200 AND Preference = LTRIM(RTRIM(Preference)))
);

CREATE TABLE planning.DestinationVisits
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    DestinationVisitId nvarchar(64) NOT NULL,
    Name nvarchar(200) NOT NULL,
    StartDate date NOT NULL,
    EndDate date NOT NULL,
    TimeZone varchar(100) NOT NULL,
    Sequence int NOT NULL,
    Notes nvarchar(2000) NULL,
    CONSTRAINT PK_DestinationVisits PRIMARY KEY
        (CreatorId, AdventurePlanId, DestinationVisitId),
    CONSTRAINT UQ_DestinationVisits_Sequence UNIQUE
        (CreatorId, AdventurePlanId, Sequence),
    CONSTRAINT FK_DestinationVisits_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId),
    CONSTRAINT CK_DestinationVisits_Dates CHECK (EndDate >= StartDate),
    CONSTRAINT CK_DestinationVisits_Sequence CHECK (Sequence > 0)
);

CREATE TABLE planning.ItineraryDays
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    ItineraryDayId nvarchar(64) NOT NULL,
    DestinationVisitId nvarchar(64) NULL,
    LocalDate date NOT NULL,
    TimeZone varchar(100) NOT NULL,
    Title nvarchar(200) NOT NULL,
    CONSTRAINT PK_ItineraryDays PRIMARY KEY
        (CreatorId, AdventurePlanId, ItineraryDayId),
    CONSTRAINT UQ_ItineraryDays_Date UNIQUE
        (CreatorId, AdventurePlanId, LocalDate),
    CONSTRAINT FK_ItineraryDays_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId),
    CONSTRAINT FK_ItineraryDays_Visit FOREIGN KEY
        (CreatorId, AdventurePlanId, DestinationVisitId)
        REFERENCES planning.DestinationVisits
        (CreatorId, AdventurePlanId, DestinationVisitId)
);

CREATE TABLE planning.PlannedActivities
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    PlannedActivityId nvarchar(64) NOT NULL,
    ItineraryDayId nvarchar(64) NOT NULL,
    Title nvarchar(200) NOT NULL,
    StartsAtLocal time(0) NULL,
    EndsAtLocal time(0) NULL,
    Status varchar(16) NOT NULL,
    CONSTRAINT PK_PlannedActivities PRIMARY KEY
        (CreatorId, AdventurePlanId, PlannedActivityId),
    CONSTRAINT FK_PlannedActivities_Day FOREIGN KEY
        (CreatorId, AdventurePlanId, ItineraryDayId)
        REFERENCES planning.ItineraryDays (CreatorId, AdventurePlanId, ItineraryDayId),
    CONSTRAINT CK_PlannedActivities_Time CHECK
        (StartsAtLocal IS NULL OR EndsAtLocal IS NULL OR EndsAtLocal >= StartsAtLocal),
    CONSTRAINT CK_PlannedActivities_Status CHECK
        (Status IN ('Proposed', 'Reserved', 'Confirmed', 'Changed', 'Cancelled', 'Completed'))
);

CREATE TABLE planning.TransportationSegments
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    TransportationSegmentId nvarchar(64) NOT NULL,
    Mode nvarchar(100) NOT NULL,
    Origin nvarchar(200) NOT NULL,
    Destination nvarchar(200) NOT NULL,
    DepartureDate date NOT NULL,
    DepartureTimeLocal time(0) NULL,
    DepartureTimeZone varchar(100) NOT NULL,
    ArrivalDate date NOT NULL,
    ArrivalTimeLocal time(0) NULL,
    ArrivalTimeZone varchar(100) NOT NULL,
    Status varchar(16) NOT NULL,
    CONSTRAINT PK_TransportationSegments PRIMARY KEY
        (CreatorId, AdventurePlanId, TransportationSegmentId),
    CONSTRAINT FK_TransportationSegments_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId),
    CONSTRAINT CK_TransportationSegments_Dates CHECK (ArrivalDate >= DepartureDate),
    CONSTRAINT CK_TransportationSegments_Status CHECK
        (Status IN ('Proposed', 'Reserved', 'Confirmed', 'Changed', 'Cancelled', 'Completed'))
);

CREATE TABLE planning.Accommodations
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    AccommodationId nvarchar(64) NOT NULL,
    Name nvarchar(200) NOT NULL,
    StartDate date NOT NULL,
    EndDate date NOT NULL,
    TimeZone varchar(100) NOT NULL,
    Status varchar(16) NOT NULL,
    CONSTRAINT PK_Accommodations PRIMARY KEY
        (CreatorId, AdventurePlanId, AccommodationId),
    CONSTRAINT FK_Accommodations_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId),
    CONSTRAINT CK_Accommodations_Dates CHECK (EndDate >= StartDate),
    CONSTRAINT CK_Accommodations_Status CHECK
        (Status IN ('Proposed', 'Reserved', 'Confirmed', 'Changed', 'Cancelled', 'Completed'))
);

CREATE TABLE planning.Reservations
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    ReservationId nvarchar(64) NOT NULL,
    Subject nvarchar(200) NOT NULL,
    ConfirmationReference nvarchar(500) NULL,
    Status varchar(16) NOT NULL,
    CONSTRAINT PK_Reservations PRIMARY KEY
        (CreatorId, AdventurePlanId, ReservationId),
    CONSTRAINT FK_Reservations_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId),
    CONSTRAINT CK_Reservations_Status CHECK
        (Status IN ('Proposed', 'Reserved', 'Confirmed', 'Changed', 'Cancelled', 'Completed'))
);

CREATE TABLE planning.PlanningNotes
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    PlanningNoteId nvarchar(64) NOT NULL,
    NoteText nvarchar(4000) NOT NULL,
    CONSTRAINT PK_PlanningNotes PRIMARY KEY
        (CreatorId, AdventurePlanId, PlanningNoteId),
    CONSTRAINT FK_PlanningNotes_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId)
);

CREATE TABLE planning.PlanningTasks
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    PlanningTaskId nvarchar(64) NOT NULL,
    Description nvarchar(500) NOT NULL,
    DueDate date NULL,
    IsCompleted bit NOT NULL,
    CONSTRAINT PK_PlanningTasks PRIMARY KEY
        (CreatorId, AdventurePlanId, PlanningTaskId),
    CONSTRAINT FK_PlanningTasks_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId)
);

CREATE TABLE planning.BudgetItems
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    BudgetItemId nvarchar(64) NOT NULL,
    Description nvarchar(500) NOT NULL,
    Amount decimal(19, 4) NOT NULL,
    CurrencyCode char(3) NOT NULL,
    CONSTRAINT PK_BudgetItems PRIMARY KEY
        (CreatorId, AdventurePlanId, BudgetItemId),
    CONSTRAINT FK_BudgetItems_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId),
    CONSTRAINT CK_BudgetItems_Amount CHECK (Amount >= 0),
    CONSTRAINT CK_BudgetItems_Currency CHECK
        (CurrencyCode COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]')
);

CREATE TABLE planning.PackingItems
(
    CreatorId nvarchar(64) NOT NULL,
    AdventurePlanId nvarchar(64) NOT NULL,
    PackingItemId nvarchar(64) NOT NULL,
    Description nvarchar(500) NOT NULL,
    IsPacked bit NOT NULL,
    CONSTRAINT PK_PackingItems PRIMARY KEY
        (CreatorId, AdventurePlanId, PackingItemId),
    CONSTRAINT FK_PackingItems_Plan FOREIGN KEY (CreatorId, AdventurePlanId)
        REFERENCES planning.AdventurePlans (CreatorId, AdventurePlanId)
);

CREATE INDEX IX_DestinationVisits_Creator_Plan_Dates
    ON planning.DestinationVisits (CreatorId, AdventurePlanId, StartDate, EndDate);
CREATE INDEX IX_ItineraryDays_Creator_Plan_Date
    ON planning.ItineraryDays (CreatorId, AdventurePlanId, LocalDate);
CREATE INDEX IX_PlanningTasks_Creator_Plan_Completion
    ON planning.PlanningTasks (CreatorId, AdventurePlanId, IsCompleted, DueDate);
