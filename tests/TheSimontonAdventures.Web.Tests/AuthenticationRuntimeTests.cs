using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Authorization.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies deterministic Slice 5C authentication boundaries.</summary>
public sealed class AuthenticationRuntimeTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Ensures an authoritative session maps only to a human actor.</summary>
    [Fact]
    public async Task AuthenticateAsync_ValidSession_ReturnsHumanActorAndTouchesActivity()
    {
        var session = Session();
        var persistence = new FakePersistenceFactory(session);
        var authenticator = Authenticator(persistence);

        var result = await authenticator.AuthenticateAsync(
            "https://workspace.example.com",
            Ticket(session));

        Assert.Equal(SessionAuthenticationOutcome.Authenticated, result.Outcome);
        Assert.Equal(ActorType.Human, result.Actor?.Type);
        Assert.Equal(session.UserId, result.Actor?.UserId);
        Assert.Equal(session.UserId.Value, result.Actor?.ActorId);
        Assert.True(persistence.Transaction.SessionsRepository.Touched);
        Assert.True(persistence.Transaction.Committed);
    }

    /// <summary>Ensures authentication never manufactures membership or permission state.</summary>
    [Fact]
    public async Task AuthenticateAsync_ValidSession_ProducesIdentityOnly()
    {
        var session = Session();
        var result = await Authenticator(new FakePersistenceFactory(session))
            .AuthenticateAsync("https://workspace.example.com", Ticket(session));

        Assert.NotNull(result.Actor);
        Assert.DoesNotContain(
            result.Actor!.GetType().GetProperties(),
            property => property.Name.Contains("Membership", StringComparison.Ordinal)
                || property.Name.Contains("Permission", StringComparison.Ordinal)
                || property.Name.Contains("Creator", StringComparison.Ordinal));
    }

    /// <summary>Ensures only the canonical workspace origin can activate private authentication.</summary>
    [Theory]
    [InlineData("https://creator.example.com")]
    [InlineData("https://unknown.example.com")]
    [InlineData("http://workspace.example.com")]
    [InlineData("https://workspace.example.com:444")]
    [InlineData("https://workspace.example.com/path")]
    [InlineData("not-an-origin")]
    public async Task AuthenticateAsync_NonWorkspaceOrigin_FailsBeforePersistence(string origin)
    {
        var persistence = new FakePersistenceFactory(Session());

        var result = await Authenticator(persistence).AuthenticateAsync(origin, Ticket(Session()));

        Assert.Equal(SessionAuthenticationOutcome.AuthenticationFailed, result.Outcome);
        Assert.Null(result.Actor);
        Assert.Equal(0, persistence.BeginCount);
    }

    /// <summary>Ensures a missing ticket remains safely anonymous.</summary>
    [Fact]
    public async Task AuthenticateAsync_MissingTicket_ReturnsAnonymousWithoutPersistence()
    {
        var persistence = new FakePersistenceFactory(Session());

        var result = await Authenticator(persistence)
            .AuthenticateAsync("https://workspace.example.com", null);

        Assert.Equal(SessionAuthenticationOutcome.Anonymous, result.Outcome);
        Assert.Null(result.Actor);
        Assert.Equal(0, persistence.BeginCount);
    }

    /// <summary>Ensures every invalid authoritative state has one non-disclosing outcome.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("stale-version")]
    [InlineData("disabled-user")]
    [InlineData("disabled-mapping")]
    public async Task AuthenticateAsync_InvalidAuthoritativeState_FailsWithoutDisclosure(string scenario)
    {
        var persistence = new FakePersistenceFactory(null)
        {
            FailureScenario = scenario
        };

        var result = await Authenticator(persistence).AuthenticateAsync(
            "https://workspace.example.com",
            Ticket(Session()));

        Assert.Equal(SessionAuthenticationOutcome.AuthenticationFailed, result.Outcome);
        Assert.Null(result.Actor);
        Assert.False(persistence.Transaction.Committed);
        Assert.False(persistence.Transaction.SessionsRepository.Touched);
    }

    /// <summary>Ensures ticket values cannot be substituted across users or versions.</summary>
    [Theory]
    [InlineData("user_other", 1)]
    [InlineData("user_alpha", 2)]
    public async Task AuthenticateAsync_SubstitutedTicket_FailsClosed(string userId, long version)
    {
        var authoritative = Session();
        var ticket = new AuthenticationSessionTicket(
            authoritative.Id,
            new UserId(userId),
            new SecurityVersion(version));

        var result = await Authenticator(new FakePersistenceFactory(authoritative))
            .AuthenticateAsync("https://workspace.example.com", ticket);

        Assert.Equal(SessionAuthenticationOutcome.AuthenticationFailed, result.Outcome);
        Assert.Null(result.Actor);
    }

    /// <summary>Ensures one valid session cannot authenticate a different session identity.</summary>
    [Fact]
    public async Task AuthenticateAsync_CrossSessionSubstitution_FailsClosed()
    {
        var authoritative = Session();
        var substituted = new AuthenticationSessionTicket(
            new UserSessionId("session_other"),
            authoritative.UserId,
            authoritative.SecurityVersion);

        var result = await Authenticator(new FakePersistenceFactory(authoritative))
            .AuthenticateAsync("https://workspace.example.com", substituted);

        Assert.Equal(SessionAuthenticationOutcome.AuthenticationFailed, result.Outcome);
        Assert.Null(result.Actor);
    }

    /// <summary>Ensures persistence failures use the same non-disclosing result.</summary>
    [Fact]
    public async Task AuthenticateAsync_PersistenceFailure_FailsWithoutPrivateDetails()
    {
        var persistence = new FakePersistenceFactory(Session())
        {
            ThrowOnBegin = true
        };

        var result = await Authenticator(persistence).AuthenticateAsync(
            "https://workspace.example.com",
            Ticket(Session()));

        Assert.Equal(SessionAuthenticationOutcome.AuthenticationFailed, result.Outcome);
        Assert.Null(result.Actor);
        Assert.DoesNotContain("private identity", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("connection details", result.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Ensures a concurrent invalidation during activity touching fails closed.</summary>
    [Fact]
    public async Task AuthenticateAsync_SessionUnavailableDuringTouch_FailsClosed()
    {
        var persistence = new FakePersistenceFactory(Session());
        persistence.Transaction.SessionsRepository.TouchResult =
            SessionActivityTouchResult.SessionUnavailable;

        var result = await Authenticator(persistence).AuthenticateAsync(
            "https://workspace.example.com",
            Ticket(Session()));

        Assert.Equal(SessionAuthenticationOutcome.AuthenticationFailed, result.Outcome);
        Assert.False(persistence.Transaction.Committed);
    }

    /// <summary>Ensures development authentication cannot activate outside Development.</summary>
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Test")]
    [InlineData("development")]
    public void DevelopmentAdapter_NonDevelopmentEnvironment_Throws(string environmentName)
    {
        Assert.Throws<InvalidOperationException>(() => new DevelopmentAuthenticationAdapter(
            environmentName,
            DevelopmentConfiguration(),
            DevelopmentIdentity(),
            new FakePersistenceFactory(null),
            new DeterministicIdentityGenerator(),
            new FixedClock()));
    }

    /// <summary>Ensures the development adapter uses only its server-configured identity.</summary>
    [Fact]
    public async Task DevelopmentAdapter_UsesFixedIdentityAndDeterministicGenerators()
    {
        var persistence = new FakePersistenceFactory(null);
        var adapter = new DevelopmentAuthenticationAdapter(
            "Development",
            DevelopmentConfiguration(),
            DevelopmentIdentity(),
            persistence,
            new DeterministicIdentityGenerator(),
            new FixedClock());

        var resolved = await adapter.ResolveIdentityAsync();

        Assert.Equal("user_development", resolved.UserId.Value);
        Assert.Equal("identity_development", resolved.Id.Value);
        Assert.Equal("synthetic-subject", resolved.Key.Subject.Value);
        Assert.Equal(Now, resolved.LastAuthenticatedAtUtc);
        Assert.Equal(0, typeof(DevelopmentAuthenticationAdapter)
            .GetMethod(nameof(DevelopmentAuthenticationAdapter.ResolveIdentityAsync))!
            .GetParameters()
            .Count(parameter => parameter.ParameterType != typeof(CancellationToken)));
    }

    /// <summary>Ensures deterministic development authentication creates a server session.</summary>
    [Fact]
    public async Task DevelopmentAdapter_EstablishSession_UsesAuthoritativeUserAndMapping()
    {
        var persistence = new FakePersistenceFactory(null);
        var adapter = new DevelopmentAuthenticationAdapter(
            "Development",
            DevelopmentConfiguration(),
            DevelopmentIdentity(),
            persistence,
            new DeterministicIdentityGenerator(),
            new FixedClock());

        var ticket = await adapter.EstablishSessionAsync();

        Assert.Equal("session_development", ticket.SessionId.Value);
        Assert.Equal("user_development", ticket.UserId.Value);
        Assert.Equal(1, ticket.SecurityVersion.Value);
        Assert.Equal("identity_development", persistence.Transaction.SessionsRepository.AddedIdentityId?.Value);
        Assert.Equal(Now.AddHours(8), persistence.Transaction.SessionsRepository.AddedSession?.AbsoluteExpiresAtUtc);
    }

    /// <summary>Ensures the test-only scheme refuses deployed environment names.</summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void TestOnlyScheme_DeployedEnvironment_Throws(string environmentName)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new TestOnlyDeterministicAuthenticationScheme(environmentName, Ticket(Session())));
    }

    private static ServerSessionAuthenticator Authenticator(FakePersistenceFactory persistence) =>
        new(DevelopmentConfiguration(), persistence, new FixedClock());

    private static AuthenticationSessionTicket Ticket(ApplicationSession session) =>
        new(session.Id, session.UserId, session.SecurityVersion);

    private static ApplicationSession Session() => new(
        new UserSessionId("session_alpha"),
        new UserId("user_alpha"),
        new SecurityVersion(1),
        Now.AddMinutes(-10),
        Now.AddMinutes(-10),
        Now.AddHours(1));

    private static AuthenticationConfiguration DevelopmentConfiguration() => new(
        AuthenticationMode.Development,
        "https://workspace.example.com",
        new ExternalIdentityProviderId("development"),
        null, null, null, null, null,
        TimeSpan.FromHours(8),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5));

    private static DevelopmentAuthenticationIdentity DevelopmentIdentity() => new(new ExternalIdentityKey(
        new ExternalIdentityProviderId("development"),
        new ExternalIdentityIssuer("https://development.invalid/issuer"),
        new ExternalIdentitySubject("synthetic-subject")));

    private sealed class FixedClock : IAuthenticationClock
    {
        public DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class DeterministicIdentityGenerator : IAuthenticationIdentityGenerator
    {
        public UserId CreateUserId() => new("user_development");
        public ExternalIdentityId CreateExternalIdentityId() => new("identity_development");
        public UserSessionId CreateSessionId() => new("session_development");
    }

    /// <summary>
    /// Test-assembly-only scheme with no request input. Application factories
    /// may install it only under the synthetic Test environment.
    /// </summary>
    private sealed class TestOnlyDeterministicAuthenticationScheme
    {
        public TestOnlyDeterministicAuthenticationScheme(
            string environmentName,
            AuthenticationSessionTicket ticket)
        {
            if (!string.Equals(environmentName, "Test", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The deterministic test scheme can activate only in a test host.");
            }

            Ticket = ticket;
        }

        public AuthenticationSessionTicket Ticket { get; }
    }

    private sealed class FakePersistenceFactory(ApplicationSession? validSession)
        : IAuthenticationPersistenceTransactionFactory
    {
        public FakeTransaction Transaction { get; } = new(validSession);
        public int BeginCount { get; private set; }
        public string? FailureScenario { get; init; }
        public bool ThrowOnBegin { get; init; }

        public Task<IAuthenticationPersistenceTransaction> BeginAsync(
            CancellationToken cancellationToken = default)
        {
            BeginCount++;
            if (ThrowOnBegin)
            {
                throw new InvalidOperationException(
                    "database failed with private identity and connection details");
            }

            return Task.FromResult<IAuthenticationPersistenceTransaction>(Transaction);
        }

        public Task<ExternalIdentityMapping> ResolveOrCreateUserAsync(
            PlatformUser proposedUser,
            ExternalIdentityMapping proposedExternalIdentity,
            CancellationToken cancellationToken = default)
        {
            Transaction.ResolvedUser = proposedUser;
            Transaction.ResolvedMapping = proposedExternalIdentity;
            Transaction.UserRepository.User = proposedUser;
            return Task.FromResult(proposedExternalIdentity);
        }
    }

    private sealed class FakeTransaction(ApplicationSession? validSession)
        : IAuthenticationPersistenceTransaction
    {
        public FakeSessionRepository SessionsRepository { get; } = new(validSession);
        public FakeUserRepository UserRepository { get; } = new();
        public IPlatformUserRepository Users => UserRepository;
        public IExternalIdentityRepository ExternalIdentities => throw new NotSupportedException();
        public IUserSessionRepository Sessions => SessionsRepository;
        public bool Committed { get; private set; }
        public PlatformUser? ResolvedUser { get; set; }
        public ExternalIdentityMapping? ResolvedMapping { get; set; }

        public Task CreateUserWithIdentityAsync(
            PlatformUser user,
            ExternalIdentityMapping externalIdentity,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeUserRepository : IPlatformUserRepository
    {
        public PlatformUser? User { get; set; }

        public Task<PlatformUser?> GetAsync(
            UserId userId,
            CancellationToken cancellationToken = default) => Task.FromResult(User);

        public Task AddAsync(
            PlatformUser user,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UpdateAsync(
            PlatformUser user,
            SecurityVersion expectedSecurityVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeSessionRepository(ApplicationSession? validSession) : IUserSessionRepository
    {
        public bool Touched { get; private set; }
        public ApplicationSession? AddedSession { get; private set; }
        public ExternalIdentityId? AddedIdentityId { get; private set; }
        public SessionActivityTouchResult TouchResult { get; set; } =
            SessionActivityTouchResult.Coalesced;

        public Task<ApplicationSession?> GetAsync(
            UserSessionId sessionId,
            CancellationToken cancellationToken = default) => Task.FromResult(validSession);

        public Task<ApplicationSession?> GetValidAsync(
            UserSessionId sessionId,
            DateTimeOffset utcNow,
            TimeSpan idleTimeout,
            CancellationToken cancellationToken = default) => Task.FromResult(validSession);

        public Task AddAsync(
            ApplicationSession session,
            ExternalIdentityId authenticatedIdentityId,
            CancellationToken cancellationToken = default)
        {
            AddedSession = session;
            AddedIdentityId = authenticatedIdentityId;
            return Task.CompletedTask;
        }

        public Task<bool> RevokeAsync(
            UserSessionId sessionId,
            DateTimeOffset revokedAtUtc,
            SessionRevocationReason reason,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SessionActivityTouchResult> TouchActivityAsync(
            UserSessionId sessionId,
            DateTimeOffset observedAtUtc,
            TimeSpan minimumWriteInterval,
            CancellationToken cancellationToken = default)
        {
            Touched = true;
            return Task.FromResult(TouchResult);
        }
    }
}
