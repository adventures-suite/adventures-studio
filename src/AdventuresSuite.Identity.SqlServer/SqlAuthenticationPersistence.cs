using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Authorization.Persistence;

namespace AdventuresSuite.Identity.SqlServer;

/// <summary>Creates SQL Server transactions for platform identity persistence.</summary>
public sealed class SqlAuthenticationTransactionFactory(string connectionString)
    : IAuthenticationPersistenceTransactionFactory
{
    private readonly string connectionString = string.IsNullOrWhiteSpace(connectionString)
        ? throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString))
        : connectionString;

    /// <inheritdoc />
    public async Task<IAuthenticationPersistenceTransaction> BeginAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            return new SqlAuthenticationTransaction(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ExternalIdentityMapping> ResolveOrCreateUserAsync(
        PlatformUser proposedUser,
        ExternalIdentityMapping proposedExternalIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposedUser);
        ArgumentNullException.ThrowIfNull(proposedExternalIdentity);
        if (proposedExternalIdentity.UserId != proposedUser.Id)
        {
            throw new ArgumentException("The proposed external identity must reference the proposed user.");
        }

        await using var persistence = (SqlAuthenticationTransaction)await BeginAsync(cancellationToken);
        await persistence.AcquireIdentityLockAsync(proposedExternalIdentity.Key, cancellationToken);
        var existing = await persistence.ExternalIdentities.GetByKeyAsync(
            proposedExternalIdentity.Key,
            cancellationToken);
        if (existing is not null)
        {
            await persistence.CommitAsync(cancellationToken);
            return existing;
        }

        await persistence.CreateUserWithIdentityAsync(
            proposedUser,
            proposedExternalIdentity,
            cancellationToken);
        await persistence.CommitAsync(cancellationToken);
        return proposedExternalIdentity;
    }
}

internal sealed class SqlAuthenticationTransaction : IAuthenticationPersistenceTransaction
{
    private readonly SqlConnection connection;
    private readonly SqlTransaction transaction;
    private bool completed;

    public SqlAuthenticationTransaction(SqlConnection connection, SqlTransaction transaction)
    {
        this.connection = connection;
        this.transaction = transaction;
        Users = new DapperPlatformUserRepository(connection, transaction);
        ExternalIdentities = new DapperExternalIdentityRepository(connection, transaction);
        Sessions = new DapperUserSessionRepository(connection, transaction);
    }

    public IPlatformUserRepository Users { get; }
    public IExternalIdentityRepository ExternalIdentities { get; }
    public IUserSessionRepository Sessions { get; }

    public async Task CreateUserWithIdentityAsync(
        PlatformUser user,
        ExternalIdentityMapping externalIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(externalIdentity);
        if (externalIdentity.UserId != user.Id)
            throw new ArgumentException("The external identity must reference the new user.");
        await Users.AddAsync(user, cancellationToken);
        await ExternalIdentities.AddAsync(externalIdentity, cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        await transaction.CommitAsync(cancellationToken);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!completed)
                await transaction.RollbackAsync();
        }
        finally
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                await connection.DisposeAsync();
                completed = true;
            }
        }
    }

    public async Task AcquireIdentityLockAsync(
        ExternalIdentityKey key,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(
            $"{key.ProviderId.Value.Length}:{key.ProviderId.Value}|{key.Issuer.Value.Length}:{key.Issuer.Value}|{key.Subject.Value.Length}:{key.Subject.Value}");
        var resource = $"auth:{Convert.ToHexString(SHA256.HashData(bytes))}";
        var result = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            DECLARE @Result int;
            EXEC @Result = sys.sp_getapplock @Resource=@Resource, @LockMode='Exclusive',
                @LockOwner='Transaction', @LockTimeout=15000;
            SELECT @Result;
            """, new { Resource = resource }, transaction, cancellationToken: cancellationToken));
        if (result < 0)
            throw new InvalidOperationException("The external identity provisioning lock was unavailable.");
    }
}

internal abstract class DapperAuthenticationRepository(SqlConnection connection, SqlTransaction transaction)
{
    protected SqlConnection Connection { get; } = connection;
    protected SqlTransaction Transaction { get; } = transaction;
    protected CommandDefinition Command(string sql, object? parameters, CancellationToken token) =>
        new(sql, parameters, Transaction, cancellationToken: token);
}

internal sealed class DapperPlatformUserRepository(SqlConnection connection, SqlTransaction transaction)
    : DapperAuthenticationRepository(connection, transaction), IPlatformUserRepository
{
    public async Task<PlatformUser?> GetAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        if (userId == default) throw new ArgumentException("A user identity is required.", nameof(userId));
        var row = await Connection.QuerySingleOrDefaultAsync<UserRow>(Command(
            "SELECT UserId,Status,SecurityVersion,CreatedAtUtc,UpdatedAtUtc,DisabledAtUtc FROM auth.Users WHERE UserId=@UserId;",
            new { UserId = userId.Value }, cancellationToken));
        return row?.Map();
    }

    public Task AddAsync(PlatformUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Connection.ExecuteAsync(Command("""
            INSERT auth.Users (UserId,Status,SecurityVersion,CreatedAtUtc,UpdatedAtUtc,DisabledAtUtc)
            VALUES (@UserId,@Status,@SecurityVersion,@CreatedAtUtc,@UpdatedAtUtc,@DisabledAtUtc);
            """, Parameters(user), cancellationToken));
    }

    public async Task UpdateAsync(PlatformUser user, SecurityVersion expectedSecurityVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (expectedSecurityVersion == default || user.SecurityVersion != expectedSecurityVersion.Next())
            throw new ArgumentException("A user update must advance the expected security version exactly once.");
        var count = await Connection.ExecuteAsync(Command("""
            UPDATE auth.Users SET Status=@Status,SecurityVersion=@SecurityVersion,UpdatedAtUtc=@UpdatedAtUtc,DisabledAtUtc=@DisabledAtUtc
            WHERE UserId=@UserId AND SecurityVersion=@ExpectedVersion;
            """, new
        {
            UserId = user.Id.Value,
            Status = user.Status.ToString(),
            SecurityVersion = user.SecurityVersion.Value,
            user.UpdatedAtUtc,
            user.DisabledAtUtc,
            ExpectedVersion = expectedSecurityVersion.Value
        }, cancellationToken));
        if (count == 0) throw new AuthenticationConcurrencyException(user.Id, expectedSecurityVersion);
    }

    private static object Parameters(PlatformUser user) => new
    {
        UserId = user.Id.Value,
        Status = user.Status.ToString(),
        SecurityVersion = user.SecurityVersion.Value,
        user.CreatedAtUtc,
        user.UpdatedAtUtc,
        user.DisabledAtUtc
    };
    private sealed record UserRow(string UserId, string Status, long SecurityVersion, DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc, DateTimeOffset? DisabledAtUtc)
    {
        public PlatformUser Map() => new(new(UserId), Enum.Parse<PlatformUserStatus>(Status), new(SecurityVersion),
            CreatedAtUtc.ToUniversalTime(), UpdatedAtUtc.ToUniversalTime(), DisabledAtUtc?.ToUniversalTime());
    }
}

internal sealed class DapperExternalIdentityRepository(SqlConnection connection, SqlTransaction transaction)
    : DapperAuthenticationRepository(connection, transaction), IExternalIdentityRepository
{
    public async Task<ExternalIdentityMapping?> GetByKeyAsync(ExternalIdentityKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var row = await Connection.QuerySingleOrDefaultAsync<IdentityRow>(Command("""
            SELECT ExternalIdentityId,UserId,Provider,Issuer,Subject,CreatedAtUtc,LastAuthenticatedAtUtc,DisabledAtUtc
            FROM auth.ExternalIdentities
            WHERE Provider=@Provider COLLATE Latin1_General_100_BIN2
              AND Issuer=@Issuer COLLATE Latin1_General_100_BIN2
              AND Subject=@Subject COLLATE Latin1_General_100_BIN2;
            """, new { Provider = key.ProviderId.Value, Issuer = key.Issuer.Value, Subject = key.Subject.Value }, cancellationToken));
        return row?.Map();
    }

    public Task AddAsync(ExternalIdentityMapping mapping, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return Connection.ExecuteAsync(Command("""
            INSERT auth.ExternalIdentities
              (ExternalIdentityId,UserId,Provider,Issuer,Subject,CreatedAtUtc,LastAuthenticatedAtUtc,DisabledAtUtc)
            VALUES (@Id,@UserId,@Provider,@Issuer,@Subject,@CreatedAtUtc,@LastAuthenticatedAtUtc,@DisabledAtUtc);
            """, new
        {
            Id = mapping.Id.Value,
            UserId = mapping.UserId.Value,
            Provider = mapping.Key.ProviderId.Value,
            Issuer = mapping.Key.Issuer.Value,
            Subject = mapping.Key.Subject.Value,
            mapping.CreatedAtUtc,
            mapping.LastAuthenticatedAtUtc,
            mapping.DisabledAtUtc
        }, cancellationToken));
    }

    public async Task<bool> DisableAsync(ExternalIdentityId externalIdentityId, DateTimeOffset disabledAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (externalIdentityId == default || disabledAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("An external identity and UTC disablement time are required.");
        return await Connection.ExecuteAsync(Command("""
            UPDATE auth.ExternalIdentities SET DisabledAtUtc=@DisabledAtUtc
            WHERE ExternalIdentityId=@Id AND DisabledAtUtc IS NULL
              AND @DisabledAtUtc>=CreatedAtUtc
              AND (LastAuthenticatedAtUtc IS NULL OR @DisabledAtUtc>=LastAuthenticatedAtUtc);
            """, new { Id = externalIdentityId.Value, DisabledAtUtc = disabledAtUtc }, cancellationToken)) == 1;
    }

    private sealed record IdentityRow(string ExternalIdentityId, string UserId, string Provider, string Issuer,
        string Subject, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastAuthenticatedAtUtc, DateTimeOffset? DisabledAtUtc)
    {
        public ExternalIdentityMapping Map() => new(new(ExternalIdentityId),
            new(new(Provider), new(Issuer), new(Subject)), new(UserId), CreatedAtUtc.ToUniversalTime(),
            LastAuthenticatedAtUtc?.ToUniversalTime(), DisabledAtUtc?.ToUniversalTime());
    }
}

internal sealed class DapperUserSessionRepository(SqlConnection connection, SqlTransaction transaction)
    : DapperAuthenticationRepository(connection, transaction), IUserSessionRepository
{
    public async Task<ApplicationSession?> GetAsync(UserSessionId sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == default) throw new ArgumentException("A session identity is required.", nameof(sessionId));
        var row = await Connection.QuerySingleOrDefaultAsync<SessionRow>(Command(
            "SELECT UserSessionId,UserId,SecurityVersion,CreatedAtUtc,LastSeenAtUtc,AbsoluteExpiresAtUtc,RevokedAtUtc,RevocationReason FROM auth.UserSessions WHERE UserSessionId=@Id;",
            new { Id = sessionId.Value }, cancellationToken));
        return row?.Map();
    }

    public async Task<ApplicationSession?> GetValidAsync(UserSessionId sessionId, DateTimeOffset utcNow,
        TimeSpan idleTimeout, CancellationToken cancellationToken = default)
    {
        if (utcNow.Offset != TimeSpan.Zero || idleTimeout <= TimeSpan.Zero)
            throw new ArgumentException("A UTC time and positive idle timeout are required.");
        var state = await Connection.QuerySingleOrDefaultAsync<ValidSessionRow>(Command("""
            SELECT s.UserSessionId,s.UserId,s.SecurityVersion,s.CreatedAtUtc,s.LastSeenAtUtc,
                   s.AbsoluteExpiresAtUtc,s.RevokedAtUtc,s.RevocationReason,
                   u.Status AS UserStatus,u.SecurityVersion AS UserSecurityVersion
            FROM auth.UserSessions s
            JOIN auth.Users u ON u.UserId=s.UserId
            JOIN auth.ExternalIdentities i
              ON i.ExternalIdentityId=s.ExternalIdentityId AND i.UserId=s.UserId
            WHERE s.UserSessionId=@Id AND i.DisabledAtUtc IS NULL;
            """, new { Id = sessionId.Value }, cancellationToken));
        if (state is null) return null;
        var session = state.Map();
        return session.EvaluateAt(utcNow, idleTimeout, Enum.Parse<PlatformUserStatus>(state.UserStatus), new(state.UserSecurityVersion))
            == ApplicationSessionState.Active ? session : null;
    }

    public async Task AddAsync(ApplicationSession session, ExternalIdentityId authenticatedIdentityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (authenticatedIdentityId == default) throw new ArgumentException("An authenticated identity is required.");
        var count = await Connection.ExecuteAsync(Command("""
            INSERT auth.UserSessions (UserSessionId,UserId,ExternalIdentityId,SecurityVersion,CreatedAtUtc,LastSeenAtUtc,AbsoluteExpiresAtUtc,RevokedAtUtc,RevocationReason)
            SELECT @Id,@UserId,@IdentityId,@Version,@Created,@LastSeen,@Expires,@Revoked,@Reason
            FROM auth.Users u JOIN auth.ExternalIdentities i ON i.UserId=u.UserId
            WHERE u.UserId=@UserId AND u.Status='Active' AND u.SecurityVersion=@Version
              AND i.ExternalIdentityId=@IdentityId AND i.DisabledAtUtc IS NULL;
            """, new
        {
            Id = session.Id.Value,
            UserId = session.UserId.Value,
            Version = session.SecurityVersion.Value,
            Created = session.CreatedAtUtc,
            LastSeen = session.LastSeenAtUtc,
            Expires = session.AbsoluteExpiresAtUtc,
            Revoked = session.RevokedAtUtc,
            Reason = session.RevocationReason?.ToString(),
            IdentityId = authenticatedIdentityId.Value
        }, cancellationToken));
        if (count != 1) throw new InvalidOperationException("The active user and external identity could not establish a session.");
    }

    public async Task<bool> RevokeAsync(UserSessionId sessionId, DateTimeOffset revokedAtUtc,
        SessionRevocationReason reason, CancellationToken cancellationToken = default)
    {
        if (sessionId == default || revokedAtUtc.Offset != TimeSpan.Zero || !Enum.IsDefined(reason))
            throw new ArgumentException("A session, UTC revocation time, and valid reason are required.");
        return await Connection.ExecuteAsync(Command("""
            UPDATE auth.UserSessions SET RevokedAtUtc=@RevokedAtUtc,RevocationReason=@Reason
            WHERE UserSessionId=@Id AND RevokedAtUtc IS NULL AND @RevokedAtUtc>=LastSeenAtUtc;
            """, new { Id = sessionId.Value, RevokedAtUtc = revokedAtUtc, Reason = reason.ToString() }, cancellationToken)) == 1;
    }

    public async Task<SessionActivityTouchResult> TouchActivityAsync(UserSessionId sessionId, DateTimeOffset observedAtUtc,
        TimeSpan minimumWriteInterval, CancellationToken cancellationToken = default)
    {
        if (sessionId == default || observedAtUtc.Offset != TimeSpan.Zero || minimumWriteInterval <= TimeSpan.Zero)
            throw new ArgumentException("A session, UTC observation, and positive coalescing interval are required.");
        var seconds = checked((int)Math.Ceiling(minimumWriteInterval.TotalSeconds));
        var updated = await Connection.ExecuteAsync(Command("""
            UPDATE auth.UserSessions SET LastSeenAtUtc=@Observed
            WHERE UserSessionId=@Id AND RevokedAtUtc IS NULL AND @Observed<AbsoluteExpiresAtUtc
              AND @Observed>LastSeenAtUtc AND DATEADD(second,@Seconds,LastSeenAtUtc)<=@Observed
              AND EXISTS
              (
                  SELECT 1 FROM auth.Users
                  WHERE auth.Users.UserId=auth.UserSessions.UserId
                    AND auth.Users.Status='Active'
                    AND auth.Users.SecurityVersion=auth.UserSessions.SecurityVersion
              )
              AND EXISTS
              (
                  SELECT 1 FROM auth.ExternalIdentities
                  WHERE auth.ExternalIdentities.ExternalIdentityId=auth.UserSessions.ExternalIdentityId
                    AND auth.ExternalIdentities.UserId=auth.UserSessions.UserId
                    AND auth.ExternalIdentities.DisabledAtUtc IS NULL
              );
            """, new { Id = sessionId.Value, Observed = observedAtUtc, Seconds = seconds }, cancellationToken));
        if (updated == 1) return SessionActivityTouchResult.Updated;
        var available = await Connection.ExecuteScalarAsync<int>(Command("""
            SELECT COUNT(*) FROM auth.UserSessions
            WHERE UserSessionId=@Id AND RevokedAtUtc IS NULL AND @Observed>=LastSeenAtUtc AND @Observed<AbsoluteExpiresAtUtc
              AND EXISTS
              (
                  SELECT 1 FROM auth.Users
                  WHERE auth.Users.UserId=auth.UserSessions.UserId
                    AND auth.Users.Status='Active'
                    AND auth.Users.SecurityVersion=auth.UserSessions.SecurityVersion
              )
              AND EXISTS
              (
                  SELECT 1 FROM auth.ExternalIdentities
                  WHERE auth.ExternalIdentities.ExternalIdentityId=auth.UserSessions.ExternalIdentityId
                    AND auth.ExternalIdentities.UserId=auth.UserSessions.UserId
                    AND auth.ExternalIdentities.DisabledAtUtc IS NULL
              );
            """, new { Id = sessionId.Value, Observed = observedAtUtc }, cancellationToken));
        return available == 1 ? SessionActivityTouchResult.Coalesced : SessionActivityTouchResult.SessionUnavailable;
    }

    private sealed record SessionRow(string UserSessionId, string UserId, long SecurityVersion, DateTimeOffset CreatedAtUtc,
        DateTimeOffset LastSeenAtUtc, DateTimeOffset AbsoluteExpiresAtUtc, DateTimeOffset? RevokedAtUtc, string? RevocationReason)
    {
        public ApplicationSession Map() => new(new(UserSessionId), new(UserId), new(SecurityVersion),
            CreatedAtUtc.ToUniversalTime(), LastSeenAtUtc.ToUniversalTime(), AbsoluteExpiresAtUtc.ToUniversalTime(),
            RevokedAtUtc?.ToUniversalTime(), RevocationReason is null ? null : Enum.Parse<SessionRevocationReason>(RevocationReason));
    }
    private sealed record ValidSessionRow(string UserSessionId, string UserId, long SecurityVersion,
        DateTimeOffset CreatedAtUtc, DateTimeOffset LastSeenAtUtc, DateTimeOffset AbsoluteExpiresAtUtc,
        DateTimeOffset? RevokedAtUtc, string? RevocationReason, string UserStatus, long UserSecurityVersion)
    {
        public ApplicationSession Map() => new(new(UserSessionId), new(UserId), new(SecurityVersion),
            CreatedAtUtc.ToUniversalTime(), LastSeenAtUtc.ToUniversalTime(), AbsoluteExpiresAtUtc.ToUniversalTime(),
            RevokedAtUtc?.ToUniversalTime(), RevocationReason is null ? null : Enum.Parse<SessionRevocationReason>(RevocationReason));
    }
}
