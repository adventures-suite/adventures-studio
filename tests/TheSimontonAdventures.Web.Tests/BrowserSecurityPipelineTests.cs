using System.Net;
using System.Security.Claims;
using AdventuresSuite.Identity.ExternalId;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TheSimontonAdventures.Web.Authorization;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the Slice 5E browser and interactive-server security gates.</summary>
public sealed class BrowserSecurityPipelineTests
{
    /// <summary>Ensures all reviewed browser headers are applied together.</summary>
    [Fact]
    public async Task BrowserHeaders_AreAppliedToEveryResponse()
    {
        var context = new DefaultHttpContext();
        var middleware = new BrowserSecurityHeadersMiddleware(context =>
        {
            context.Response.Headers.Append(
                "Content-Security-Policy",
                "frame-ancestors 'self'");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions);
        Assert.Equal("DENY", context.Response.Headers.XFrameOptions);
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
        Assert.Contains("frame-ancestors 'none'", context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Contains("object-src 'none'", context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Contains("script-src 'self' 'nonce-", context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.DoesNotContain(",", context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Contains("geolocation=()", context.Response.Headers["Permissions-Policy"].ToString());
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.Equal(32, BrowserSecurityHeadersMiddleware.GetNonce(context).Length);
    }

    /// <summary>Ensures every unsafe cookie-authenticated HTTP method validates antiforgery.</summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task CookieMutation_RequiresAntiforgery(string method)
    {
        var context = AuthenticatedContext(method, "/workspace/plans");
        var antiforgery = new RecordingAntiforgery();
        var nextCalled = false;
        var middleware = new CookieAuthenticatedAntiforgeryMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, AuthenticationConfiguration.Disabled());

        await middleware.InvokeAsync(context, antiforgery);

        Assert.True(antiforgery.Validated);
        Assert.True(nextCalled);
    }

    /// <summary>Ensures Blazor transport POSTs use exact Origin enforcement instead of form tokens.</summary>
    [Fact]
    public async Task BlazorNegotiate_DoesNotInvokeFormAntiforgeryValidation()
    {
        var context = AuthenticatedContext("POST", "/_blazor/negotiate");
        var antiforgery = new RecordingAntiforgery();
        var middleware = new CookieAuthenticatedAntiforgeryMiddleware(
            _ => Task.CompletedTask,
            AuthenticationConfiguration.Disabled());

        await middleware.InvokeAsync(context, antiforgery);

        Assert.False(antiforgery.Validated);
    }

    /// <summary>Ensures an invalid antiforgery proof stops a cookie mutation before its endpoint.</summary>
    [Fact]
    public async Task CookieMutation_InvalidProof_FailsBeforeEndpoint()
    {
        var context = AuthenticatedContext("POST", "/workspace/plans");
        var antiforgery = new RecordingAntiforgery { Reject = true };
        var nextCalled = false;
        var middleware = new CookieAuthenticatedAntiforgeryMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, AuthenticationConfiguration.Disabled());

        await Assert.ThrowsAsync<AntiforgeryValidationException>(() =>
            middleware.InvokeAsync(context, antiforgery));

        Assert.False(nextCalled);
    }

    /// <summary>Ensures framework-validated endpoint metadata is not evaluated a second time.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MetadataEndpoint_UsesFrameworkAntiforgeryValidation(bool requiresValidation)
    {
        var context = AuthenticatedContext("POST", "/authentication/sign-in");
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new TestAntiforgeryMetadata(requiresValidation)),
            "metadata endpoint"));
        var antiforgery = new RecordingAntiforgery();
        var nextCalled = false;
        var middleware = new CookieAuthenticatedAntiforgeryMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, AuthenticationConfiguration.Disabled());

        await middleware.InvokeAsync(context, antiforgery);

        Assert.False(antiforgery.Validated);
        Assert.True(nextCalled);
    }

    /// <summary>Ensures future bearer APIs are not accidentally assigned browser CSRF semantics.</summary>
    [Fact]
    public async Task BearerMutation_IsExplicitlyOutsideBrowserAntiforgeryPolicy()
    {
        var context = AuthenticatedContext("POST", "/api/mobile/plans", includeCookie: false);
        context.Request.Headers.Authorization = "Bearer test-only-token";
        var antiforgery = new RecordingAntiforgery();
        var middleware = new CookieAuthenticatedAntiforgeryMiddleware(
            _ => Task.CompletedTask,
            AuthenticationConfiguration.Disabled());

        await middleware.InvokeAsync(context, antiforgery);

        Assert.False(antiforgery.Validated);
    }

    /// <summary>Ensures only exact OIDC protocol callbacks bypass browser antiforgery.</summary>
    [Theory]
    [InlineData("/signin-oidc", false)]
    [InlineData("/signout-callback-oidc", false)]
    [InlineData("/signin-oidc/extra", true)]
    [InlineData("/authentication/sign-out", true)]
    public async Task ProtocolEndpointExemption_IsExactAndBounded(
        string path,
        bool expectedValidation)
    {
        var context = AuthenticatedContext("POST", path);
        var antiforgery = new RecordingAntiforgery();
        var middleware = new CookieAuthenticatedAntiforgeryMiddleware(
            _ => Task.CompletedTask,
            ExternalConfiguration());

        await middleware.InvokeAsync(context, antiforgery);

        Assert.Equal(expectedValidation, antiforgery.Validated);
    }

    /// <summary>Ensures every Blazor transport path accepts only the exact workspace origin.</summary>
    [Theory]
    [InlineData("/_blazor/negotiate", "https://workspace.example.com", 200)]
    [InlineData("/_blazor", "https://workspace.example.com", 200)]
    [InlineData("/_blazor/initializers", null, 200)]
    [InlineData("/_blazor/initializers", "https://creator.example.com", 200)]
    [InlineData("/_blazor?id=connection", "https://creator.example.com", 403)]
    [InlineData("/_blazor?id=connection", "https://workspace.example.com.evil.test", 403)]
    [InlineData("/_blazor?id=connection", "https://WORKSPACE.EXAMPLE.COM", 200)]
    [InlineData("/_blazor?id=connection", "https://workspace.example.com:444", 403)]
    [InlineData("/_blazor?id=connection", "https://workspace%2eexample.com", 403)]
    [InlineData("/_blazor?id=connection", null, 403)]
    public async Task WorkspaceSignalR_EnforcesExactOrigin(
        string target,
        string? origin,
        int expectedStatus)
    {
        var context = new DefaultHttpContext();
        var uri = new Uri("https://workspace.example.com" + target);
        context.Request.Scheme = uri.Scheme;
        context.Request.Host = new HostString(uri.Host);
        context.Request.Path = uri.AbsolutePath;
        context.Request.QueryString = new QueryString(uri.Query);
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        var middleware = new WorkspaceSignalROriginMiddleware(
            _ => Task.CompletedTask,
            Configuration());
        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
    }

    /// <summary>Ensures a workspace cookie cannot activate a circuit on a public Creator origin.</summary>
    [Fact]
    public async Task PublicHost_WithWorkspaceCookie_IsRejectedBeforeCircuitEstablishment()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("creator.example.com");
        context.Request.Path = "/_blazor/negotiate";
        context.Request.Headers.Origin = "https://creator.example.com";
        context.Request.Headers.Cookie =
            $"{BrowserAuthenticationDefaults.ApplicationCookieName}=untrusted";
        var middleware = new WorkspaceSignalROriginMiddleware(
            _ => Task.CompletedTask,
            Configuration());

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    /// <summary>Ensures circuit revalidation uses the configured interval and canonical origin.</summary>
    [Fact]
    public async Task CircuitRevalidation_UsesAuthoritativeSessionAndConfiguredInterval()
    {
        var configuration = Configuration(TimeSpan.FromMinutes(2));
        var authenticator = new RecordingSessionAuthenticator();
        var clock = new FixedClock();
        var provider = new AdventuresSuiteCircuitAuthenticationStateProvider(
            NullLoggerFactory.Instance,
            configuration,
            authenticator,
            clock);
        var ticket = new AuthenticationSessionTicket(
            new UserSessionId("session_1"),
            new UserId("user_1"),
            new SecurityVersion(1));
        var principal = ApplicationCookiePrincipal.Create(ticket, clock.GetUtcNow());

        var valid = await provider.ValidateForTestAsync(new AuthenticationState(principal));

        Assert.True(valid);
        Assert.Equal(configuration.WorkspaceOrigin, authenticator.Origin);
        Assert.Equal(ticket, authenticator.Ticket);
        Assert.Equal(TimeSpan.FromMinutes(2), provider.IntervalForTest);
    }

    /// <summary>Ensures persistence errors invalidate rather than preserve a captured circuit principal.</summary>
    [Fact]
    public async Task CircuitRevalidation_ErrorFailsClosed()
    {
        var clock = new FixedClock();
        var provider = new AdventuresSuiteCircuitAuthenticationStateProvider(
            NullLoggerFactory.Instance,
            Configuration(),
            new RecordingSessionAuthenticator { Throw = true },
            clock);
        var principal = ApplicationCookiePrincipal.Create(
            new AuthenticationSessionTicket(
                new UserSessionId("session_1"),
                new UserId("user_1"),
                new SecurityVersion(1)),
            clock.GetUtcNow());

        Assert.False(await provider.ValidateForTestAsync(new AuthenticationState(principal)));
    }

    private static DefaultHttpContext AuthenticatedContext(
        string method,
        string path,
        bool includeCookie = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")],
            "test"));
        if (includeCookie)
        {
            context.Request.Headers.Cookie =
                $"{BrowserAuthenticationDefaults.ApplicationCookieName}=test";
        }
        return context;
    }

    private static AuthenticationConfiguration Configuration(TimeSpan? interval = null) => new(
        AuthenticationMode.Development,
        "https://workspace.example.com",
        new ExternalIdentityProviderId("development"),
        null,
        null,
        null,
        null,
        null,
        TimeSpan.FromHours(8),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(5),
        interval ?? TimeSpan.FromMinutes(5));

    private static AuthenticationConfiguration ExternalConfiguration() => new(
        AuthenticationMode.ExternalProvider,
        "https://workspace.example.com",
        new ExternalIdentityProviderId("external_id"),
        "https://login.example.com/tenant/v2.0",
        "client-id",
        "certificate-reference",
        "/signin-oidc",
        "/signout-callback-oidc",
        TimeSpan.FromHours(8),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5));

    private sealed class RecordingAntiforgery : IAntiforgery
    {
        public bool Validated { get; private set; }
        public bool Reject { get; init; }
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public void SetCookieTokenAndHeader(HttpContext httpContext) => throw new NotSupportedException();
        public Task ValidateRequestAsync(HttpContext httpContext)
        {
            Validated = true;
            if (Reject)
            {
                throw new AntiforgeryValidationException("Invalid antiforgery proof.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed record TestAntiforgeryMetadata(bool RequiresValidation) : IAntiforgeryMetadata;

    private sealed class RecordingSessionAuthenticator : IServerSessionAuthenticator
    {
        public string? Origin { get; private set; }
        public AuthenticationSessionTicket? Ticket { get; private set; }
        public bool Throw { get; init; }

        public Task<SessionAuthenticationResult> AuthenticateAsync(
            string requestOrigin,
            AuthenticationSessionTicket? ticket,
            CancellationToken cancellationToken = default)
        {
            if (Throw)
            {
                throw new InvalidOperationException("Simulated persistence failure.");
            }
            Origin = requestOrigin;
            Ticket = ticket;
            return Task.FromResult(SessionAuthenticationResult.Authenticated(
                new ActorIdentity(ActorType.Human, ticket!.UserId.Value, ticket.UserId)));
        }
    }

    private sealed class FixedClock : IAuthenticationClock
    {
        public DateTimeOffset GetUtcNow() =>
            new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    }
}
