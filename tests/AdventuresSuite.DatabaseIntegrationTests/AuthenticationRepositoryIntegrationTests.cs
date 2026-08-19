using AdventuresSuite.DatabaseMigrator;
using AdventuresSuite.Identity.SqlServer;
using Microsoft.Data.SqlClient;
using AdventuresSuite.Identity.Persistence;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies Slice 5B identity persistence against real SQL Server.</summary>
public sealed class AuthenticationRepositoryIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 8, 15, 0, 0, TimeSpan.Zero);

    /// <summary>Proves identity convergence, lifecycle concurrency, sessions, and rollback.</summary>
    [Fact]
    public async Task Repositories_RealSqlServer_PreserveAuthenticationBoundaries()
    {
        var master = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(master), $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteAuthenticationTest_{Guid.NewGuid():N}";
        var connectionString = BuildConnectionString(master, databaseName);
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
        try
        {
            await CompanionPolicyMigrationTestHarness.MigrateAllAsync(connectionString);
            var factory = new SqlAuthenticationTransactionFactory(connectionString);
            var first = Proposed("user_first", "identity_first", "Customer-A");
            var second = Proposed("user_second", "identity_second", "Customer-A");

            var results = await Task.WhenAll(
                factory.ResolveOrCreateUserAsync(first.User, first.Identity),
                factory.ResolveOrCreateUserAsync(second.User, second.Identity));

            Assert.Equal(results[0], results[1]);
            Assert.Equal(1, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM auth.Users;"));
            Assert.Equal(1, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM auth.ExternalIdentities;"));

            await factory.ResolveOrCreateUserAsync(
                Proposed("user_case", "identity_case", "customer-A").User,
                Proposed("user_case", "identity_case", "customer-A").Identity);
            await factory.ResolveOrCreateUserAsync(
                Proposed("user_unicode", "identity_unicode", "café").User,
                Proposed("user_unicode", "identity_unicode", "café").Identity);
            await factory.ResolveOrCreateUserAsync(
                Proposed("user_unicode_two", "identity_unicode_two", "café").User,
                Proposed("user_unicode_two", "identity_unicode_two", "café").Identity);
            Assert.Equal(4, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM auth.ExternalIdentities;"));
            Assert.Equal(0, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('auth.ExternalIdentities') AND name LIKE '%Email%';"));

            var owner = results[0];
            var session = NewSession(owner.UserId, new(1), "session_first");
            await using (var transaction = await factory.BeginAsync())
            {
                await transaction.Sessions.AddAsync(session, owner.Id);
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync())
            {
                Assert.NotNull(await transaction.Sessions.GetValidAsync(session.Id, CreatedAt.AddMinutes(1), TimeSpan.FromMinutes(30)));
                Assert.Equal(SessionActivityTouchResult.Coalesced,
                    await transaction.Sessions.TouchActivityAsync(session.Id, CreatedAt.AddMinutes(1), TimeSpan.FromMinutes(5)));
                Assert.Equal(SessionActivityTouchResult.Updated,
                    await transaction.Sessions.TouchActivityAsync(session.Id, CreatedAt.AddMinutes(5), TimeSpan.FromMinutes(5)));
                Assert.Equal(SessionActivityTouchResult.SessionUnavailable,
                    await transaction.Sessions.TouchActivityAsync(session.Id, CreatedAt.AddMinutes(4), TimeSpan.FromMinutes(5)));
                await transaction.CommitAsync();
            }

            PlatformUser disabled;
            await using (var transaction = await factory.BeginAsync())
            {
                var user = (await transaction.Users.GetAsync(owner.UserId))!;
                disabled = user.TransitionTo(PlatformUserStatus.Disabled, CreatedAt.AddMinutes(10));
                await transaction.Users.UpdateAsync(disabled, user.SecurityVersion);
                await transaction.CommitAsync();
            }
            await using (var transaction = await factory.BeginAsync())
            {
                Assert.Null(await transaction.Sessions.GetValidAsync(session.Id, CreatedAt.AddMinutes(11), TimeSpan.FromMinutes(30)));
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.Sessions.AddAsync(
                    NewSession(owner.UserId, disabled.SecurityVersion, "session_disabled_user"), owner.Id));
                await Assert.ThrowsAsync<AuthenticationConcurrencyException>(() =>
                    transaction.Users.UpdateAsync(disabled, new(1)));
            }

            await using (var transaction = await factory.BeginAsync())
            {
                var reactivated = disabled.TransitionTo(PlatformUserStatus.Active, CreatedAt.AddMinutes(20));
                await transaction.Users.UpdateAsync(reactivated, disabled.SecurityVersion);
                await transaction.CommitAsync();
            }
            await using (var transaction = await factory.BeginAsync())
            {
                Assert.Null(await transaction.Sessions.GetValidAsync(session.Id, CreatedAt.AddMinutes(21), TimeSpan.FromMinutes(30)));
                var current = (await transaction.Users.GetAsync(owner.UserId))!;
                var currentSession = NewSession(owner.UserId, current.SecurityVersion, "session_current");
                var activitySession = NewSession(owner.UserId, current.SecurityVersion, "session_activity");
                await transaction.Sessions.AddAsync(currentSession, owner.Id);
                await transaction.Sessions.AddAsync(activitySession, owner.Id);
                Assert.True(await transaction.Sessions.RevokeAsync(currentSession.Id, CreatedAt.AddMinutes(22), SessionRevocationReason.SignedOut));
                Assert.Null(await transaction.Sessions.GetValidAsync(currentSession.Id, CreatedAt.AddMinutes(23), TimeSpan.FromMinutes(30)));
                await transaction.CommitAsync();
            }

            var activityId = new UserSessionId("session_activity");
            await Task.WhenAll(
                TouchAsync(factory, activityId, CreatedAt.AddMinutes(25)),
                TouchAsync(factory, activityId, CreatedAt.AddMinutes(26)));
            await using (var transaction = await factory.BeginAsync())
            {
                var touched = (await transaction.Sessions.GetAsync(activityId))!;
                Assert.InRange(touched.LastSeenAtUtc, CreatedAt.AddMinutes(25), CreatedAt.AddMinutes(26));
                Assert.Null(await transaction.Sessions.GetValidAsync(
                    activityId, CreatedAt.AddMinutes(56), TimeSpan.FromMinutes(30)));
                Assert.Null(await transaction.Sessions.GetValidAsync(
                    activityId, CreatedAt.AddHours(8), TimeSpan.FromHours(7)));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync())
            {
                Assert.True(await transaction.ExternalIdentities.DisableAsync(owner.Id, CreatedAt.AddMinutes(30)));
                Assert.Null(await transaction.Sessions.GetValidAsync(
                    activityId,
                    CreatedAt.AddMinutes(31),
                    TimeSpan.FromMinutes(30)));
                Assert.Equal(
                    SessionActivityTouchResult.SessionUnavailable,
                    await transaction.Sessions.TouchActivityAsync(
                        activityId,
                        CreatedAt.AddMinutes(31),
                        TimeSpan.FromMinutes(5)));
                var current = (await transaction.Users.GetAsync(owner.UserId))!;
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.Sessions.AddAsync(
                    NewSession(owner.UserId, current.SecurityVersion, "session_disabled_identity"), owner.Id));
                await transaction.CommitAsync();
            }

            var rollback = Proposed("user_rollback", "identity_rollback", "rollback-subject");
            await using (var transaction = await factory.BeginAsync())
                await transaction.CreateUserWithIdentityAsync(rollback.User, rollback.Identity);
            Assert.Equal(0, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM auth.Users WHERE UserId='user_rollback';"));
            Assert.Equal(0, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM auth.ExternalIdentities WHERE ExternalIdentityId='identity_rollback';"));

            var sessionRollback = Proposed(
                "user_session_rollback",
                "identity_session_rollback",
                "session-rollback-subject");
            await using (var transaction = await factory.BeginAsync())
            {
                var resolved = await transaction.ResolveOrCreateUserAsync(
                    sessionRollback.User,
                    sessionRollback.Identity);
                await Assert.ThrowsAsync<SqlException>(() => transaction.Sessions.AddAsync(
                    NewSession(resolved.UserId, sessionRollback.User.SecurityVersion, "session_first"),
                    resolved.Id));
            }
            Assert.Equal(0, await ScalarAsync<int>(connectionString,
                "SELECT COUNT(*) FROM auth.Users WHERE UserId='user_session_rollback';"));
            Assert.Equal(0, await ScalarAsync<int>(connectionString,
                "SELECT COUNT(*) FROM auth.ExternalIdentities WHERE ExternalIdentityId='identity_session_rollback';"));

            var failedOperation = Proposed("user_failed_operation", "identity_failed_operation", "failed-operation");
            await using (var transaction = await factory.BeginAsync())
            {
                await transaction.Users.AddAsync(failedOperation.User);
                await Assert.ThrowsAsync<SqlException>(() =>
                    transaction.Users.AddAsync(failedOperation.User));
            }
            Assert.Equal(0, await ScalarAsync<int>(connectionString,
                "SELECT COUNT(*) FROM auth.Users WHERE UserId='user_failed_operation';"));
        }
        finally
        {
            await ExecuteAsync(master, $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    private static (PlatformUser User, ExternalIdentityMapping Identity) Proposed(string userId, string identityId, string subject)
    {
        var user = new PlatformUser(new(userId), PlatformUserStatus.Active, new(1), CreatedAt, CreatedAt);
        return (user, new(new(identityId), new(new("external_id"), new("https://login.example.com/tenant"), new(subject)), user.Id, CreatedAt));
    }

    private static ApplicationSession NewSession(UserId userId, SecurityVersion version, string id) =>
        new(new(id), userId, version, CreatedAt, CreatedAt, CreatedAt.AddHours(8));

    private static async Task TouchAsync(
        IAuthenticationPersistenceTransactionFactory factory,
        UserSessionId sessionId,
        DateTimeOffset observedAtUtc)
    {
        await using var transaction = await factory.BeginAsync();
        await transaction.Sessions.TouchActivityAsync(
            sessionId,
            observedAtUtc,
            TimeSpan.FromMinutes(5));
        await transaction.CommitAsync();
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
