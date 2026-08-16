using AdventuresSuite.Companion.Application;
using AdventuresSuite.Companion.SqlServer;
using AdventuresSuite.DatabaseMigrator;
using AdventuresSuite.Identity;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies authoritative Companion identity and access resolution against disposable SQL Server.</summary>
public sealed class CompanionAuthoritativeAccessContextIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";
    private static readonly DateTimeOffset EvaluationTime =
        new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Proves exact identity, ownership, versions, permission, policy, and recheck behavior.</summary>
    [Fact]
    public async Task Resolve_ExactAuthorizedIdentity_ProducesVersionedServerContext()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await SeedAsync(connectionString);
            var resolver = Resolver(connectionString, allow: true);

            var result = await resolver.ResolveAdventureAsync(Identity(), "plan_alpha");

            Assert.Equal(CompanionAccessContextOutcome.Resolved, result.Outcome);
            var context = Assert.IsType<CompanionAuthoritativeAccessContext>(result.Context);
            Assert.Equal("user_alpha", context.UserId.Value);
            Assert.Equal(7, context.UserSecurityVersion);
            Assert.Equal("creator_alpha", context.CreatorId.Value);
            Assert.Equal("traveler_alpha", context.TravelerId);
            Assert.Equal(3, context.MembershipVersion);
            Assert.Equal(5, context.ParticipationVersion);
            Assert.Equal("adventure_read_v1", context.InformationPolicyVersion);
            Assert.True(await resolver.IsCurrentAsync(context));

            await ExecuteAsync(connectionString, """
                UPDATE auth.ExternalIdentities SET DisabledAtUtc='2026-08-15T11:00:00+00:00'
                WHERE ExternalIdentityId='identity_alpha';
                """);
            Assert.False(await resolver.IsCurrentAsync(context));
            await ExecuteAsync(connectionString, """
                UPDATE auth.ExternalIdentities SET DisabledAtUtc=NULL
                WHERE ExternalIdentityId='identity_alpha';
                """);

            await ExecuteAsync(connectionString, """
                UPDATE planning.TravelerParticipations SET Version = 6
                WHERE CreatorId='creator_alpha' AND AdventurePlanId='plan_alpha' AND UserId='user_alpha';
                """);
            Assert.False(await resolver.IsCurrentAsync(context));
            await ExecuteAsync(connectionString, """
                UPDATE planning.TravelerParticipations SET Version = 5
                WHERE CreatorId='creator_alpha' AND AdventurePlanId='plan_alpha' AND UserId='user_alpha';
                UPDATE auth.CreatorMemberships SET Version = 4
                WHERE CreatorId='creator_alpha' AND CreatorMembershipId='membership_alpha';
                """);
            Assert.False(await resolver.IsCurrentAsync(context));
            await ExecuteAsync(connectionString, """
                UPDATE auth.CreatorMemberships SET Version = 3
                WHERE CreatorId='creator_alpha' AND CreatorMembershipId='membership_alpha';
                UPDATE auth.Users SET SecurityVersion = 8 WHERE UserId='user_alpha';
                """);
            Assert.False(await resolver.IsCurrentAsync(context));
        });
    }

    /// <summary>Proves case changes, lifecycle changes, relationship changes, and policy closure fail closed.</summary>
    [Fact]
    public async Task Resolve_IdentityAndAuthorizationFailures_AreClosedAndDistinct()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await SeedAsync(connectionString);
            var resolver = Resolver(connectionString, allow: true);

            Assert.Equal(CompanionAccessContextOutcome.Unmapped,
                (await resolver.ResolveAdventureAsync(
                    Identity(issuer: "https://LOGIN.example.test/Tenant"), "plan_alpha")).Outcome);
            Assert.Equal(CompanionAccessContextOutcome.Unmapped,
                (await resolver.ResolveAdventureAsync(
                    Identity(subject: "Subject-Alpha"), "plan_alpha")).Outcome);
            Assert.Equal(CompanionAccessContextOutcome.Unauthorized,
                (await resolver.ResolveAdventureAsync(Identity(), "unknown_plan")).Outcome);

            Assert.Equal(CompanionAccessContextOutcome.InformationPolicyClosed,
                (await Resolver(connectionString, allow: false)
                    .ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);

            await ExecuteAsync(connectionString, """
                UPDATE auth.ExternalIdentities SET DisabledAtUtc='2026-08-15T11:00:00+00:00'
                WHERE ExternalIdentityId='identity_alpha';
                """);
            Assert.Equal(CompanionAccessContextOutcome.Disabled,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
            await ExecuteAsync(connectionString, """
                UPDATE auth.ExternalIdentities SET DisabledAtUtc=NULL WHERE ExternalIdentityId='identity_alpha';
                """);

            await ExecuteAsync(connectionString, """
                UPDATE auth.Users SET Status='Disabled', DisabledAtUtc='2026-08-15T11:00:00+00:00',
                    UpdatedAtUtc='2026-08-15T11:00:00+00:00' WHERE UserId='user_alpha';
                """);
            Assert.Equal(CompanionAccessContextOutcome.Disabled,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
            await ExecuteAsync(connectionString, """
                UPDATE auth.Users SET Status='Active', DisabledAtUtc=NULL,
                    UpdatedAtUtc='2026-08-15T11:01:00+00:00' WHERE UserId='user_alpha';
                """);

            await SetMembershipStatusAsync(connectionString, "Revoked");
            Assert.Equal(CompanionAccessContextOutcome.Revoked,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
            await SetMembershipStatusAsync(connectionString, "Pending");
            Assert.Equal(CompanionAccessContextOutcome.Inactive,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
            await SetMembershipStatusAsync(connectionString, "Active");

            await ExecuteAsync(connectionString, """
                UPDATE auth.CreatorMemberships SET EffectiveFromUtc='2026-08-16T00:00:00+00:00'
                WHERE CreatorId='creator_alpha' AND CreatorMembershipId='membership_alpha';
                """);
            Assert.Equal(CompanionAccessContextOutcome.Inactive,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
            await ExecuteAsync(connectionString, """
                UPDATE auth.CreatorMemberships SET EffectiveFromUtc='2026-08-01T00:00:00+00:00',
                    ExpiresAtUtc='2026-08-15T12:00:00+00:00'
                WHERE CreatorId='creator_alpha' AND CreatorMembershipId='membership_alpha';
                """);
            Assert.Equal(CompanionAccessContextOutcome.Inactive,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
            await ExecuteAsync(connectionString, """
                UPDATE auth.CreatorMemberships SET ExpiresAtUtc=NULL
                WHERE CreatorId='creator_alpha' AND CreatorMembershipId='membership_alpha';
                """);

            await SetParticipationStatusAsync(connectionString, "Revoked");
            Assert.Equal(CompanionAccessContextOutcome.Revoked,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
            await SetParticipationStatusAsync(connectionString, "Invited");
            Assert.Equal(CompanionAccessContextOutcome.Inactive,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
            await SetParticipationStatusAsync(connectionString, "Accepted");

            await ExecuteAsync(connectionString, """
                UPDATE planning.TravelerParticipations SET EffectiveFromUtc='2026-08-16T00:00:00+00:00'
                WHERE CreatorId='creator_alpha' AND AdventurePlanId='plan_alpha' AND UserId='user_alpha';
                """);
            Assert.Equal(CompanionAccessContextOutcome.Inactive,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
            await ExecuteAsync(connectionString, """
                UPDATE planning.TravelerParticipations SET EffectiveFromUtc='2026-08-01T00:00:00+00:00'
                WHERE CreatorId='creator_alpha' AND AdventurePlanId='plan_alpha' AND UserId='user_alpha';
                """);

            await ExecuteAsync(connectionString, """
                UPDATE planning.TravelerParticipations SET ExpiresAtUtc='2026-08-15T12:00:00+00:00'
                WHERE CreatorId='creator_alpha' AND AdventurePlanId='plan_alpha' AND UserId='user_alpha';
                """);
            Assert.Equal(CompanionAccessContextOutcome.Inactive,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);

            await ExecuteAsync(connectionString, """
                DELETE planning.TravelerParticipations
                WHERE CreatorId='creator_alpha' AND AdventurePlanId='plan_alpha' AND UserId='user_alpha';
                """);
            Assert.Equal(CompanionAccessContextOutcome.Unauthorized,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
        });
    }

    /// <summary>Proves schema safeguards reject duplicate and malformed identity authority.</summary>
    [Fact]
    public async Task Persistence_RejectsDuplicateAndMalformedIdentityMappings()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await SeedAsync(connectionString);

            await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connectionString, """
                INSERT auth.ExternalIdentities
                    (ExternalIdentityId,UserId,Provider,Issuer,Subject,CreatedAtUtc,DisabledAtUtc)
                VALUES ('identity_duplicate','user_alpha','entra_external_id',
                    'https://login.example.test/Tenant','subject-alpha',
                    '2026-08-01T00:00:00+00:00',NULL);
                """));
            await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connectionString, """
                UPDATE auth.Users SET Status='Unknown' WHERE UserId='user_alpha';
                """));
            await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connectionString, """
                UPDATE auth.Users SET SecurityVersion=0 WHERE UserId='user_alpha';
                """));
        });
    }

    /// <summary>Proves exact grants, Creator isolation, ambiguity, cancellation, and SQL failure behavior.</summary>
    [Fact]
    public async Task Resolve_PermissionAmbiguityAndFailures_FailClosed()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await SeedAsync(connectionString);
            var resolver = Resolver(connectionString, allow: true);

            await ExecuteAsync(connectionString, """
                DELETE auth.CreatorMembershipRoles
                WHERE CreatorId='creator_alpha' AND CreatorMembershipId='membership_alpha';
                """);
            Assert.Equal(CompanionAccessContextOutcome.Unauthorized,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);

            await ExecuteAsync(connectionString, """
                INSERT auth.CreatorMembershipPermissionGrants
                    (CreatorId,CreatorMembershipId,Permission)
                VALUES ('creator_alpha','membership_alpha','AdventurePlan.View');
                """);
            Assert.Equal(CompanionAccessContextOutcome.Resolved,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);

            await SeedAmbiguousAdventureAsync(connectionString);
            Assert.Equal(CompanionAccessContextOutcome.Ambiguous,
                (await resolver.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                resolver.ResolveAdventureAsync(Identity(), "plan_alpha", cancellation.Token));
        });

        var unavailable = Resolver(
            "Server=127.0.0.1,1;Database=unavailable;User ID=none;Password=none;" +
            "Encrypt=False;TrustServerCertificate=True;Connect Timeout=1", allow: true);
        Assert.Equal(CompanionAccessContextOutcome.OperationallyUnavailable,
            (await unavailable.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);

        var malformedConfiguration = Resolver("not a connection string", allow: true);
        Assert.Equal(CompanionAccessContextOutcome.OperationallyUnavailable,
            (await malformedConfiguration.ResolveAdventureAsync(Identity(), "plan_alpha")).Outcome);
    }

    private static SqlCompanionAuthoritativeAccessContextResolver Resolver(
        string connectionString, bool allow) => new(
            connectionString,
            allow ? new AllowingPolicy() : new ClosedCompanionInformationPolicy(),
            new FixedTimeProvider(EvaluationTime));

    private static CompanionExternalIdentity Identity(
        string issuer = "https://login.example.test/Tenant",
        string subject = "subject-alpha") => new(
            new ExternalIdentityProviderId("entra_external_id"),
            new ExternalIdentityIssuer(issuer),
            new ExternalIdentitySubject(subject));

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        var master = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(master), $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteCompanionAccessTest_{Guid.NewGuid():N}";
        var connectionString = BuildConnectionString(master, databaseName);
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
        try
        {
            DatabaseMigratorRunner.Migrate(connectionString);
            await test(connectionString);
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

    private static Task SeedAsync(string connectionString) => ExecuteAsync(connectionString, """
        INSERT auth.Users (UserId,Status,SecurityVersion,CreatedAtUtc,UpdatedAtUtc,DisabledAtUtc)
        VALUES ('user_alpha','Active',7,'2026-08-01T00:00:00+00:00','2026-08-01T00:00:00+00:00',NULL);
        INSERT auth.ExternalIdentities
            (ExternalIdentityId,UserId,Provider,Issuer,Subject,CreatedAtUtc,LastAuthenticatedAtUtc,DisabledAtUtc)
        VALUES ('identity_alpha','user_alpha','entra_external_id','https://login.example.test/Tenant',
            'subject-alpha','2026-08-01T00:00:00+00:00','2026-08-15T10:00:00+00:00',NULL);
        INSERT auth.CreatorMemberships
            (CreatorId,CreatorMembershipId,UserId,Status,Version,EffectiveFromUtc,ExpiresAtUtc,
             CreatedAtUtc,UpdatedAtUtc,CreatedByUserId,UpdatedByUserId)
        VALUES ('creator_alpha','membership_alpha','user_alpha','Active',3,
            '2026-08-01T00:00:00+00:00',NULL,'2026-08-01T00:00:00+00:00',
            '2026-08-01T00:00:00+00:00','user_alpha','user_alpha');
        INSERT auth.CreatorMembershipRoles (CreatorId,CreatorMembershipId,Role)
        VALUES ('creator_alpha','membership_alpha','Viewer');
        INSERT planning.AdventurePlans
            (CreatorId,AdventurePlanId,Title,WorkingDescription,LifecycleStage,PlanningStatus,
             StartDate,EndDate,Version,CreatedAtUtc,UpdatedAtUtc)
        VALUES ('creator_alpha','plan_alpha','Alpha',NULL,'Plan','Upcoming',
            '2026-09-01','2026-09-03',2,'2026-08-01T00:00:00+00:00','2026-08-02T00:00:00+00:00');
        INSERT planning.Travelers (CreatorId,AdventurePlanId,TravelerId,DisplayName)
        VALUES ('creator_alpha','plan_alpha','traveler_alpha','Alpha');
        INSERT planning.TravelerParticipations
            (CreatorId,AdventurePlanId,TravelerId,UserId,Status,Version,EffectiveFromUtc,
             ExpiresAtUtc,CreatedAtUtc,UpdatedAtUtc)
        VALUES ('creator_alpha','plan_alpha','traveler_alpha','user_alpha','Accepted',5,
            '2026-08-01T00:00:00+00:00',NULL,'2026-08-01T00:00:00+00:00','2026-08-01T00:00:00+00:00');
        """);

    private static Task SeedAmbiguousAdventureAsync(string connectionString) => ExecuteAsync(connectionString, """
        INSERT auth.CreatorMemberships
            (CreatorId,CreatorMembershipId,UserId,Status,Version,EffectiveFromUtc,ExpiresAtUtc,
             CreatedAtUtc,UpdatedAtUtc,CreatedByUserId,UpdatedByUserId)
        VALUES ('creator_beta','membership_beta','user_alpha','Active',1,
            '2026-08-01T00:00:00+00:00',NULL,'2026-08-01T00:00:00+00:00',
            '2026-08-01T00:00:00+00:00','user_alpha','user_alpha');
        INSERT auth.CreatorMembershipRoles (CreatorId,CreatorMembershipId,Role)
        VALUES ('creator_beta','membership_beta','Viewer');
        INSERT planning.AdventurePlans
            (CreatorId,AdventurePlanId,Title,WorkingDescription,LifecycleStage,PlanningStatus,
             StartDate,EndDate,Version,CreatedAtUtc,UpdatedAtUtc)
        VALUES ('creator_beta','plan_alpha','Other',NULL,'Plan','Upcoming',
            '2026-09-01','2026-09-03',1,'2026-08-01T00:00:00+00:00','2026-08-02T00:00:00+00:00');
        INSERT planning.Travelers (CreatorId,AdventurePlanId,TravelerId,DisplayName)
        VALUES ('creator_beta','plan_alpha','traveler_beta','Beta');
        INSERT planning.TravelerParticipations
            (CreatorId,AdventurePlanId,TravelerId,UserId,Status,Version,EffectiveFromUtc,
             ExpiresAtUtc,CreatedAtUtc,UpdatedAtUtc)
        VALUES ('creator_beta','plan_alpha','traveler_beta','user_alpha','Accepted',1,
            '2026-08-01T00:00:00+00:00',NULL,'2026-08-01T00:00:00+00:00','2026-08-01T00:00:00+00:00');
        """);

    private static Task SetMembershipStatusAsync(string connectionString, string status) =>
        ExecuteAsync(connectionString, $"""
            UPDATE auth.CreatorMemberships SET Status='{status}'
            WHERE CreatorId='creator_alpha' AND CreatorMembershipId='membership_alpha';
            """);

    private static Task SetParticipationStatusAsync(string connectionString, string status) =>
        ExecuteAsync(connectionString, $"""
            UPDATE planning.TravelerParticipations SET Status='{status}'
            WHERE CreatorId='creator_alpha' AND AdventurePlanId='plan_alpha' AND UserId='user_alpha';
            """);

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

    private sealed class AllowingPolicy : ICompanionInformationPolicy
    {
        public Task<CompanionInformationPolicyDecision> EvaluateAsync(
            CompanionInformationPolicyRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CompanionInformationPolicyDecision.Allow("adventure_read_v1"));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
