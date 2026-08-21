ALTER TABLE planning.TransportationSegments
    ADD DepartureDestinationVisitId nvarchar(64) NULL,
        ArrivalDestinationVisitId nvarchar(64) NULL;

ALTER TABLE planning.Accommodations
    ADD DestinationVisitId nvarchar(64) NULL;

ALTER TABLE planning.Reservations
    ADD DestinationVisitId nvarchar(64) NULL;

EXEC(N'ALTER TABLE planning.TransportationSegments ADD CONSTRAINT FK_TransportationSegments_DepartureDestinationVisit
    FOREIGN KEY (CreatorId, AdventurePlanId, DepartureDestinationVisitId)
    REFERENCES planning.DestinationVisits (CreatorId, AdventurePlanId, DestinationVisitId);

ALTER TABLE planning.TransportationSegments ADD CONSTRAINT FK_TransportationSegments_ArrivalDestinationVisit
    FOREIGN KEY (CreatorId, AdventurePlanId, ArrivalDestinationVisitId)
    REFERENCES planning.DestinationVisits (CreatorId, AdventurePlanId, DestinationVisitId);

ALTER TABLE planning.Accommodations ADD CONSTRAINT FK_Accommodations_DestinationVisit
    FOREIGN KEY (CreatorId, AdventurePlanId, DestinationVisitId)
    REFERENCES planning.DestinationVisits (CreatorId, AdventurePlanId, DestinationVisitId);

ALTER TABLE planning.Reservations ADD CONSTRAINT FK_Reservations_DestinationVisit
    FOREIGN KEY (CreatorId, AdventurePlanId, DestinationVisitId)
    REFERENCES planning.DestinationVisits (CreatorId, AdventurePlanId, DestinationVisitId);

CREATE INDEX IX_TransportationSegments_DepartureDestinationVisit
    ON planning.TransportationSegments (CreatorId, AdventurePlanId, DepartureDestinationVisitId)
    WHERE DepartureDestinationVisitId IS NOT NULL;

CREATE INDEX IX_TransportationSegments_ArrivalDestinationVisit
    ON planning.TransportationSegments (CreatorId, AdventurePlanId, ArrivalDestinationVisitId)
    WHERE ArrivalDestinationVisitId IS NOT NULL;

CREATE INDEX IX_Accommodations_DestinationVisit
    ON planning.Accommodations (CreatorId, AdventurePlanId, DestinationVisitId)
    WHERE DestinationVisitId IS NOT NULL;');

CREATE INDEX IX_Reservations_DestinationVisit
    ON planning.Reservations (CreatorId, AdventurePlanId, DestinationVisitId)
    WHERE DestinationVisitId IS NOT NULL;
