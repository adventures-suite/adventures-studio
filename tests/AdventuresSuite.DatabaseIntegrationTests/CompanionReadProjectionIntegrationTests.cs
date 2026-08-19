extern alias api;

using AdventuresSuite.Companion.Application;
using AdventuresSuite.Companion.SqlServer;
using AdventuresSuite.DatabaseMigrator;
using AdventuresSuite.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies Companion projections against real SQL Server authorization behavior.</summary>
public sealed class CompanionReadProjectionIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";

    /// <summary>Proves authoritative scope, lifecycle, bounds, ordering, and query access paths.</summary>
    [Fact]
    public async Task Queries_RealSqlServer_PreserveTravelerAndCreatorIsolation()
    {
        var master = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(master), $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteCompanionReadTest_{Guid.NewGuid():N}";
        var connectionString = BuildConnectionString(master, databaseName);
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");

        try
        {
            await CompanionPolicyMigrationTestHarness.MigrateAllAsync(connectionString);
            await SeedAsync(connectionString);
            var queries = new SqlCompanionAdventureQueries(connectionString);
            var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
            var alpha = Scope("creator_alpha", "user_alpha", 3, now);

            var active = await queries.ListAsync(alpha, 10, includeCompleted: false);
            Assert.Equal(["plan_active", "plan_upcoming"], active.Select(value => value.AdventureId));
            Assert.Equal(CompanionAdventureLifecycle.InProgress, active[0].Lifecycle);
            Assert.Equal(CompanionAdventureLifecycle.Committed, active[1].Lifecycle);
            Assert.All(active, value => Assert.Equal("traveler_alpha", value.TravelerId));

            var bounded = await queries.ListAsync(alpha, 1, includeCompleted: true);
            Assert.Single(bounded);
            Assert.Equal("plan_completed", bounded[0].AdventureId);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                queries.ListAsync(alpha, CompanionReadProjectionLimits.MaximumAdventures + 1, false));

            var detail = await queries.GetAsync(alpha, "plan_active");
            Assert.NotNull(detail);
            Assert.Equal(11, detail.Adventure.PlanVersion);
            Assert.Equal(5, detail.Adventure.ParticipationVersion);
            Assert.Equal(["visit_first", "visit_second"],
                detail.Destinations.Select(value => value.DestinationVisitId));
            Assert.Equal("Europe/Rome", detail.Adventure.PrimaryTimeZone);

            await VerifyHttpEndpointsAsync(connectionString);

            Assert.Null(await queries.GetAsync(alpha, "plan_other_creator"));
            Assert.Null(await queries.GetAsync(alpha, "plan_other_traveler"));
            Assert.Null(await queries.GetAsync(alpha, "plan_archived"));
            Assert.Empty(await queries.ListAsync(Scope("creator_beta", "user_alpha", 3, now), 10, true));
            Assert.Empty(await queries.ListAsync(Scope("creator_alpha", "user_alpha", 2, now), 10, true));

            await ExecuteAsync(connectionString, """
                UPDATE planning.TravelerParticipations
                SET Status = 'Revoked', Version = Version + 1, UpdatedAtUtc = '2026-08-10T12:01:00+00:00'
                WHERE CreatorId = 'creator_alpha' AND AdventurePlanId = 'plan_active'
                  AND UserId = 'user_alpha';
                """);
            Assert.Null(await queries.GetAsync(alpha, "plan_active"));

            await VerifyIndexesAsync(connectionString);
            await VerifyEstimatedPlanAsync(connectionString);
        }
        finally
        {
            await ExecuteAsync(master, $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END;
                """);
        }
    }

    private static CompanionAdventureReadScope Scope(
        string creatorId, string userId, long version, DateTimeOffset now) =>
        new(new CreatorId(creatorId), new UserId(userId), version, now);

    private static async Task SeedAsync(string connectionString) => await ExecuteAsync(connectionString, """
        INSERT auth.Users (UserId, Status, SecurityVersion, CreatedAtUtc, UpdatedAtUtc, DisabledAtUtc)
        VALUES ('user_alpha', 'Active', 1, '2026-08-01T00:00:00+00:00', '2026-08-01T00:00:00+00:00', NULL),
               ('user_other', 'Active', 1, '2026-08-01T00:00:00+00:00', '2026-08-01T00:00:00+00:00', NULL);

        INSERT auth.CreatorMemberships
            (CreatorId, CreatorMembershipId, UserId, Status, Version, EffectiveFromUtc,
             ExpiresAtUtc, CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId)
        VALUES ('creator_alpha', 'membership_alpha', 'user_alpha', 'Active', 3,
                '2026-08-01T00:00:00+00:00', NULL, '2026-08-01T00:00:00+00:00',
                '2026-08-01T00:00:00+00:00', 'user_alpha', 'user_alpha');

        INSERT auth.CreatorMembershipRoles (CreatorId, CreatorMembershipId, Role)
        VALUES ('creator_alpha', 'membership_alpha', 'Viewer');

        INSERT planning.AdventurePlans
            (CreatorId, AdventurePlanId, Title, WorkingDescription, LifecycleStage,
             PlanningStatus, StartDate, EndDate, Version, CreatedAtUtc, UpdatedAtUtc)
        VALUES
            ('creator_alpha', 'plan_completed', 'Completed', NULL, 'Remember', 'Completed', '2025-01-01', '2025-01-03', 2, '2026-08-01T00:00:00+00:00', '2026-08-02T00:00:00+00:00'),
            ('creator_alpha', 'plan_active', 'Active', 'Safe description', 'Travel', 'InProgress', '2026-08-09', '2026-08-16', 11, '2026-08-01T00:00:00+00:00', '2026-08-10T10:00:00+00:00'),
            ('creator_alpha', 'plan_upcoming', 'Upcoming', NULL, 'Plan', 'Upcoming', '2027-01-01', '2027-01-05', 4, '2026-08-01T00:00:00+00:00', '2026-08-03T00:00:00+00:00'),
            ('creator_alpha', 'plan_archived', 'Archived', NULL, 'Remember', 'Archived', '2024-01-01', '2024-01-02', 6, '2026-08-01T00:00:00+00:00', '2026-08-03T00:00:00+00:00'),
            ('creator_alpha', 'plan_other_traveler', 'Private other', NULL, 'Plan', 'Planned', '2028-01-01', '2028-01-02', 1, '2026-08-01T00:00:00+00:00', '2026-08-03T00:00:00+00:00'),
            ('creator_beta', 'plan_other_creator', 'Other Creator', NULL, 'Plan', 'Planned', '2028-02-01', '2028-02-02', 1, '2026-08-01T00:00:00+00:00', '2026-08-03T00:00:00+00:00');

        INSERT planning.Travelers (CreatorId, AdventurePlanId, TravelerId, DisplayName)
        VALUES
            ('creator_alpha', 'plan_completed', 'traveler_alpha', 'Alpha'),
            ('creator_alpha', 'plan_active', 'traveler_alpha', 'Alpha'),
            ('creator_alpha', 'plan_upcoming', 'traveler_alpha', 'Alpha'),
            ('creator_alpha', 'plan_archived', 'traveler_alpha', 'Alpha'),
            ('creator_alpha', 'plan_other_traveler', 'traveler_other', 'Other'),
            ('creator_beta', 'plan_other_creator', 'traveler_alpha', 'Alpha');

        INSERT planning.TravelerParticipations
            (CreatorId, AdventurePlanId, TravelerId, UserId, Status, Version,
             EffectiveFromUtc, ExpiresAtUtc, CreatedAtUtc, UpdatedAtUtc)
        VALUES
            ('creator_alpha', 'plan_completed', 'traveler_alpha', 'user_alpha', 'Accepted', 1, '2026-08-01T00:00:00+00:00', NULL, '2026-08-01T00:00:00+00:00', '2026-08-01T00:00:00+00:00'),
            ('creator_alpha', 'plan_active', 'traveler_alpha', 'user_alpha', 'Accepted', 5, '2026-08-01T00:00:00+00:00', NULL, '2026-08-01T00:00:00+00:00', '2026-08-09T00:00:00+00:00'),
            ('creator_alpha', 'plan_upcoming', 'traveler_alpha', 'user_alpha', 'Accepted', 1, '2026-08-01T00:00:00+00:00', NULL, '2026-08-01T00:00:00+00:00', '2026-08-01T00:00:00+00:00'),
            ('creator_alpha', 'plan_archived', 'traveler_alpha', 'user_alpha', 'Accepted', 1, '2026-08-01T00:00:00+00:00', NULL, '2026-08-01T00:00:00+00:00', '2026-08-01T00:00:00+00:00');

        INSERT planning.DestinationVisits
            (CreatorId, AdventurePlanId, DestinationVisitId, Name, StartDate, EndDate, TimeZone, Sequence, Notes)
        VALUES ('creator_alpha', 'plan_active', 'visit_second', 'Florence', '2026-08-12', '2026-08-16', 'Europe/Rome', 2, NULL),
               ('creator_alpha', 'plan_active', 'visit_first', 'Rome', '2026-08-09', '2026-08-12', 'Europe/Rome', 1, NULL);
        """);

    private static async Task VerifyIndexesAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT COUNT(*) FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'planning.TravelerParticipations')
              AND name = N'IX_TravelerParticipations_AuthorizedList';
            """, connection);
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    private static async Task VerifyHttpEndpointsAsync(string connectionString)
    {
        await using var factory = new SqlCompanionApiFactory(connectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Companion-Test-User", "user_alpha");
        client.DefaultRequestHeaders.Add("X-Companion-Test-Traveler", "traveler_alpha");
        client.DefaultRequestHeaders.Add("X-Companion-Test-Creator", "creator_alpha");
        client.DefaultRequestHeaders.Add("X-Companion-Test-Membership-Version", "3");

        using var list = await client.GetAsync("/v1/companion/adventures?includeCompleted=true&limit=10");
        Assert.Equal(System.Net.HttpStatusCode.OK, list.StatusCode);
        Assert.NotNull(list.Headers.ETag);
        var listPayload = await list.Content.ReadAsStringAsync();
        Assert.Contains("\"plan_active\"", listPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("plan_other_traveler", listPayload, StringComparison.Ordinal);

        using var detail = await client.GetAsync("/v1/companion/adventures/plan_active");
        Assert.Equal(System.Net.HttpStatusCode.OK, detail.StatusCode);
        Assert.NotNull(detail.Headers.ETag);
        var detailPayload = await detail.Content.ReadAsStringAsync();
        Assert.Contains("\"visit_first\"", detailPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("Safe description", detailPayload, StringComparison.Ordinal);

        using var inaccessible = await client.GetAsync("/v1/companion/adventures/plan_other_traveler");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, inaccessible.StatusCode);
    }

    private static async Task VerifyEstimatedPlanAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var enable = new SqlCommand("SET SHOWPLAN_XML ON;", connection))
            await enable.ExecuteNonQueryAsync();
        await using var command = new SqlCommand("""
            SELECT ap.AdventurePlanId
            FROM planning.AdventurePlans AS ap
            INNER JOIN planning.TravelerParticipations AS tp
              ON tp.CreatorId = ap.CreatorId AND tp.AdventurePlanId = ap.AdventurePlanId
            WHERE ap.CreatorId = N'creator_alpha' AND tp.UserId = N'user_alpha'
            ORDER BY ap.StartDate, ap.AdventurePlanId;
            """, connection);
        var plan = Convert.ToString(await command.ExecuteScalarAsync());
        await using (var disable = new SqlCommand("SET SHOWPLAN_XML OFF;", connection))
            await disable.ExecuteNonQueryAsync();
        Assert.Contains("TravelerParticipations", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("MissingIndexes", plan, StringComparison.Ordinal);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildConnectionString(string master, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName };
        return builder.ConnectionString;
    }
}

internal sealed class SqlCompanionApiFactory(string connectionString) : WebApplicationFactory<api::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("Companion:DeterministicMode", "true");
        builder.UseSetting("Companion:ActivationMode", "Disabled");
        builder.UseSetting("Companion:ProjectionProvider", "Closed");
        builder.UseSetting("Deployment:CommitSha", "5555555555555555555555555555555555555555");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICompanionProjectionService>();
            var queries = new SqlCompanionAdventureQueries(connectionString);
            services.AddSingleton<ICompanionAdventureSummaryQuery>(queries);
            services.AddSingleton<ICompanionAdventureDetailQuery>(queries);
            services.AddSingleton<ICompanionProjectionService, AuthoritativeCompanionProjectionService>();
        });
    }
}
