namespace TheSimontonAdventures.Web.Authorization.Persistence;

/// <summary>Describes the outcome of a coalesced session-activity write.</summary>
public enum SessionActivityTouchResult
{
    /// <summary>The authoritative activity timestamp advanced.</summary>
    Updated,
    /// <summary>The observation was valid but fell inside the coalescing interval.</summary>
    Coalesced,
    /// <summary>The session was missing, revoked, expired, or otherwise unavailable.</summary>
    SessionUnavailable
}

/// <summary>Creates atomic persistence transactions for platform identity state.</summary>
public interface IAuthenticationPersistenceTransactionFactory
{
    /// <summary>Begins one authentication persistence transaction.</summary>
    Task<IAuthenticationPersistenceTransaction> BeginAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves an exact external identity or atomically creates its proposed
    /// user and mapping when it does not yet exist.
    /// </summary>
    Task<ExternalIdentityMapping> ResolveOrCreateUserAsync(
        PlatformUser proposedUser,
        ExternalIdentityMapping proposedExternalIdentity,
        CancellationToken cancellationToken = default);
}

/// <summary>Coordinates user, external-identity, and session writes atomically.</summary>
public interface IAuthenticationPersistenceTransaction : IAsyncDisposable
{
    /// <summary>Gets platform-user persistence operations in this transaction.</summary>
    IPlatformUserRepository Users { get; }

    /// <summary>Gets external-identity persistence operations in this transaction.</summary>
    IExternalIdentityRepository ExternalIdentities { get; }

    /// <summary>Gets application-session persistence operations in this transaction.</summary>
    IUserSessionRepository Sessions { get; }

    /// <summary>
    /// Resolves an exact external identity or creates its proposed user and
    /// mapping inside this transaction's atomic boundary.
    /// </summary>
    Task<ExternalIdentityMapping> ResolveOrCreateUserAsync(
        PlatformUser proposedUser,
        ExternalIdentityMapping proposedExternalIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new user and its first exact external identity atomically.</summary>
    Task CreateUserWithIdentityAsync(
        PlatformUser user,
        ExternalIdentityMapping externalIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>Commits every validated change in this transaction.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>Persists immutable platform-user lifecycle snapshots.</summary>
public interface IPlatformUserRepository
{
    /// <summary>Reads the current authoritative user snapshot without a cache.</summary>
    Task<PlatformUser?> GetAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new platform user.</summary>
    Task AddAsync(
        PlatformUser user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a user snapshot only when the stored security version matches
    /// the expected version.
    /// </summary>
    Task UpdateAsync(
        PlatformUser user,
        SecurityVersion expectedSecurityVersion,
        CancellationToken cancellationToken = default);
}

/// <summary>Persists exact provider-scoped external identity mappings.</summary>
public interface IExternalIdentityRepository
{
    /// <summary>Reads a mapping using ordinal case-sensitive key semantics.</summary>
    Task<ExternalIdentityMapping?> GetByKeyAsync(
        ExternalIdentityKey key,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new external identity mapping.</summary>
    Task AddAsync(
        ExternalIdentityMapping mapping,
        CancellationToken cancellationToken = default);

    /// <summary>Disables an active mapping so it cannot establish new sessions.</summary>
    Task<bool> DisableAsync(
        ExternalIdentityId externalIdentityId,
        DateTimeOffset disabledAtUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>Persists revocable application-controlled sessions.</summary>
public interface IUserSessionRepository
{
    /// <summary>Reads current authoritative session state without a cache.</summary>
    Task<ApplicationSession?> GetAsync(
        UserSessionId sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new application session.</summary>
    Task AddAsync(
        ApplicationSession session,
        ExternalIdentityId authenticatedIdentityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and validates current session, user-status, security-version, and
    /// expiration state in one authoritative operation.
    /// </summary>
    Task<ApplicationSession?> GetValidAsync(
        UserSessionId sessionId,
        DateTimeOffset utcNow,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes an active session without clearing existing evidence.</summary>
    Task<bool> RevokeAsync(
        UserSessionId sessionId,
        DateTimeOffset revokedAtUtc,
        SessionRevocationReason reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances non-security-critical activity monotonically when the configured
    /// coalescing interval has elapsed.
    /// </summary>
    Task<SessionActivityTouchResult> TouchActivityAsync(
        UserSessionId sessionId,
        DateTimeOffset observedAtUtc,
        TimeSpan minimumWriteInterval,
        CancellationToken cancellationToken = default);
}

/// <summary>Reports an optimistic-concurrency rejection of a user transition.</summary>
public sealed class AuthenticationConcurrencyException : Exception
{
    /// <summary>Initializes a concurrency failure without exposing private data.</summary>
    public AuthenticationConcurrencyException(
        UserId userId,
        SecurityVersion expectedSecurityVersion)
        : base("The platform user no longer has the expected security version.")
    {
        if (userId == default || expectedSecurityVersion == default)
        {
            throw new ArgumentException(
                "A user identity and expected security version are required.");
        }

        UserId = userId;
        ExpectedSecurityVersion = expectedSecurityVersion;
    }

    /// <summary>Gets the stale platform-user identity.</summary>
    public UserId UserId { get; }

    /// <summary>Gets the stale security version.</summary>
    public SecurityVersion ExpectedSecurityVersion { get; }
}
