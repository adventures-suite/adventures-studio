using AdventuresSuite.DatabaseMigrator;
using AdventuresSuite.Identity;
using AdventuresSuite.Planning.SqlServer;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies the Dapper Planning adapter against real SQL Server behavior.</summary>
public sealed class PlanningRepositoryIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";

    /// <summary>Proves a destination append, plan version, and audit commit atomically.</summary>
    [Fact]
    public async Task AddDestinationVisit_RealSqlServer_IsAtomicAndConcurrent()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteDestinationTest_{Guid.NewGuid():N}";
        var databaseConnectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE [{databaseName}];");

        try
        {
            DatabaseMigratorRunner.Migrate(databaseConnectionString);
            var factory = new SqlPlanningTransactionFactory(databaseConnectionString);
            var creator = new CreatorId("creator_destination");
            var original = CreatePlan(creator, 1, "Destination append");
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddAsync(creator, original);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanCreate, null, 1));
                await transaction.CommitAsync();
            }

            var visit = new DestinationVisit
            {
                Id = new("visit_barcelona"),
                Name = "Barcelona",
                Dates = new(new(2027, 10, 29), new(2027, 10, 30)),
                TimeZone = new("Europe/Madrid"),
                Sequence = 2
            };
            var updated = original.WithDestinationVisit(
                visit, original.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddDestinationVisitAsync(
                    creator, updated, visit, 1);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanEdit, 1, 2));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(2, loaded!.Audit.Version);
                Assert.Equal("Barcelona", loaded.DestinationVisits.Single(item => item.Sequence == 2).Name);
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.AddDestinationVisitAsync(
                        creator, updated, visit, 1));
            }

            var rollbackVisit = visit with
            {
                Id = new("visit_rollback"),
                Name = "Valencia",
                Sequence = 3
            };
            var current = updated.WithDestinationVisit(
                rollbackVisit, updated.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddDestinationVisitAsync(
                    creator, current, rollbackVisit, 2);
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(2, loaded!.Audit.Version);
                Assert.DoesNotContain(loaded.DestinationVisits, item => item.Id == rollbackVisit.Id);
                await transaction.CommitAsync();
            }
        }
        finally
        {
            await ExecuteAsync(masterConnectionString,
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    /// <summary>Proves itinerary-day append and title edit version and audit behavior atomically.</summary>
    [Fact]
    public async Task ItineraryDayMutations_RealSqlServer_AreAtomicAndConcurrent()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteItineraryDayTest_{Guid.NewGuid():N}";
        var databaseConnectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE [{databaseName}];");

        try
        {
            DatabaseMigratorRunner.Migrate(databaseConnectionString);
            var factory = new SqlPlanningTransactionFactory(databaseConnectionString);
            var creator = new CreatorId("creator_itinerary_day");
            var original = CreatePlan(creator, 1, "Itinerary day append");
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddAsync(creator, original);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanCreate, null, 1));
                await transaction.CommitAsync();
            }

            var day = new ItineraryDay
            {
                Id = new("day_madrid_two"),
                DestinationVisitId = new DestinationVisitId("visit_madrid"),
                Date = new(2027, 10, 27),
                TimeZone = new("Europe/Madrid"),
                Title = "Madrid museums"
            };
            var updated = original.WithItineraryDay(
                day, original.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddItineraryDayAsync(
                    creator, updated, day, 1);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanEdit, 1, 2));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(2, loaded!.Audit.Version);
                Assert.Equal("Madrid museums",
                    loaded.ItineraryDays.Single(item => item.Date == day.Date).Title);
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.AddItineraryDayAsync(
                        creator, updated, day, 1));
            }

            var rollbackDay = day with
            {
                Id = new("day_madrid_rollback"),
                Date = new(2027, 10, 28),
                Title = "Must roll back"
            };
            var rollbackPlan = updated.WithItineraryDay(
                rollbackDay, updated.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddItineraryDayAsync(
                    creator, rollbackPlan, rollbackDay, 2);
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(2, loaded!.Audit.Version);
                Assert.DoesNotContain(loaded.ItineraryDays, item => item.Id == rollbackDay.Id);
                await transaction.CommitAsync();
            }

            var originalDay = updated.ItineraryDays.Single(item => item.Id == new ItineraryDayId("day_madrid"));
            var originalActivity = updated.Activities.Single(item => item.ItineraryDayId == originalDay.Id);
            var editedPlan = updated.WithEditedItineraryDayTitle(
                originalDay.Id, "Madrid arrival corrected", updated.Audit.UpdatedAtUtc.AddMinutes(2));
            var editedDay = editedPlan.ItineraryDays.Single(item => item.Id == originalDay.Id);
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.UpdateItineraryDayAsync(
                    creator, editedPlan, editedDay, 2);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanEdit, 2, 3));
                await transaction.CommitAsync();
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                var persisted = loaded!.ItineraryDays.Single(item => item.Id == originalDay.Id);
                Assert.Equal(3, loaded.Audit.Version);
                Assert.Equal("Madrid arrival corrected", persisted.Title);
                Assert.Equal(originalDay.Id, persisted.Id);
                Assert.Equal(originalDay.Date, persisted.Date);
                Assert.Equal(originalDay.DestinationVisitId, persisted.DestinationVisitId);
                Assert.Equal(originalDay.TimeZone, persisted.TimeZone);
                Assert.Equal(originalActivity,
                    loaded.Activities.Single(item => item.Id == originalActivity.Id));
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.UpdateItineraryDayAsync(
                        creator, editedPlan, editedDay, 2));
            }

            var rollbackEditPlan = editedPlan.WithEditedItineraryDayTitle(
                originalDay.Id, "Must roll back", editedPlan.Audit.UpdatedAtUtc.AddMinutes(1));
            var rollbackEditedDay = rollbackEditPlan.ItineraryDays.Single(item => item.Id == originalDay.Id);
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.UpdateItineraryDayAsync(
                    creator, rollbackEditPlan, rollbackEditedDay, 3);
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(3, loaded!.Audit.Version);
                Assert.Equal("Madrid arrival corrected",
                    loaded.ItineraryDays.Single(item => item.Id == originalDay.Id).Title);
                Assert.Equal(originalActivity,
                    loaded.Activities.Single(item => item.Id == originalActivity.Id));
                await transaction.CommitAsync();
            }
        }
        finally
        {
            await ExecuteAsync(masterConnectionString,
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    /// <summary>Proves an activity append, version, and audit commit atomically.</summary>
    [Fact]
    public async Task AddPlannedActivity_RealSqlServer_IsAtomicAndConcurrent()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteActivityTest_{Guid.NewGuid():N}";
        var databaseConnectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE [{databaseName}];");

        try
        {
            DatabaseMigratorRunner.Migrate(databaseConnectionString);
            var factory = new SqlPlanningTransactionFactory(databaseConnectionString);
            var creator = new CreatorId("creator_activity");
            var original = CreatePlan(creator, 1, "Activity append");
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddAsync(creator, original);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanCreate, null, 1));
                await transaction.CommitAsync();
            }

            var activity = new PlannedActivity
            {
                Id = new("activity_reina_sofia"),
                ItineraryDayId = new("day_madrid"),
                Title = "Reina Sofía Museum",
                StartsAtLocal = new(14, 0),
                EndsAtLocal = new(16, 0),
                Status = PlanItemStatus.Proposed
            };
            var updated = original.WithPlannedActivity(
                activity, original.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddPlannedActivityAsync(
                    creator, updated, activity, 1);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanEdit, 1, 2));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(2, loaded!.Audit.Version);
                Assert.Equal("Reina Sofía Museum",
                    loaded.Activities.Single(item => item.Id == activity.Id).Title);
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.AddPlannedActivityAsync(
                        creator, updated, activity, 1));
            }

            var editedPlan = updated.WithEditedPlannedActivity(
                activity.Id, "Reina Sofía afternoon", new(15, 0), new(17, 0),
                updated.Audit.UpdatedAtUtc.AddMinutes(1));
            var editedActivity = editedPlan.Activities.Single(item => item.Id == activity.Id);
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.UpdatePlannedActivityAsync(
                    creator, editedPlan, editedActivity, 2);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanEdit, 2, 3));
                await transaction.CommitAsync();
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                var persisted = loaded!.Activities.Single(item => item.Id == activity.Id);
                Assert.Equal(3, loaded.Audit.Version);
                Assert.Equal("Reina Sofía afternoon", persisted.Title);
                Assert.Equal(activity.ItineraryDayId, persisted.ItineraryDayId);
                Assert.Equal(activity.Status, persisted.Status);
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.UpdatePlannedActivityAsync(
                        creator, editedPlan, editedActivity, 2));
            }

            var rollbackPlan = editedPlan.WithEditedPlannedActivity(
                activity.Id, "Must roll back", new(16, 0), new(18, 0),
                editedPlan.Audit.UpdatedAtUtc.AddMinutes(1));
            var rollbackActivity = rollbackPlan.Activities.Single(item => item.Id == activity.Id);
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.UpdatePlannedActivityAsync(
                    creator, rollbackPlan, rollbackActivity, 3);
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(3, loaded!.Audit.Version);
                Assert.Equal("Reina Sofía afternoon",
                    loaded.Activities.Single(item => item.Id == activity.Id).Title);
                await transaction.CommitAsync();
            }
        }
        finally
        {
            await ExecuteAsync(masterConnectionString,
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    /// <summary>Proves transportation append, version, and audit commit atomically.</summary>
    [Fact]
    public async Task AddTransportationSegment_RealSqlServer_IsAtomicAndConcurrent()
    {
        var master = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(master), $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteTransportationTest_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(master, databaseName);
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
        try
        {
            DatabaseMigratorRunner.Migrate(connectionString);
            var factory = new SqlPlanningTransactionFactory(connectionString);
            var creator = new CreatorId("creator_transportation");
            var original = CreatePlan(creator, 1, "Transportation append");
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddAsync(creator, original);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanCreate, null, 1));
                await transaction.CommitAsync();
            }

            var segment = new TransportationSegment
            {
                Id = new("transport_train"),
                Mode = "Rail",
                From = "Madrid",
                To = "Barcelona",
                DepartureDate = new(2027, 10, 28),
                DepartureTimeLocal = new(9, 0),
                DepartureTimeZone = new("Europe/Madrid"),
                ArrivalDate = new(2027, 10, 28),
                ArrivalTimeLocal = new(12, 0),
                ArrivalTimeZone = new("Europe/Madrid"),
                Status = PlanItemStatus.Proposed
            };
            var updated = original.WithTransportationSegment(
                segment, original.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddTransportationSegmentAsync(
                    creator, updated, segment, 1);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanEdit, 1, 2));
                await transaction.CommitAsync();
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(2, loaded!.Audit.Version);
                Assert.Equal("Rail", loaded.Transportation.Single(item => item.Id == segment.Id).Mode);
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.AddTransportationSegmentAsync(
                        creator, updated, segment, 1));
            }

            var editedPlan = updated.WithEditedTransportationSegment(
                segment.Id, "Express rail", "Madrid Atocha", "Barcelona Sants",
                new(2027, 10, 28), new(10, 0), new("Europe/Madrid"),
                new(2027, 10, 28), new(13, 0), new("Europe/Madrid"),
                updated.Audit.UpdatedAtUtc.AddMinutes(1));
            var editedSegment = editedPlan.Transportation.Single(item => item.Id == segment.Id);
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.UpdateTransportationSegmentAsync(
                    creator, editedPlan, editedSegment, 2);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanEdit, 2, 3));
                await transaction.CommitAsync();
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                var persisted = loaded!.Transportation.Single(item => item.Id == segment.Id);
                Assert.Equal(3, loaded.Audit.Version);
                Assert.Equal("Express rail", persisted.Mode);
                Assert.Equal("Madrid Atocha", persisted.From);
                Assert.Equal(segment.Id, persisted.Id);
                Assert.Equal(segment.Status, persisted.Status);
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.UpdateTransportationSegmentAsync(
                        creator, editedPlan, editedSegment, 2));
            }

            var rollbackPlan = editedPlan.WithEditedTransportationSegment(
                segment.Id, "Must roll back", "Private origin", "Private destination",
                new(2027, 10, 28), new(11, 0), new("Europe/Madrid"),
                new(2027, 10, 28), new(14, 0), new("Europe/Madrid"),
                editedPlan.Audit.UpdatedAtUtc.AddMinutes(1));
            var rollbackSegment = rollbackPlan.Transportation.Single(item => item.Id == segment.Id);
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.UpdateTransportationSegmentAsync(
                    creator, rollbackPlan, rollbackSegment, 3);
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(3, loaded!.Audit.Version);
                Assert.Equal("Express rail",
                    loaded.Transportation.Single(item => item.Id == segment.Id).Mode);
                await transaction.CommitAsync();
            }
        }
        finally
        {
            await ExecuteAsync(master,
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    /// <summary>Proves accommodation append, version, and audit commit atomically.</summary>
    [Fact]
    public async Task AddAccommodation_RealSqlServer_IsAtomicAndConcurrent()
    {
        var master = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(master), $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteAccommodationTest_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(master, databaseName);
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
        try
        {
            DatabaseMigratorRunner.Migrate(connectionString);
            var factory = new SqlPlanningTransactionFactory(connectionString);
            var creator = new CreatorId("creator_accommodation");
            var original = CreatePlan(creator, 1, "Accommodation append");
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddAsync(creator, original);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanCreate, null, 1));
                await transaction.CommitAsync();
            }
            var accommodation = new Accommodation
            {
                Id = new("accommodation_barcelona"),
                Name = "Barcelona hotel",
                Dates = new(new(2027, 10, 28), new(2027, 10, 29)),
                TimeZone = new("Europe/Madrid"),
                Status = PlanItemStatus.Proposed
            };
            var updated = original.WithAccommodation(
                accommodation, original.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddAccommodationAsync(
                    creator, updated, accommodation, 1);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanEdit, 1, 2));
                await transaction.CommitAsync();
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(2, loaded!.Audit.Version);
                Assert.Equal("Barcelona hotel", loaded.Accommodations.Single(item => item.Id == accommodation.Id).Name);
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.AddAccommodationAsync(creator, updated, accommodation, 1));
            }

            var editedPlan = updated.WithEditedAccommodation(
                accommodation.Id, "Barcelona Central",
                new(new(2027, 10, 27), new(2027, 10, 30)),
                new("Europe/Madrid"), updated.Audit.UpdatedAtUtc.AddMinutes(1));
            var editedAccommodation = editedPlan.Accommodations.Single(
                item => item.Id == accommodation.Id);
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.UpdateAccommodationAsync(
                    creator, editedPlan, editedAccommodation, 2);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanEdit, 2, 3));
                await transaction.CommitAsync();
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                var persisted = loaded!.Accommodations.Single(item => item.Id == accommodation.Id);
                Assert.Equal(3, loaded.Audit.Version);
                Assert.Equal("Barcelona Central", persisted.Name);
                Assert.Equal(new DateOnly(2027, 10, 27), persisted.Dates.Start);
                Assert.Equal(new DateOnly(2027, 10, 30), persisted.Dates.End);
                Assert.Equal(accommodation.Id, persisted.Id);
                Assert.Equal(accommodation.Status, persisted.Status);
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.UpdateAccommodationAsync(
                        creator, editedPlan, editedAccommodation, 2));
            }

            var auditRollbackPlan = editedPlan.WithEditedAccommodation(
                accommodation.Id, "Must roll back",
                new(new(2027, 10, 26), new(2027, 10, 31)),
                new("Europe/Madrid"), editedPlan.Audit.UpdatedAtUtc.AddMinutes(1));
            var auditRollbackAccommodation = auditRollbackPlan.Accommodations.Single(
                item => item.Id == accommodation.Id);
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.UpdateAccommodationAsync(
                    creator, auditRollbackPlan, auditRollbackAccommodation, 3);
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(3, loaded!.Audit.Version);
                Assert.Equal("Barcelona Central",
                    loaded.Accommodations.Single(item => item.Id == accommodation.Id).Name);
                await transaction.CommitAsync();
            }

            var missing = accommodation with
            {
                Id = new("accommodation_missing"),
                Name = "Must not advance parent"
            };
            var missingPlan = editedPlan.WithAccommodation(
                missing, editedPlan.Audit.UpdatedAtUtc.AddMinutes(2));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.UpdateAccommodationAsync(
                        creator, missingPlan, missing, 3));
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(3, loaded!.Audit.Version);
                Assert.DoesNotContain(loaded.Accommodations, item => item.Id == missing.Id);
                await transaction.CommitAsync();
            }

            var rollback = accommodation with { Id = new("accommodation_rollback"), Name = "Rollback" };
            var rollbackPlan = editedPlan.WithAccommodation(
                rollback, editedPlan.Audit.UpdatedAtUtc.AddMinutes(3));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddAccommodationAsync(creator, rollbackPlan, rollback, 3);
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(3, loaded!.Audit.Version);
                Assert.DoesNotContain(loaded.Accommodations, item => item.Id == rollback.Id);
                await transaction.CommitAsync();
            }
        }
        finally
        {
            await ExecuteAsync(master,
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    /// <summary>Proves credential-free reservation append, version, and audit commit atomically.</summary>
    [Fact]
    public async Task AddReservation_RealSqlServer_IsAtomicAndConcurrent()
    {
        var master = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(master), $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteReservationTest_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(master, databaseName);
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
        try
        {
            DatabaseMigratorRunner.Migrate(connectionString);
            var factory = new SqlPlanningTransactionFactory(connectionString);
            var creator = new CreatorId("creator_reservation");
            var original = CreatePlan(creator, 1, "Reservation append");
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddAsync(creator, original);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanCreate, null, 1));
                await transaction.CommitAsync();
            }
            var reservation = new Reservation
            {
                Id = new("reservation_guggenheim"),
                Subject = "Guggenheim Museum",
                ConfirmationReference = null,
                Status = PlanItemStatus.Proposed
            };
            var updated = original.WithReservation(
                reservation, original.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddReservationAsync(
                    creator, updated, reservation, 1);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    creator, original.Id, Permissions.AdventurePlanEdit, 1, 2));
                await transaction.CommitAsync();
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(2, loaded!.Audit.Version);
                var stored = loaded.Reservations.Single(item => item.Id == reservation.Id);
                Assert.Equal("Guggenheim Museum", stored.Subject);
                Assert.Null(stored.ConfirmationReference);
                var detail = await transaction.AdventurePlans.GetDetailAsync(creator, original.Id);
                Assert.Contains(detail!.Reservations,
                    item => item.Subject == "Guggenheim Museum"
                        && item.Status == PlanItemStatus.Proposed);
                Assert.Contains(detail.Reservations,
                    item => item.Subject == "Prado entry"
                        && item.Status == PlanItemStatus.Confirmed);
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.AddReservationAsync(creator, updated, reservation, 1));
            }
            var rollback = reservation with { Id = new("reservation_rollback"), Subject = "Rollback" };
            var rollbackPlan = updated.WithReservation(rollback, updated.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(creator))
            {
                await transaction.AdventurePlans.AddReservationAsync(
                    creator, rollbackPlan, rollback, 2);
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }
            await using (var transaction = await factory.BeginAsync(creator))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(creator, original.Id);
                Assert.Equal(2, loaded!.Audit.Version);
                Assert.DoesNotContain(loaded.Reservations, item => item.Id == rollback.Id);
                await transaction.CommitAsync();
            }
        }
        finally
        {
            await ExecuteAsync(master,
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    /// <summary>Proves round trips, Creator isolation, concurrency, and rollback.</summary>
    [Fact]
    public async Task Repository_RealSqlServer_PreservesPlanningBoundaries()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteRepositoryTest_{Guid.NewGuid():N}";
        var databaseConnectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE [{databaseName}];");

        try
        {
            DatabaseMigratorRunner.Migrate(databaseConnectionString);
            var factory = new SqlPlanningTransactionFactory(databaseConnectionString);
            var alpha = new CreatorId("creator_alpha");
            var beta = new CreatorId("creator_beta");
            var original = CreatePlan(alpha, 1, "Spain and Atlantic");
            var originalAuditId = new AuditEventId("audit_planning_create");

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.AddAsync(alpha, original);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, original.Id, Permissions.AdventurePlanCreate, null, 1,
                    originalAuditId));
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    transaction.AdventurePlans.GetAsync(beta, original.Id));
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    transaction.AdventurePlans.AddAsync(alpha, CreatePlan(beta, 1, "Wrong owner")));
                await transaction.CommitAsync();
            }

            await AssertSuccessfulAuditAsync(
                databaseConnectionString, originalAuditId, alpha, original.Id);

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(alpha, original.Id);
                AssertPlanGraph(original, loaded!);
                Assert.Single(await transaction.AdventurePlans.ListAsync(alpha));
                var dashboard = Assert.Single(
                    await transaction.AdventurePlans.ListDashboardAsync(alpha));
                Assert.Equal(original.Id, dashboard.Id);
                Assert.Equal(original.Audit.Version, dashboard.Version);
                Assert.False(dashboard.IsArchived);
                var facts = await transaction.AdventurePlans.GetAuthorizationFactsAsync(
                    alpha, original.Id);
                Assert.NotNull(facts);
                Assert.Equal(alpha, facts.CreatorId);
                Assert.Equal(original.Id, facts.PlanId);
                Assert.Equal(original.Audit.Version, facts.Version);
                Assert.False(facts.IsArchived);
                var detail = await transaction.AdventurePlans.GetDetailAsync(alpha, original.Id);
                Assert.NotNull(detail);
                Assert.Equal("Private working plan", detail.WorkingDescription);
                Assert.Equal(1, detail.TravelerCount);
                Assert.Equal("Madrid", Assert.Single(detail.Destinations).Name);
                Assert.Equal("Prado", Assert.Single(Assert.Single(detail.Days).Activities).Title);
                Assert.Equal("Flight", Assert.Single(detail.Transportation).Mode);
                Assert.Equal("Madrid hotel", Assert.Single(detail.Accommodations).Name);
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(beta))
            {
                Assert.Null(await transaction.AdventurePlans.GetAsync(beta, original.Id));
                Assert.Null(await transaction.AdventurePlans.GetAuthorizationFactsAsync(beta, original.Id));
                Assert.Null(await transaction.AdventurePlans.GetDetailAsync(beta, original.Id));
                var independent = CreatePlan(beta, 1, "Independent plan");
                await transaction.AdventurePlans.AddAsync(beta, independent);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    beta, independent.Id, Permissions.AdventurePlanCreate, null, 1));
                await transaction.CommitAsync();
            }

            var updated = original.WithOverview(
                "Updated Spain and Atlantic",
                original.WorkingDescription,
                original.Dates,
                original.Audit.UpdatedAtUtc.AddMinutes(1));
            var overviewAuditId = new AuditEventId("audit_planning_overview_edit");
            await ExecuteAsync(databaseConnectionString, """
                CREATE TRIGGER planning.RejectOverviewChildWrites
                ON planning.DestinationVisits
                AFTER INSERT, UPDATE, DELETE
                AS THROW 51000, 'Overview edits must not write child rows.', 1;
                """);
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.UpdateOverviewAsync(alpha, updated, 1);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, updated.Id, Permissions.AdventurePlanEdit, 1, 2,
                    overviewAuditId));
                await transaction.CommitAsync();
            }
            await ExecuteAsync(databaseConnectionString,
                "DROP TRIGGER planning.RejectOverviewChildWrites;");
            await AssertSuccessfulAuditAsync(
                databaseConnectionString, overviewAuditId, alpha, original.Id,
                Permissions.AdventurePlanEdit, previousVersion: 1, resultingVersion: 2);

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var stale = original.WithOverview(
                    "Stale write",
                    original.WorkingDescription,
                    original.Dates,
                    original.Audit.UpdatedAtUtc.AddMinutes(1));
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.UpdateOverviewAsync(alpha, stale, 1));
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(alpha, original.Id);
                AssertPlanGraph(updated, loaded!);
                await transaction.CommitAsync();
            }

            var missingEditAudit = updated.WithOverview(
                "Missing edit audit",
                updated.WorkingDescription,
                updated.Dates,
                updated.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.UpdateOverviewAsync(alpha, missingEditAudit, 2);
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(alpha, original.Id);
                AssertPlanGraph(updated, loaded!);
                await transaction.CommitAsync();
            }

            var failedAuditOverview = updated.WithOverview(
                "Duplicate audit edit",
                updated.WorkingDescription,
                updated.Dates,
                updated.Audit.UpdatedAtUtc.AddMinutes(1));
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.UpdateOverviewAsync(
                    alpha, failedAuditOverview, 2);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, failedAuditOverview.Id, Permissions.AdventurePlanEdit, 2, 3,
                    originalAuditId));
                await Assert.ThrowsAsync<SqlException>(() => transaction.CommitAsync());
            }
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(alpha, original.Id);
                AssertPlanGraph(updated, loaded!);
                await transaction.CommitAsync();
            }

            var archived = CreatePlan(alpha, 3, "Archived Spain and Atlantic",
                lifecycleStage: AdventureLifecycleStage.Remember,
                status: PlanningStatus.Archived);
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.UpdateAsync(alpha, archived, 2);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, archived.Id, Permissions.AdventurePlanArchive, 2, 3));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                Assert.Empty(await transaction.AdventurePlans.ListAsync(alpha));
                Assert.Empty(await transaction.AdventurePlans.ListDashboardAsync(alpha));
                Assert.Equal(archived.Id, Assert.Single(
                    await transaction.AdventurePlans.ListArchivedAsync(alpha)).Id);
                Assert.Equal(PlanningStatus.Archived,
                    (await transaction.AdventurePlans.GetAsync(alpha, archived.Id))!.Status);
                await transaction.CommitAsync();
            }

            var restored = CreatePlan(alpha, 4, "Restored Spain and Atlantic",
                lifecycleStage: AdventureLifecycleStage.Remember,
                status: PlanningStatus.Completed);
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.UpdateAsync(alpha, restored, 3);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, restored.Id, Permissions.AdventurePlanRestore, 3, 4));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                Assert.Equal(restored.Id, Assert.Single(
                    await transaction.AdventurePlans.ListAsync(alpha)).Id);
                Assert.Empty(await transaction.AdventurePlans.ListArchivedAsync(alpha));
                await transaction.CommitAsync();
            }

            var rollbackId = new AdventurePlanId("plan_rollback");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.AddAsync(alpha,
                    CreatePlan(alpha, 1, "Rollback plan", rollbackId));
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                Assert.Null(await transaction.AdventurePlans.GetAsync(alpha, rollbackId));
                await transaction.CommitAsync();
            }

            var missingAuditId = new AdventurePlanId("plan_missing_audit");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.AddAsync(alpha,
                    CreatePlan(alpha, 1, "Missing audit", missingAuditId));
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }

            await AssertPlanAndAuditAbsentAsync(
                databaseConnectionString, alpha, missingAuditId, auditEventId: null);

            var mismatchedAuditPlanId = new AdventurePlanId("plan_mismatched_audit");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var mismatchedAuditPlan = CreatePlan(
                    alpha, 1, "Mismatched audit", mismatchedAuditPlanId);
                await transaction.AdventurePlans.AddAsync(alpha, mismatchedAuditPlan);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, mismatchedAuditPlanId, Permissions.AdventurePlanEdit, null, 1));
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }

            await AssertPlanAndAuditAbsentAsync(
                databaseConnectionString, alpha, mismatchedAuditPlanId, auditEventId: null);

            var duplicateAuditId = new AuditEventId("audit_duplicate_planning");
            var duplicateAuditPlanId = new AdventurePlanId("plan_duplicate_audit");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var duplicateAuditPlan = CreatePlan(alpha, 1, "Audit seed", duplicateAuditPlanId);
                await transaction.AdventurePlans.AddAsync(alpha, duplicateAuditPlan);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, duplicateAuditPlanId, Permissions.AdventurePlanCreate, null, 1,
                    duplicateAuditId));
                await transaction.CommitAsync();
            }

            var auditFailurePlanId = new AdventurePlanId("plan_audit_failure");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var auditFailurePlan = CreatePlan(alpha, 1, "Audit failure", auditFailurePlanId);
                await transaction.AdventurePlans.AddAsync(alpha, auditFailurePlan);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, auditFailurePlanId, Permissions.AdventurePlanCreate, null, 1,
                    duplicateAuditId));
                await Assert.ThrowsAsync<SqlException>(() => transaction.CommitAsync());
            }

            await AssertPlanAndAuditAbsentAsync(
                databaseConnectionString, alpha, auditFailurePlanId, auditEventId: null);

            var planFailureAuditId = new AuditEventId("audit_plan_failure");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, original.Id, Permissions.AdventurePlanCreate, null, 1,
                    planFailureAuditId));
                await Assert.ThrowsAsync<SqlException>(() => transaction.AdventurePlans.AddAsync(
                    alpha, CreatePlan(alpha, 1, "Duplicate plan")));
            }

            await AssertPlanAndAuditAbsentAsync(
                databaseConnectionString, alpha, new AdventurePlanId("plan_absent_probe"),
                planFailureAuditId);

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                Assert.Throws<ArgumentException>(() => transaction.RequiredAuditIntents.AddRequired(
                    Audit(beta, original.Id, Permissions.AdventurePlanCreate, null, 1)));
            }
        }
        finally
        {
            await ExecuteAsync(masterConnectionString, $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END;
                """);
        }
    }

    private static AuditEventIntent Audit(
        CreatorId creatorId,
        AdventurePlanId planId,
        Permission permission,
        long? previousVersion,
        long resultingVersion,
        AuditEventId? auditEventId = null) => new(
            auditEventId ?? new AuditEventId($"audit_{Guid.NewGuid():N}"),
            new ActorIdentity(ActorType.Human, "actor_planner", new UserId("user_planner")),
            creatorId,
            permission,
            AuthorizationResourceScope.ForInstance(
                creatorId, AuthorizationResourceTypes.AdventurePlan, planId.Value),
            AuditOutcome.Succeeded,
            AuditReasonCategory.Completed,
            new DateTimeOffset(2026, 8, 11, 17, 0, 0, TimeSpan.Zero),
            new CorrelationId($"correlation_{Guid.NewGuid():N}"),
            previousVersion: previousVersion,
            resultingVersion: resultingVersion);

    private static async Task AssertPlanAndAuditAbsentAsync(
        string connectionString,
        CreatorId creatorId,
        AdventurePlanId planId,
        AuditEventId? auditEventId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM planning.AdventurePlans
               WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId),
              (SELECT COUNT(*) FROM audit.AuditEvents
               WHERE AuditEventId=@AuditEventId);
            """, connection);
        command.Parameters.AddWithValue("CreatorId", creatorId.Value);
        command.Parameters.AddWithValue("PlanId", planId.Value);
        command.Parameters.AddWithValue("AuditEventId", auditEventId?.Value ?? "audit_absent_probe");
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.Equal(0, reader.GetInt32(1));
    }

    private static async Task AssertSuccessfulAuditAsync(
        string connectionString,
        AuditEventId auditEventId,
        CreatorId creatorId,
        AdventurePlanId planId,
        Permission? permission = null,
        long? previousVersion = null,
        long resultingVersion = 1)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT CreatorId,ActorType,ActorUserId,Permission,ResourceType,ResourceId,
                   Outcome,ReasonCategory,OccurredAtUtc,CorrelationId,PreviousVersion,ResultingVersion
            FROM audit.AuditEvents
            WHERE AuditEventId=@AuditEventId;
            """, connection);
        command.Parameters.AddWithValue("AuditEventId", auditEventId.Value);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(creatorId.Value, reader.GetString(0));
        Assert.Equal(nameof(ActorType.Human), reader.GetString(1));
        Assert.Equal("user_planner", reader.GetString(2));
        Assert.Equal((permission ?? Permissions.AdventurePlanCreate).Value, reader.GetString(3));
        Assert.Equal(AuthorizationResourceTypes.AdventurePlan.Value, reader.GetString(4));
        Assert.Equal(planId.Value, reader.GetString(5));
        Assert.Equal(nameof(AuditOutcome.Succeeded), reader.GetString(6));
        Assert.Equal(nameof(AuditReasonCategory.Completed), reader.GetString(7));
        Assert.Equal(TimeSpan.Zero, reader.GetFieldValue<DateTimeOffset>(8).Offset);
        Assert.StartsWith("correlation_", reader.GetString(9), StringComparison.Ordinal);
        if (previousVersion.HasValue)
        {
            Assert.Equal(previousVersion.Value, reader.GetInt64(10));
        }
        else
        {
            Assert.True(reader.IsDBNull(10));
        }
        Assert.Equal(resultingVersion, reader.GetInt64(11));
    }

    private static AdventurePlan CreatePlan(
        CreatorId creatorId,
        long version,
        string title,
        AdventurePlanId? id = null,
        AdventureLifecycleStage lifecycleStage = AdventureLifecycleStage.Plan,
        PlanningStatus status = PlanningStatus.Planned)
    {
        var created = new DateTimeOffset(2026, 8, 7, 20, 0, 0, TimeSpan.Zero);
        var visitId = new DestinationVisitId("visit_madrid");
        var dayId = new ItineraryDayId("day_madrid");
        return new AdventurePlan(
            id ?? new AdventurePlanId("plan_shared"), creatorId, title, "Private working plan",
            lifecycleStage, status,
            new(new DateOnly(2027, 10, 25), new DateOnly(2027, 11, 15)),
            new(version, created, created.AddMinutes(version - 1)),
            [new Traveler { Id = new("traveler_steve"), DisplayName = "Steve", Preferences = ["Window seat"] }],
            [new DestinationVisit { Id = visitId, Name = "Madrid", Dates = new(new(2027, 10, 26), new(2027, 10, 29)), TimeZone = new("Europe/Madrid"), Sequence = 1, Notes = "Explore" }],
            [new ItineraryDay { Id = dayId, Date = new(2027, 10, 26), TimeZone = new("Europe/Madrid"), DestinationVisitId = visitId, Title = "Madrid arrival" }],
            [new PlannedActivity { Id = new("activity_prado"), ItineraryDayId = dayId, Title = "Prado", StartsAtLocal = new(10, 0), EndsAtLocal = new(12, 0), Status = PlanItemStatus.Confirmed }],
            [new TransportationSegment { Id = new("transport_flight"), Mode = "Flight", From = "Phoenix", To = "Madrid", DepartureDate = new(2027, 10, 25), DepartureTimeLocal = new(18, 0), DepartureTimeZone = new("America/Phoenix"), ArrivalDate = new(2027, 10, 26), ArrivalTimeLocal = new(13, 0), ArrivalTimeZone = new("Europe/Madrid"), Status = PlanItemStatus.Reserved }],
            [new Accommodation { Id = new("accommodation_madrid"), Name = "Madrid hotel", Dates = new(new(2027, 10, 26), new(2027, 10, 29)), TimeZone = new("Europe/Madrid"), Status = PlanItemStatus.Reserved }],
            [new Reservation { Id = new("reservation_prado"), Subject = "Prado entry", ConfirmationReference = "ABC123", Status = PlanItemStatus.Confirmed }],
            [new PlanningNote { Id = new("note_madrid"), Text = "Walkable neighborhoods" }],
            [new PlanningTask { Id = new("task_insurance"), Description = "Review insurance", DueDate = new(2027, 8, 1), IsCompleted = false }],
            [new BudgetItem { Id = new("budget_flight"), Description = "Flights", Amount = 2500m, CurrencyCode = "USD" }],
            [new PackingItem { Id = new("packing_adapter"), Description = "Power adapter", IsPacked = true }]);
    }

    private static void AssertPlanGraph(AdventurePlan expected, AdventurePlan actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.CreatorId, actual.CreatorId);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Audit, actual.Audit);
        Assert.Single(actual.Travelers);
        Assert.Equal("Window seat", Assert.Single(actual.Travelers[0].Preferences));
        Assert.Single(actual.DestinationVisits);
        Assert.Single(actual.ItineraryDays);
        Assert.Single(actual.Activities);
        Assert.Single(actual.Transportation);
        Assert.Single(actual.Accommodations);
        Assert.Single(actual.Reservations);
        Assert.Single(actual.Notes);
        Assert.Single(actual.Tasks);
        Assert.Single(actual.BudgetItems);
        Assert.Single(actual.PackingItems);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildDatabaseConnectionString(string masterConnectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(masterConnectionString) { InitialCatalog = databaseName };
        return builder.ConnectionString;
    }
}
