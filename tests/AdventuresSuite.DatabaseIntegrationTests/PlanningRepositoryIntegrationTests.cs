using AdventuresSuite.DatabaseMigrator;
using AdventuresSuite.Planning.SqlServer;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies the Dapper Planning adapter against real SQL Server behavior.</summary>
public sealed class PlanningRepositoryIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";

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

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.AddAsync(alpha, original);
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    transaction.AdventurePlans.GetAsync(beta, original.Id));
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    transaction.AdventurePlans.AddAsync(alpha, CreatePlan(beta, 1, "Wrong owner")));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(alpha, original.Id);
                AssertPlanGraph(original, loaded!);
                Assert.Single(await transaction.AdventurePlans.ListAsync(alpha));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(beta))
            {
                Assert.Null(await transaction.AdventurePlans.GetAsync(beta, original.Id));
                await transaction.AdventurePlans.AddAsync(beta, CreatePlan(beta, 1, "Independent plan"));
                await transaction.CommitAsync();
            }

            var updated = CreatePlan(alpha, 2, "Updated Spain and Atlantic");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.UpdateAsync(alpha, updated, 1);
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var stale = CreatePlan(alpha, 2, "Stale write");
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.UpdateAsync(alpha, stale, 1));
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(alpha, original.Id);
                Assert.Equal("Updated Spain and Atlantic", loaded!.Title);
                Assert.Equal(2, loaded.Audit.Version);
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

    private static AdventurePlan CreatePlan(
        CreatorId creatorId,
        long version,
        string title,
        AdventurePlanId? id = null)
    {
        var created = new DateTimeOffset(2026, 8, 7, 20, 0, 0, TimeSpan.Zero);
        var visitId = new DestinationVisitId("visit_madrid");
        var dayId = new ItineraryDayId("day_madrid");
        return new AdventurePlan(
            id ?? new AdventurePlanId("plan_shared"), creatorId, title, "Private working plan",
            AdventureLifecycleStage.Plan, PlanningStatus.Planned,
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
