using System.Security.Claims;
using AdventuresSuite.Identity.ExternalId;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Authorization.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the narrow browser endpoint and redirect-safety increment.</summary>
public sealed class ExternalIdBrowserEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Only unambiguous bounded local workspace targets are accepted.</summary>
    [Theory]
    [InlineData("/", true)]
    [InlineData("/plans", true)]
    [InlineData("/plans/plan_01?tab=itinerary", true)]
    [InlineData("https://evil.example/path", false)]
    [InlineData("//evil.example/path", false)]
    [InlineData("/\\evil.example", false)]
    [InlineData("/%2f%2fevil.example", false)]
    [InlineData("/%252f%252fevil.example", false)]
    [InlineData("/%5cevil.example", false)]
    [InlineData("/%255cevil.example", false)]
    [InlineData("/%", false)]
    [InlineData("/https:evil.example", false)]
    [InlineData("/../plans", false)]
    [InlineData("/%2e%2e/plans", false)]
    [InlineData("/evil.example/path", false)]
    [InlineData("/authentication/sign-in", false)]
    [InlineData("/authentication/failure/again", false)]
    [InlineData("/signin-oidc", false)]
    [InlineData("/plans#https://evil.example", false)]
    [InlineData("/plans\r\nLocation:https://evil.example", false)]
    public void ReturnTarget_EnforcesStrictLocalPolicy(string target, bool expected)
    {
        Assert.Equal(expected, WorkspaceReturnTarget.IsValid(target));
        Assert.Equal(expected ? target : "/", WorkspaceReturnTarget.ValidateOrDefault(target));
    }

    /// <summary>Oversized and absent targets safely fall back to the workspace root.</summary>
    [Fact]
    public void ReturnTarget_AbsentOrOversized_DefaultsToRoot()
    {
        Assert.False(WorkspaceReturnTarget.IsValid(null));
        Assert.Equal("/", WorkspaceReturnTarget.ValidateOrDefault(null));
        Assert.False(WorkspaceReturnTarget.IsValid("/" + new string('a', 512)));
    }

    /// <summary>Sign-in challenges only from the exact canonical workspace.</summary>
    [Fact]
    public async Task SignIn_WorkspaceOnly_UsesSafeReturnTarget()
    {
        var authentication = new RecordingAuthenticationService();
        var workspace = Context("workspace.example.com", authentication);
        var challenge = await ExternalIdBrowserEndpoints.BeginSignInAsync(
            workspace,
            Configuration(),
            "/plans");
        await challenge.ExecuteAsync(workspace);

        Assert.Equal(ExternalIdAuthenticationExtensions.Scheme, authentication.ChallengeScheme);
        Assert.Equal("/plans", authentication.ChallengeProperties?.RedirectUri);

        var publicHost = Context("creator.example.com", authentication);
        var rejected = await ExternalIdBrowserEndpoints.BeginSignInAsync(
            publicHost,
            Configuration(),
            "/plans");
        await rejected.ExecuteAsync(publicHost);
        Assert.Equal(StatusCodes.Status404NotFound, publicHost.Response.StatusCode);
    }

    /// <summary>Successful sign-out commits revocation before deleting the cookie.</summary>
    [Fact]
    public async Task SignOut_RevokesAndCommitsBeforeCookieDeletion()
    {
        var events = new List<string>();
        var authentication = new RecordingAuthenticationService(events);
        var transaction = new RecordingTransaction(events, revokeResult: true);
        var context = Context("workspace.example.com", authentication);
        context.User = Principal();

        var result = await ExternalIdBrowserEndpoints.CompleteSignOutAsync(
            context,
            Configuration(),
            new FixedClock(),
            new RecordingTransactionFactory(transaction),
            "/plans");
        await result.ExecuteAsync(context);

        Assert.Equal(["revoke", "commit", "dispose", "cookie-delete"], events);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/plans", context.Response.Headers.Location);
    }

    /// <summary>A persistence failure leaves the cookie intact and returns one generic failure.</summary>
    [Fact]
    public async Task SignOut_RevocationFailure_DoesNotDeleteCookieOrLoop()
    {
        var events = new List<string>();
        var authentication = new RecordingAuthenticationService(events);
        var transaction = new RecordingTransaction(events, revokeResult: false);
        var context = Context("workspace.example.com", authentication);
        context.User = Principal();

        var result = await ExternalIdBrowserEndpoints.CompleteSignOutAsync(
            context,
            Configuration(),
            new FixedClock(),
            new RecordingTransactionFactory(transaction),
            "/plans");
        await result.ExecuteAsync(context);

        Assert.Equal(["revoke", "dispose"], events);
        Assert.Null(authentication.SignOutScheme);
        Assert.Equal(ExternalIdBrowserEndpoints.FailurePath, context.Response.Headers.Location);
    }

    /// <summary>Public and unknown hosts cannot execute sign-out or touch persistence.</summary>
    [Theory]
    [InlineData("creator.example.com")]
    [InlineData("unknown.example.com")]
    public async Task SignOut_NonWorkspaceHost_IsNotFoundBeforePersistence(string host)
    {
        var authentication = new RecordingAuthenticationService();
        var transaction = new RecordingTransaction([], revokeResult: true);
        var context = Context(host, authentication);
        context.User = Principal();

        var result = await ExternalIdBrowserEndpoints.CompleteSignOutAsync(
            context,
            Configuration(),
            new FixedClock(),
            new RecordingTransactionFactory(transaction),
            "/");
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(transaction.RevokeCalled);
        Assert.Null(authentication.SignOutScheme);
    }

    /// <summary>The mapped mutation is POST-only and carries antiforgery metadata.</summary>
    [Fact]
    public void MapEndpoints_SignOutIsPostOnlyAndRequiresAntiforgery()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        using var routes = builder.Build();

        routes.MapAdventuresSuiteExternalIdEndpoints(Configuration());

        var signOut = ((IEndpointRouteBuilder)routes).DataSources
            .SelectMany(source => source.Endpoints)
            .Single(endpoint => endpoint.DisplayName?.Contains(
                ExternalIdBrowserEndpoints.SignOutPath,
                StringComparison.Ordinal) == true);
        Assert.Equal(["POST"], signOut.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods);
        Assert.True(signOut.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation);
    }

    /// <summary>Generic outcome pages are visible only on the private workspace host.</summary>
    [Fact]
    public async Task GenericOutcomePages_DoNotActivateOnPublicHosts()
    {
        var builder = WebApplication.CreateBuilder();
        using var application = builder.Build();
        application.MapAdventuresSuiteExternalIdEndpoints(Configuration());

        var endpoints = ((IEndpointRouteBuilder)application).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToDictionary(endpoint => endpoint.RoutePattern.RawText!);
        var handler = endpoints[ExternalIdBrowserEndpoints.FailurePath].RequestDelegate!;
        var publicHost = Context("creator.example.com", new RecordingAuthenticationService());
        await handler(publicHost);

        Assert.Equal(StatusCodes.Status404NotFound, publicHost.Response.StatusCode);
        Assert.Equal(0, publicHost.Response.ContentLength ?? 0);
    }

    private static DefaultHttpContext Context(string host, IAuthenticationService authentication)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(authentication);
        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        return context;
    }

    private static ClaimsPrincipal Principal()
    {
        var ticket = new AuthenticationSessionTicket(
            new UserSessionId("session_external_01"),
            new UserId("user_external_01"),
            new SecurityVersion(1));
        return ApplicationCookiePrincipal.Create(ticket, Now);
    }

    private static AuthenticationConfiguration Configuration() => new(
        AuthenticationMode.ExternalProvider,
        "https://workspace.example.com",
        new ExternalIdentityProviderId("entra_external_id"),
        "https://tenant.ciamlogin.com/tenant/v2.0",
        "client-id",
        "certificate-reference",
        "/signin-oidc",
        "/signout-callback-oidc",
        TimeSpan.FromHours(8),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5));

    private sealed class FixedClock : IAuthenticationClock
    {
        public DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingAuthenticationService(List<string>? events = null)
        : IAuthenticationService
    {
        public string? ChallengeScheme { get; private set; }
        public AuthenticationProperties? ChallengeProperties { get; private set; }
        public string? SignOutScheme { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            ChallengeScheme = scheme;
            ChallengeProperties = properties;
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutScheme = scheme;
            events?.Add("cookie-delete");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTransactionFactory(RecordingTransaction transaction)
        : IAuthenticationPersistenceTransactionFactory
    {
        public Task<IAuthenticationPersistenceTransaction> BeginAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IAuthenticationPersistenceTransaction>(transaction);

        public Task<ExternalIdentityMapping> ResolveOrCreateUserAsync(
            PlatformUser proposedUser,
            ExternalIdentityMapping proposedExternalIdentity,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingTransaction(List<string> events, bool revokeResult)
        : IAuthenticationPersistenceTransaction
    {
        private readonly RecordingSessionRepository sessions = new(events, revokeResult);
        public bool RevokeCalled => sessions.RevokeCalled;
        public IPlatformUserRepository Users => throw new NotSupportedException();
        public IExternalIdentityRepository ExternalIdentities => throw new NotSupportedException();
        public IUserSessionRepository Sessions => sessions;
        public Task<ExternalIdentityMapping> ResolveOrCreateUserAsync(PlatformUser proposedUser,
            ExternalIdentityMapping proposedExternalIdentity,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateUserWithIdentityAsync(PlatformUser user, ExternalIdentityMapping externalIdentity,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            events.Add("commit");
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            events.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingSessionRepository(List<string> events, bool result)
        : IUserSessionRepository
    {
        public bool RevokeCalled { get; private set; }
        public Task<bool> RevokeAsync(UserSessionId sessionId, DateTimeOffset revokedAtUtc,
            SessionRevocationReason reason, CancellationToken cancellationToken = default)
        {
            RevokeCalled = true;
            events.Add("revoke");
            return Task.FromResult(result);
        }
        public Task<ApplicationSession?> GetAsync(UserSessionId sessionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(ApplicationSession session, ExternalIdentityId authenticatedIdentityId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ApplicationSession?> GetValidAsync(UserSessionId sessionId, DateTimeOffset utcNow,
            TimeSpan idleTimeout, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SessionActivityTouchResult> TouchActivityAsync(UserSessionId sessionId,
            DateTimeOffset observedAtUtc, TimeSpan minimumWriteInterval,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
