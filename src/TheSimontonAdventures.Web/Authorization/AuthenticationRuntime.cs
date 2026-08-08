using TheSimontonAdventures.Web.Authorization.Persistence;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Provides deterministic UTC time to authentication operations.</summary>
public interface IAuthenticationClock
{
    /// <summary>Gets the current UTC instant.</summary>
    DateTimeOffset GetUtcNow();
}

/// <summary>Creates platform identities without accepting caller-selected values.</summary>
public interface IAuthenticationIdentityGenerator
{
    /// <summary>Creates a new stable platform-user identity.</summary>
    UserId CreateUserId();

    /// <summary>Creates a new stable external-identity mapping identity.</summary>
    ExternalIdentityId CreateExternalIdentityId();

    /// <summary>Creates a new opaque application-session identity.</summary>
    UserSessionId CreateSessionId();
}

/// <summary>Contains only the server-issued values needed to validate one session.</summary>
public sealed record AuthenticationSessionTicket
{
    /// <summary>Initializes an immutable server-issued session ticket.</summary>
    public AuthenticationSessionTicket(
        UserSessionId sessionId,
        UserId userId,
        SecurityVersion securityVersion)
    {
        if (sessionId == default || userId == default || securityVersion == default)
        {
            throw new ArgumentException("Session, user, and security-version values are required.");
        }

        SessionId = sessionId;
        UserId = userId;
        SecurityVersion = securityVersion;
    }

    /// <summary>Gets the opaque session identity.</summary>
    public UserSessionId SessionId { get; }

    /// <summary>Gets the platform user captured when the ticket was issued.</summary>
    public UserId UserId { get; }

    /// <summary>Gets the security version captured when the ticket was issued.</summary>
    public SecurityVersion SecurityVersion { get; }
}

/// <summary>Classifies a safe authentication result without disclosing record existence.</summary>
public enum SessionAuthenticationOutcome
{
    /// <summary>No server-issued session ticket was presented.</summary>
    Anonymous,
    /// <summary>The authoritative session produced an authenticated human actor.</summary>
    Authenticated,
    /// <summary>The ticket or authoritative state failed closed.</summary>
    AuthenticationFailed
}

/// <summary>Returns either an authenticated actor or a non-disclosing failure.</summary>
public sealed record SessionAuthenticationResult
{
    private SessionAuthenticationResult(
        SessionAuthenticationOutcome outcome,
        ActorIdentity? actor)
    {
        Outcome = outcome;
        Actor = actor;
    }

    /// <summary>Gets the safe result category.</summary>
    public SessionAuthenticationOutcome Outcome { get; }

    /// <summary>Gets the authenticated human actor when validation succeeded.</summary>
    public ActorIdentity? Actor { get; }

    /// <summary>Creates an anonymous result.</summary>
    public static SessionAuthenticationResult Anonymous() =>
        new(SessionAuthenticationOutcome.Anonymous, null);

    /// <summary>Creates a generic fail-closed result.</summary>
    public static SessionAuthenticationResult Failed() =>
        new(SessionAuthenticationOutcome.AuthenticationFailed, null);

    /// <summary>Creates a successful human authentication result.</summary>
    public static SessionAuthenticationResult Authenticated(ActorIdentity actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.IsHuman)
        {
            throw new ArgumentException("An authenticated session must map to a human actor.", nameof(actor));
        }

        return new(SessionAuthenticationOutcome.Authenticated, actor);
    }
}

/// <summary>Validates server-issued sessions only on the canonical workspace origin.</summary>
public interface IServerSessionAuthenticator
{
    /// <summary>Validates current authoritative session state and returns a human actor.</summary>
    Task<SessionAuthenticationResult> AuthenticateAsync(
        string requestOrigin,
        AuthenticationSessionTicket? ticket,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Performs immediate authoritative session validation and separately coalesces
/// non-security-critical activity writes.
/// </summary>
public sealed class ServerSessionAuthenticator(
    AuthenticationConfiguration configuration,
    IAuthenticationPersistenceTransactionFactory transactionFactory,
    IAuthenticationClock clock) : IServerSessionAuthenticator
{
    private readonly AuthenticationConfiguration configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IAuthenticationPersistenceTransactionFactory transactionFactory =
        transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
    private readonly IAuthenticationClock clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    /// <inheritdoc />
    public async Task<SessionAuthenticationResult> AuthenticateAsync(
        string requestOrigin,
        AuthenticationSessionTicket? ticket,
        CancellationToken cancellationToken = default)
    {
        if (!IsCanonicalWorkspaceOrigin(requestOrigin))
        {
            return ticket is null
                ? SessionAuthenticationResult.Anonymous()
                : SessionAuthenticationResult.Failed();
        }

        if (ticket is null)
        {
            return SessionAuthenticationResult.Anonymous();
        }

        var utcNow = clock.GetUtcNow();
        AuthenticationTimestamp.RequireUtc(utcNow, nameof(utcNow));

        try
        {
            await using var transaction = await transactionFactory.BeginAsync(cancellationToken);
            var session = await transaction.Sessions.GetValidAsync(
                ticket.SessionId,
                utcNow,
                configuration.IdleSessionTimeout,
                cancellationToken);

            // Compare every server-issued ticket value to prevent cross-user,
            // cross-session, and stale-version substitution.
            if (session is null
                || session.Id != ticket.SessionId
                || session.UserId != ticket.UserId
                || session.SecurityVersion != ticket.SecurityVersion)
            {
                return SessionAuthenticationResult.Failed();
            }

            var touch = await transaction.Sessions.TouchActivityAsync(
                session.Id,
                utcNow,
                configuration.ActivityTouchInterval,
                cancellationToken);
            if (touch == SessionActivityTouchResult.SessionUnavailable)
            {
                return SessionAuthenticationResult.Failed();
            }

            await transaction.CommitAsync(cancellationToken);
            return SessionAuthenticationResult.Authenticated(new ActorIdentity(
                ActorType.Human,
                session.UserId.Value,
                session.UserId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Authentication persistence failures remain indistinguishable at
            // this boundary and never fall back to ticket-only trust.
            return SessionAuthenticationResult.Failed();
        }
    }

    private bool IsCanonicalWorkspaceOrigin(string requestOrigin)
    {
        if (configuration.Mode == AuthenticationMode.Disabled
            || string.IsNullOrEmpty(configuration.WorkspaceOrigin)
            || string.IsNullOrWhiteSpace(requestOrigin)
            || !Uri.TryCreate(requestOrigin, UriKind.Absolute, out var request)
            || !Uri.TryCreate(configuration.WorkspaceOrigin, UriKind.Absolute, out var workspace)
            || request.AbsolutePath != "/"
            || !string.IsNullOrEmpty(request.Query)
            || !string.IsNullOrEmpty(request.Fragment)
            || !string.IsNullOrEmpty(request.UserInfo))
        {
            return false;
        }

        return string.Equals(request.Scheme, workspace.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.IdnHost, workspace.IdnHost, StringComparison.OrdinalIgnoreCase)
            && request.Port == workspace.Port;
    }
}

/// <summary>Defines one allowlisted synthetic identity for local development.</summary>
public sealed record DevelopmentAuthenticationIdentity
{
    /// <summary>Initializes server-owned development identity configuration.</summary>
    public DevelopmentAuthenticationIdentity(ExternalIdentityKey externalIdentity)
    {
        ExternalIdentity = externalIdentity ?? throw new ArgumentNullException(nameof(externalIdentity));
    }

    /// <summary>Gets the exact synthetic external identity.</summary>
    public ExternalIdentityKey ExternalIdentity { get; }
}

/// <summary>
/// Resolves one server-configured synthetic identity and cannot accept identity
/// selection from a request.
/// </summary>
public sealed class DevelopmentAuthenticationAdapter
{
    private readonly AuthenticationConfiguration configuration;
    private readonly DevelopmentAuthenticationIdentity identity;
    private readonly IAuthenticationPersistenceTransactionFactory transactionFactory;
    private readonly IAuthenticationIdentityGenerator identityGenerator;
    private readonly IAuthenticationClock clock;

    /// <summary>Creates an adapter that is valid only in the Development environment.</summary>
    public DevelopmentAuthenticationAdapter(
        string environmentName,
        AuthenticationConfiguration configuration,
        DevelopmentAuthenticationIdentity identity,
        IAuthenticationPersistenceTransactionFactory transactionFactory,
        IAuthenticationIdentityGenerator identityGenerator,
        IAuthenticationClock clock)
    {
        if (!string.Equals(environmentName, "Development", StringComparison.Ordinal)
            || configuration?.Mode != AuthenticationMode.Development)
        {
            throw new InvalidOperationException(
                "Development authentication can activate only in the Development environment.");
        }

        this.configuration = configuration;
        this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
        this.transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
        this.identityGenerator = identityGenerator ?? throw new ArgumentNullException(nameof(identityGenerator));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));

        if (identity.ExternalIdentity.ProviderId != configuration.ProviderId)
        {
            throw new ArgumentException(
                "The development identity must use the configured provider.",
                nameof(identity));
        }
    }

    /// <summary>Resolves the fixed server-configured identity without caller input.</summary>
    public async Task<ExternalIdentityMapping> ResolveIdentityAsync(
        CancellationToken cancellationToken = default)
    {
        var utcNow = clock.GetUtcNow();
        AuthenticationTimestamp.RequireUtc(utcNow, nameof(utcNow));
        var user = new PlatformUser(
            identityGenerator.CreateUserId(),
            PlatformUserStatus.Active,
            new SecurityVersion(1),
            utcNow,
            utcNow);
        var mapping = new ExternalIdentityMapping(
            identityGenerator.CreateExternalIdentityId(),
            identity.ExternalIdentity,
            user.Id,
            utcNow,
            utcNow);

        return await transactionFactory.ResolveOrCreateUserAsync(
            user,
            mapping,
            cancellationToken);
    }

    /// <summary>Creates a revocable application session for the fixed identity.</summary>
    public async Task<AuthenticationSessionTicket> EstablishSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var mapping = await ResolveIdentityAsync(cancellationToken);
        var utcNow = clock.GetUtcNow();
        AuthenticationTimestamp.RequireUtc(utcNow, nameof(utcNow));

        try
        {
            await using var transaction = await transactionFactory.BeginAsync(cancellationToken);
            var user = await transaction.Users.GetAsync(mapping.UserId, cancellationToken);
            if (user is null || !user.CanUseSession)
            {
                throw new InvalidOperationException();
            }

            var session = new ApplicationSession(
                identityGenerator.CreateSessionId(),
                user.Id,
                user.SecurityVersion,
                utcNow,
                utcNow,
                utcNow + configuration.AbsoluteSessionLifetime);
            await transaction.Sessions.AddAsync(session, mapping.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuthenticationSessionTicket(
                session.Id,
                session.UserId,
                session.SecurityVersion);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            throw new InvalidOperationException(
                "The development identity could not establish an application session.");
        }
    }
}
