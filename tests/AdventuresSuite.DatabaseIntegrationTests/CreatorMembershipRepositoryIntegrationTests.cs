using AdventuresSuite.Authorization.SqlServer;
using AdventuresSuite.DatabaseMigrator;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies Slice 6 Creator membership persistence against real SQL Server.</summary>
public sealed class CreatorMembershipRepositoryIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 4, 0, 0, TimeSpan.Zero);
    private static readonly CreatorId Alpha = new("creator_alpha");
    private static readonly CreatorId Beta = new("creator_beta");
    private static readonly UserId OwnerUser = new("user_owner");
    private static readonly UserId SecondOwnerUser = new("user_second_owner");
    private static readonly UserId ThirdOwnerUser = new("user_third_owner");
    private static readonly UserId ViewerUser = new("user_viewer");

    /// <summary>Proves Creator isolation, concurrency, audit atomicity, and last-owner safety.</summary>
    [Fact]
    public async Task Repositories_RealSqlServer_PreserveMembershipBoundaries()
    {
        var master = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(master), $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteMembershipTest_{Guid.NewGuid():N}";
        var connectionString = BuildConnectionString(master, databaseName);
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
        try
        {
            DatabaseMigratorRunner.Migrate(connectionString);
            await SeedUsersAsync(connectionString);
            var factory = new SqlCreatorMembershipTransactionFactory(connectionString);

            var alphaOwner = Membership("membership_alpha_owner", OwnerUser, Alpha, CreatorRole.Owner);
            await AddAsync(factory, alphaOwner, Audit("audit_alpha_owner", OwnerUser, Alpha, alphaOwner, null, 1));
            var betaOwner = Membership("membership_beta_owner", OwnerUser, Beta, CreatorRole.Owner);
            await AddAsync(factory, betaOwner, Audit("audit_beta_owner", OwnerUser, Beta, betaOwner, null, 1));
            var viewer = Membership(
                "membership_alpha_viewer",
                ViewerUser,
                Alpha,
                CreatorRole.Viewer,
                Permissions.AdventurePlanViewArchived);
            await AddAsync(factory, viewer, Audit("audit_alpha_viewer", OwnerUser, Alpha, viewer, null, 1));

            await using (var alpha = await factory.BeginAsync(Alpha))
            {
                var loaded = await alpha.Memberships.GetMembershipAsync(ViewerUser, Alpha);
                Assert.NotNull(loaded);
                Assert.Equal(viewer.Id, loaded.Id);
                Assert.Equal(viewer.PermissionGrants, loaded.PermissionGrants);
                Assert.True(loaded.IsActiveAt(Now));
                await alpha.CommitAsync();
            }
            await using (var beta = await factory.BeginAsync(Beta))
            {
                Assert.Null(await beta.Memberships.GetMembershipAsync(ViewerUser, Beta));
                Assert.Null(await beta.Memberships.GetByIdAsync(viewer.Id));
                await beta.CommitAsync();
            }
            await using (var crossCreator = await factory.BeginAsync(Alpha))
            {
                var betaMembership = Membership(
                    "membership_wrong_scope",
                    ViewerUser,
                    Beta,
                    CreatorRole.Viewer);
                await Assert.ThrowsAsync<ArgumentException>(() => crossCreator.Memberships.AddAsync(
                    betaMembership,
                    Audit("audit_wrong_scope", OwnerUser, Beta, betaMembership, null, 1)));
            }
            Assert.Equal(0, await ScalarAsync<int>(connectionString, """
                SELECT COUNT(*) FROM auth.CreatorMemberships
                WHERE CreatorMembershipId='membership_wrong_scope';
                """));

            var disabledViewer = Membership(
                viewer.Id.Value,
                ViewerUser,
                Alpha,
                CreatorRole.Viewer,
                status: CreatorMembershipStatus.Disabled,
                version: 2);
            await UpdateAsync(factory, disabledViewer, 1,
                Audit("audit_disable_viewer", OwnerUser, Alpha, disabledViewer, 1, 2));
            await using (var stale = await factory.BeginAsync(Alpha))
            {
                await Assert.ThrowsAsync<CreatorMembershipConcurrencyException>(() =>
                    stale.Memberships.UpdateAsync(
                        disabledViewer,
                        1,
                        Audit("audit_stale_viewer", OwnerUser, Alpha, disabledViewer, 1, 2)));
            }

            var revokedOwner = Membership(
                alphaOwner.Id.Value,
                OwnerUser,
                Alpha,
                CreatorRole.Owner,
                status: CreatorMembershipStatus.Revoked,
                version: 2);
            await using (var lastOwner = await factory.BeginAsync(Alpha))
            {
                await Assert.ThrowsAsync<LastCreatorOwnerException>(() =>
                    lastOwner.Memberships.UpdateAsync(
                        revokedOwner,
                        1,
                        Audit("audit_revoke_last_owner", OwnerUser, Alpha, revokedOwner, 1, 2)));
            }

            var secondOwner = Membership(
                "membership_alpha_second_owner",
                SecondOwnerUser,
                Alpha,
                CreatorRole.Owner);
            await AddAsync(factory, secondOwner,
                Audit("audit_second_owner", OwnerUser, Alpha, secondOwner, null, 1));
            await UpdateAsync(factory, revokedOwner, 1,
                Audit("audit_revoke_owner", SecondOwnerUser, Alpha, revokedOwner, 1, 2));

            var thirdOwner = Membership(
                "membership_alpha_third_owner",
                ThirdOwnerUser,
                Alpha,
                CreatorRole.Owner);
            await AddAsync(factory, thirdOwner,
                Audit("audit_third_owner", SecondOwnerUser, Alpha, thirdOwner, null, 1));
            var revokedSecondOwner = Membership(
                secondOwner.Id.Value,
                SecondOwnerUser,
                Alpha,
                CreatorRole.Owner,
                status: CreatorMembershipStatus.Revoked,
                version: 2);
            var revokedThirdOwner = Membership(
                thirdOwner.Id.Value,
                ThirdOwnerUser,
                Alpha,
                CreatorRole.Owner,
                status: CreatorMembershipStatus.Revoked,
                version: 2);
            var concurrentResults = await Task.WhenAll(
                TryUpdateAsync(factory, revokedSecondOwner, 1,
                    Audit("audit_revoke_second_owner", ThirdOwnerUser, Alpha, revokedSecondOwner, 1, 2)),
                TryUpdateAsync(factory, revokedThirdOwner, 1,
                    Audit("audit_revoke_third_owner", SecondOwnerUser, Alpha, revokedThirdOwner, 1, 2)));
            Assert.Single(concurrentResults, result => result is null);
            Assert.Single(concurrentResults, result => result is LastCreatorOwnerException);

            var rollbackMember = Membership(
                "membership_rollback",
                ViewerUser,
                Beta,
                CreatorRole.Viewer);
            await using (var rollback = await factory.BeginAsync(Beta))
            {
                await Assert.ThrowsAsync<SqlException>(() => rollback.Memberships.AddAsync(
                    rollbackMember,
                    Audit("audit_beta_owner", OwnerUser, Beta, rollbackMember, null, 1)));
            }

            Assert.Equal(0, await ScalarAsync<int>(connectionString, """
                SELECT COUNT(*) FROM auth.CreatorMemberships
                WHERE CreatorId='creator_beta' AND CreatorMembershipId='membership_rollback';
                """));
            Assert.Equal(8, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM audit.AuditEvents;"));
            Assert.Equal(1, await ScalarAsync<int>(connectionString, """
                SELECT COUNT(*) FROM auth.CreatorMemberships AS memberships
                JOIN auth.CreatorMembershipRoles AS roles
                  ON roles.CreatorId=memberships.CreatorId
                 AND roles.CreatorMembershipId=memberships.CreatorMembershipId
                WHERE memberships.CreatorId='creator_alpha' AND memberships.Status='Active'
                  AND roles.Role='Owner';
                """));
            Assert.Equal(0, await ScalarAsync<int>(connectionString, """
                SELECT COUNT(*) FROM audit.AuditEvents
                WHERE ActorUserId IS NULL OR Permission<>'Creator.ManageMembers';
                """));
        }
        finally
        {
            await ExecuteAsync(master,
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    private static async Task AddAsync(
        ICreatorMembershipTransactionFactory factory,
        CreatorMembershipSnapshot membership,
        AuditEventIntent audit)
    {
        await using var transaction = await factory.BeginAsync(membership.CreatorId);
        await transaction.Memberships.AddAsync(membership, audit);
        await transaction.CommitAsync();
    }

    private static async Task UpdateAsync(
        ICreatorMembershipTransactionFactory factory,
        CreatorMembershipSnapshot membership,
        long expectedVersion,
        AuditEventIntent audit)
    {
        await using var transaction = await factory.BeginAsync(membership.CreatorId);
        await transaction.Memberships.UpdateAsync(membership, expectedVersion, audit);
        await transaction.CommitAsync();
    }

    private static async Task<Exception?> TryUpdateAsync(
        ICreatorMembershipTransactionFactory factory,
        CreatorMembershipSnapshot membership,
        long expectedVersion,
        AuditEventIntent audit)
    {
        try
        {
            await UpdateAsync(factory, membership, expectedVersion, audit);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static CreatorMembershipSnapshot Membership(
        string membershipId,
        UserId userId,
        CreatorId creatorId,
        CreatorRole role,
        Permission? grant = null,
        CreatorMembershipStatus status = CreatorMembershipStatus.Active,
        long version = 1) => new(
            new(membershipId),
            userId,
            creatorId,
            status,
            [role],
            grant.HasValue ? [grant.Value] : [],
            version,
            Now.AddHours(-1));

    private static AuditEventIntent Audit(
        string auditId,
        UserId actorUserId,
        CreatorId creatorId,
        CreatorMembershipSnapshot membership,
        long? previousVersion,
        long resultingVersion) => new(
            new(auditId),
            new(ActorType.Human, actorUserId.Value, actorUserId),
            creatorId,
            Permissions.CreatorManageMembers,
            AuthorizationResourceScope.ForInstance(
                creatorId,
                AuthorizationResourceTypes.CreatorMembership,
                membership.Id.Value),
            AuditOutcome.Succeeded,
            AuditReasonCategory.Completed,
            Now.AddMinutes(resultingVersion),
            new($"correlation_{auditId}"),
            previousVersion: previousVersion,
            resultingVersion: resultingVersion);

    private static async Task SeedUsersAsync(string connectionString)
    {
        await ExecuteAsync(connectionString, """
            INSERT auth.Users (UserId,Status,SecurityVersion,CreatedAtUtc,UpdatedAtUtc)
            VALUES
              ('user_owner','Active',1,'2026-08-10T03:00:00+00:00','2026-08-10T03:00:00+00:00'),
              ('user_second_owner','Active',1,'2026-08-10T03:00:00+00:00','2026-08-10T03:00:00+00:00'),
              ('user_third_owner','Active',1,'2026-08-10T03:00:00+00:00','2026-08-10T03:00:00+00:00'),
              ('user_viewer','Active',1,'2026-08-10T03:00:00+00:00','2026-08-10T03:00:00+00:00');
            """);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }

    private static string BuildConnectionString(string master, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName };
        return builder.ConnectionString;
    }
}
