using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AdventuresSuite.Identity.ExternalId;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using TheSimontonAdventures.Web.Authorization;
using AdventuresSuite.Identity.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the production External ID adapter's protocol and trust boundaries.</summary>
public sealed class ExternalIdAdapterTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Issuer and subject retain exact ordinal case and Unicode identity.</summary>
    [Theory]
    [InlineData("https://issuer.example.com/v2.0", "Person")]
    [InlineData("https://Issuer.example.com/v2.0", "person")]
    [InlineData("https://issuer.example.com/v2.0", "caf\u00e9")]
    [InlineData("https://issuer.example.com/v2.0", "cafe\u0301")]
    public void Map_ValidatedPrincipal_PreservesExactIdentity(string issuer, string subject)
    {
        var key = ExternalIdClaims.Map(Principal(issuer, subject), Provider());

        Assert.Equal(issuer, key.Issuer.Value);
        Assert.Equal(subject, key.Subject.Value);
    }

    /// <summary>Mutable profile claims cannot influence or replace immutable identity claims.</summary>
    [Fact]
    public void Map_ProfileClaims_AreIgnored()
    {
        var principal = Principal("https://issuer.example.com/v2.0", "immutable-subject",
            new Claim("email", "other@example.com"),
            new Claim("name", "Mutable Name"),
            new Claim("oid", "mutable-object-id"));

        var key = ExternalIdClaims.Map(principal, Provider());

        Assert.Equal("immutable-subject", key.Subject.Value);
    }

    /// <summary>Missing, duplicate, malformed, and oversized immutable claims fail closed.</summary>
    [Fact]
    public void Map_InvalidIdentityClaims_ThrowsGenericFailure()
    {
        Assert.Throws<InvalidOperationException>(() => ExternalIdClaims.Map(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject")])), Provider()));
        Assert.Throws<InvalidOperationException>(() => ExternalIdClaims.Map(
            Principal("https://issuer.example.com/v2.0", "subject", new Claim("sub", "second")),
            Provider()));
        Assert.Throws<ArgumentException>(() => ExternalIdClaims.Map(
            Principal("http://issuer.example.com", "subject"), Provider()));
        Assert.Throws<ArgumentException>(() => ExternalIdClaims.Map(
            Principal("https://issuer.example.com/v2.0", new string('x', 256)), Provider()));
    }

    /// <summary>OIDC is configured for confidential code flow and strict token validation.</summary>
    [Fact]
    public void AddExternalId_ConfiguresHardenedCodeFlowWithoutTokenPersistence()
    {
        using var certificate = Certificate(Now.AddDays(-1), Now.AddDays(30), clientAuthentication: true);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAuthentication().AddAdventuresSuiteExternalId(
            Configuration(),
            new FixedSignedAssertionProvider());
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(ExternalIdAuthenticationExtensions.Scheme);

        Assert.Equal(OpenIdConnectResponseType.Code, options.ResponseType);
        Assert.True(options.UsePkce);
        Assert.False(options.SaveTokens);
        Assert.False(options.GetClaimsFromUserInfoEndpoint);
        Assert.False(options.MapInboundClaims);
        Assert.True(options.RequireHttpsMetadata);
        Assert.Equal(TimeSpan.FromSeconds(30), options.BackchannelTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), options.RemoteAuthenticationTimeout);
        Assert.True(options.ProtocolValidator.RequireNonce);
        // ASP.NET Core unprotects state and validates its correlation cookie before
        // invoking the protocol validator, after deliberately clearing message.State.
        Assert.False(options.ProtocolValidator.RequireStateValidation);
        Assert.True(options.CorrelationCookie.HttpOnly);
        Assert.Equal(SameSiteMode.None, options.CorrelationCookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, options.CorrelationCookie.SecurePolicy);
        Assert.True(options.NonceCookie.HttpOnly);
        Assert.Equal(SameSiteMode.None, options.NonceCookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, options.NonceCookie.SecurePolicy);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.True(options.TokenValidationParameters.RequireSignedTokens);
        Assert.True(options.TokenValidationParameters.RequireExpirationTime);
    }

    /// <summary>The initial OIDC code redemption carries only the remote-signed client assertion.</summary>
    [Fact]
    public async Task AuthorizationCodeRedemption_AttachesClientAssertionForExactAuthorityHost()
    {
        var signer = new FixedSignedAssertionProvider();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAuthentication().AddAdventuresSuiteExternalId(Configuration(), signer);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(ExternalIdAuthenticationExtensions.Scheme);
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = provider;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("workspace.example.com");
        var context = new AuthorizationCodeReceivedContext(
            httpContext,
            new AuthenticationScheme(
                ExternalIdAuthenticationExtensions.Scheme,
                null,
                typeof(OpenIdConnectHandler)),
            options,
            new AuthenticationProperties())
        {
            TokenEndpointRequest = new OpenIdConnectMessage
            {
                IssuerAddress = "https://tenant.ciamlogin.com/tenant/oauth2/v2.0/token"
            }
        };

        await options.Events.AuthorizationCodeReceived(context);

        Assert.Equal("test-assertion", context.TokenEndpointRequest.ClientAssertion);
        Assert.Equal(
            "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            context.TokenEndpointRequest.ClientAssertionType);
        Assert.Equal("client-id", signer.ClientId);
        Assert.Equal(
            "https://tenant.ciamlogin.com/tenant/oauth2/v2.0/token",
            signer.TokenEndpoint?.AbsoluteUri);
    }

    /// <summary>An unexpected token endpoint never receives an assertion or remote-signing operation.</summary>
    [Fact]
    public async Task AuthorizationCodeRedemption_UnexpectedAuthorityHost_FailsClosed()
    {
        var signer = new FixedSignedAssertionProvider();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAuthentication().AddAdventuresSuiteExternalId(Configuration(), signer);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(ExternalIdAuthenticationExtensions.Scheme);
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = provider;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("workspace.example.com");
        var context = new AuthorizationCodeReceivedContext(
            httpContext,
            new AuthenticationScheme(
                ExternalIdAuthenticationExtensions.Scheme,
                null,
                typeof(OpenIdConnectHandler)),
            options,
            new AuthenticationProperties())
        {
            TokenEndpointRequest = new OpenIdConnectMessage
            {
                IssuerAddress = "https://tenant.ciamlogin.com.attacker.example/token"
            }
        };

        await options.Events.AuthorizationCodeReceived(context);

        Assert.NotNull(context.Result?.Failure);
        Assert.Null(context.TokenEndpointRequest.ClientAssertion);
        Assert.Null(signer.ClientId);
        Assert.Null(signer.TokenEndpoint);
    }

    /// <summary>The application cookie is host-only, protected, bounded, and non-sliding.</summary>
    [Fact]
    public void AddExternalId_ConfiguresMinimalHostOnlyApplicationCookie()
    {
        using var certificate = Certificate(Now.AddDays(-1), Now.AddDays(30), true);
        using var provider = ExternalIdServices(certificate);
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(ExternalIdAuthenticationExtensions.InternalCookieScheme);

        Assert.Equal("__Host-AdventuresSuite.Session", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Null(options.Cookie.Domain);
        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
        Assert.False(options.SlidingExpiration);
    }

    /// <summary>Later configuration cannot enable sliding or weaken __Host- invariants.</summary>
    [Fact]
    public void ApplicationCookieOptionsValidator_WeakenedOptions_Fails()
    {
        var options = new CookieAuthenticationOptions
        {
            SlidingExpiration = true
        };
        options.Cookie.Name = "unsafe";

        var result = new ApplicationCookieOptionsValidator().Validate(
            ExternalIdAuthenticationExtensions.InternalCookieScheme,
            options);

        Assert.True(result.Failed);
    }

    /// <summary>The protected principal contains only allowlisted session-validation metadata.</summary>
    [Fact]
    public void ApplicationCookiePrincipal_ContainsOnlyMinimalClaimsAndRoundTrips()
    {
        var ticket = new AuthenticationSessionTicket(
            new UserSessionId("session_external_01"),
            new UserId("user_external_01"),
            new SecurityVersion(4));

        var principal = ApplicationCookiePrincipal.Create(ticket, Now);
        var parsed = ApplicationCookiePrincipal.Parse(principal, Now);

        Assert.Equal(ticket, parsed?.Ticket);
        Assert.Equal(Now, parsed?.AuthenticatedAtUtc);
        Assert.Equal(ApplicationCookiePrincipal.OidcAuthenticationMethod, parsed?.AuthenticationMethod);
        Assert.Equal(5, principal.Claims.Count());
        Assert.DoesNotContain(principal.Claims, claim =>
            claim.Type.Contains("creator", StringComparison.OrdinalIgnoreCase)
            || claim.Type.Contains("email", StringComparison.OrdinalIgnoreCase)
            || claim.Type.Contains("role", StringComparison.OrdinalIgnoreCase)
            || claim.Type.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || claim.Type.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Cookie expiration is anchored to the same absolute server-session boundary.</summary>
    [Fact]
    public void ApplicationCookieLifetime_MatchesServerAbsoluteExpiration()
    {
        var properties = new AuthenticationProperties
        {
            AllowRefresh = true,
            ExpiresUtc = Now.AddDays(1)
        };

        ApplicationCookiePrincipal.ApplyLifetime(
            properties,
            Now,
            Configuration().AbsoluteSessionLifetime);

        Assert.Equal(Now, properties.IssuedUtc);
        Assert.Equal(Now.AddHours(8), properties.ExpiresUtc);
        Assert.False(properties.AllowRefresh);
    }

    /// <summary>Duplicate or malformed cookie metadata cannot create a server-session ticket.</summary>
    [Fact]
    public void ApplicationCookiePrincipal_MalformedClaims_FailsClosed()
    {
        var valid = ApplicationCookiePrincipal.Create(
            new AuthenticationSessionTicket(
                new UserSessionId("session_external_01"),
                new UserId("user_external_01"),
                new SecurityVersion(1)),
            Now);
        ((ClaimsIdentity)valid.Identity!).AddClaim(new Claim(
            ApplicationCookiePrincipal.SessionIdClaim,
            "session_substitution_01"));

        Assert.Null(ApplicationCookiePrincipal.Parse(valid, Now));

        var unknown = ApplicationCookiePrincipal.Create(
            new AuthenticationSessionTicket(
                new UserSessionId("session_external_01"),
                new UserId("user_external_01"),
                new SecurityVersion(1)),
            Now);
        ((ClaimsIdentity)unknown.Identity!).AddClaim(new Claim("email", "canary@example.com"));
        Assert.Null(ApplicationCookiePrincipal.Parse(unknown, Now));

        var future = ApplicationCookiePrincipal.Create(
            new AuthenticationSessionTicket(
                new UserSessionId("session_external_01"),
                new UserId("user_external_01"),
                new SecurityVersion(1)),
            Now.AddSeconds(1));
        Assert.Null(ApplicationCookiePrincipal.Parse(future, Now));

        var oversized = ApplicationCookiePrincipal.Create(
            new AuthenticationSessionTicket(
                new UserSessionId("session_external_01"),
                new UserId("user_external_01"),
                new SecurityVersion(1)),
            Now);
        var oversizedIdentity = (ClaimsIdentity)oversized.Identity!;
        oversizedIdentity.RemoveClaim(oversizedIdentity.FindFirst(ApplicationCookiePrincipal.UserIdClaim)!);
        oversizedIdentity.AddClaim(new Claim(ApplicationCookiePrincipal.UserIdClaim, new string('a', 65)));
        Assert.Null(ApplicationCookiePrincipal.Parse(oversized, Now));

        var arbitraryMethod = ApplicationCookiePrincipal.Create(
            new AuthenticationSessionTicket(
                new UserSessionId("session_external_01"),
                new UserId("user_external_01"),
                new SecurityVersion(1)),
            Now);
        var methodIdentity = (ClaimsIdentity)arbitraryMethod.Identity!;
        methodIdentity.RemoveClaim(methodIdentity.FindFirst(
            ApplicationCookiePrincipal.AuthenticationMethodClaim)!);
        methodIdentity.AddClaim(new Claim(
            ApplicationCookiePrincipal.AuthenticationMethodClaim,
            "provider-controlled-value"));
        Assert.Null(ApplicationCookiePrincipal.Parse(arbitraryMethod, Now));
    }

    /// <summary>Explicit workspace-scheme selection cannot decrypt a cookie on another host.</summary>
    [Theory]
    [InlineData("creator.example.com")]
    [InlineData("unknown.example.com")]
    public void ApplicationCookiePolicy_NonWorkspaceHost_ForwardsToRejectingHandler(string host)
    {
        using var certificate = Certificate(Now.AddDays(-1), Now.AddDays(30), true);
        using var provider = ExternalIdServices(certificate);
        var options = provider.GetRequiredService<IOptionsMonitor<PolicySchemeOptions>>()
            .Get(ExternalIdAuthenticationExtensions.SessionScheme);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        context.Request.Headers["X-Forwarded-Host"] = "workspace.example.com";

        Assert.Equal(
            ExternalIdAuthenticationExtensions.RejectedWorkspaceScheme,
            options.ForwardDefaultSelector!(context));
    }

    /// <summary>Cookie validation rejects a wrong host before resolving persistence services.</summary>
    [Fact]
    public async Task ApplicationCookieValidation_PublicHost_RejectsBeforeSessionLookup()
    {
        using var certificate = Certificate(Now.AddDays(-1), Now.AddDays(30), true);
        using var provider = ExternalIdServices(certificate);
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(ExternalIdAuthenticationExtensions.InternalCookieScheme);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("creator.example.com");
        var principal = ApplicationCookiePrincipal.Create(
            new AuthenticationSessionTicket(
                new UserSessionId("session_external_01"),
                new UserId("user_external_01"),
                new SecurityVersion(1)),
            Now);
        var validation = new CookieValidatePrincipalContext(
            httpContext,
            new AuthenticationScheme(
                ExternalIdAuthenticationExtensions.InternalCookieScheme,
                null,
                typeof(CookieAuthenticationHandler)),
            options,
            new AuthenticationTicket(principal, ExternalIdAuthenticationExtensions.InternalCookieScheme));

        await options.Events.ValidatePrincipal(validation);

        Assert.Null(validation.Principal);
    }

    /// <summary>Every workspace cookie is revalidated against authoritative server state.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApplicationCookieValidation_UsesAuthoritativeSessionState(bool valid)
    {
        using var certificate = Certificate(Now.AddDays(-1), Now.AddDays(30), true);
        using var provider = ExternalIdServices(certificate);
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(ExternalIdAuthenticationExtensions.InternalCookieScheme);
        var authenticator = new RecordingSessionAuthenticator(valid);
        var requestServices = new ServiceCollection()
            .AddSingleton<IServerSessionAuthenticator>(authenticator)
            .AddSingleton<IAuthenticationClock>(new FixedClock())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = requestServices };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("workspace.example.com");
        var ticket = new AuthenticationSessionTicket(
            new UserSessionId("session_external_01"),
            new UserId("user_external_01"),
            new SecurityVersion(1));
        var principal = ApplicationCookiePrincipal.Create(ticket, Now);
        var validation = new CookieValidatePrincipalContext(
            httpContext,
            new AuthenticationScheme(
                ExternalIdAuthenticationExtensions.InternalCookieScheme,
                null,
                typeof(CookieAuthenticationHandler)),
            options,
            new AuthenticationTicket(principal, ExternalIdAuthenticationExtensions.InternalCookieScheme));

        await options.Events.ValidatePrincipal(validation);

        Assert.Equal(1, authenticator.CallCount);
        Assert.Equal(ticket, authenticator.Ticket);
        Assert.Equal("https://workspace.example.com", authenticator.Origin);
        Assert.Equal(valid, validation.Principal is not null);
        Assert.False(validation.ShouldRenew);
    }

    /// <summary>Callback completion requires the session created by validated-principal processing.</summary>
    [Fact]
    public async Task CallbackCompletion_MissingFreshSession_FailsClosed()
    {
        using var certificate = Certificate(Now.AddDays(-1), Now.AddDays(30), true);
        using var provider = ExternalIdServices(certificate);
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(ExternalIdAuthenticationExtensions.Scheme);
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationClock>(new FixedClock())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("workspace.example.com");
        var principal = ApplicationCookiePrincipal.Create(
            new AuthenticationSessionTicket(
                new UserSessionId("session_external_01"),
                new UserId("user_external_01"),
                new SecurityVersion(1)),
            Now);
        var context = new TicketReceivedContext(
            httpContext,
            new AuthenticationScheme(
                ExternalIdAuthenticationExtensions.Scheme,
                null,
                typeof(OpenIdConnectHandler)),
            options,
            new AuthenticationTicket(
                principal,
                new AuthenticationProperties(),
                ExternalIdAuthenticationExtensions.Scheme));

        await options.Events.TicketReceived(context);

        Assert.NotNull(context.Result?.Failure);
        Assert.Equal("Authentication failed.", context.Result?.Failure?.Message);
    }

    /// <summary>Callback completion accepts only the exact freshly established session ticket.</summary>
    [Fact]
    public async Task CallbackCompletion_ExactFreshSession_Succeeds()
    {
        using var certificate = Certificate(Now.AddDays(-1), Now.AddDays(30), true);
        using var provider = ExternalIdServices(certificate);
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(ExternalIdAuthenticationExtensions.Scheme);
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationClock>(new FixedClock())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("workspace.example.com");
        var ticket = new AuthenticationSessionTicket(
            new UserSessionId("session_external_01"),
            new UserId("user_external_01"),
            new SecurityVersion(1));
        httpContext.Features.Set(new ExternalIdSessionFeature(ticket));
        var context = new TicketReceivedContext(
            httpContext,
            new AuthenticationScheme(
                ExternalIdAuthenticationExtensions.Scheme,
                null,
                typeof(OpenIdConnectHandler)),
            options,
            new AuthenticationTicket(
                ApplicationCookiePrincipal.Create(ticket, Now),
                new AuthenticationProperties(),
                ExternalIdAuthenticationExtensions.Scheme));

        await options.Events.TicketReceived(context);

        Assert.Null(context.Result?.Failure);
    }

    /// <summary>Cancellation, timeout, and replay-like protocol failures share one safe outcome.</summary>
    [Fact]
    public async Task RemoteFailures_ReturnOneGenericWorkspaceOutcome()
    {
        using var certificate = Certificate(Now.AddDays(-1), Now.AddDays(30), true);
        using var provider = ExternalIdServices(certificate);
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(ExternalIdAuthenticationExtensions.Scheme);
        var scheme = new AuthenticationScheme(
            ExternalIdAuthenticationExtensions.Scheme,
            null,
            typeof(OpenIdConnectHandler));

        foreach (var failure in new Exception[]
                 {
                     new OperationCanceledException("provider details"),
                     new TimeoutException("provider details"),
                     new InvalidOperationException("replayed state details")
                 })
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("workspace.example.com");
            var context = new RemoteFailureContext(httpContext, scheme, options, failure);

            await options.Events.RemoteFailure(context);

            Assert.True(context.Result?.Handled);
            Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
            Assert.Equal(ExternalIdBrowserEndpoints.FailurePath, httpContext.Response.Headers.Location);
            Assert.DoesNotContain("details", httpContext.Response.Headers.Location.ToString());
        }
    }

    /// <summary>Missing, expired, future, keyless, and incorrectly purposed certificates fail closed.</summary>
    [Fact]
    public void ValidateCertificate_InvalidCertificate_ThrowsSafeFailure()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ExternalIdClientCertificateValidator.Validate(null, Now));
        using var expired = Certificate(Now.AddDays(-30), Now.AddDays(-1), true);
        using var future = Certificate(Now.AddDays(1), Now.AddDays(30), true);
        using var wrongPurpose = Certificate(Now.AddDays(-1), Now.AddDays(30), false);
        using var valid = Certificate(Now.AddDays(-1), Now.AddDays(30), true);
        using var publicOnly = X509CertificateLoader.LoadCertificate(valid.Export(X509ContentType.Cert));

        Assert.Throws<InvalidOperationException>(() =>
            ExternalIdClientCertificateValidator.Validate(expired, Now));
        Assert.Throws<InvalidOperationException>(() =>
            ExternalIdClientCertificateValidator.Validate(future, Now));
        Assert.Throws<InvalidOperationException>(() =>
            ExternalIdClientCertificateValidator.Validate(wrongPurpose, Now));
        Assert.Throws<InvalidOperationException>(() =>
            ExternalIdClientCertificateValidator.Validate(publicOnly, Now));
    }

    /// <summary>Development or disabled configuration cannot silently become a production fallback.</summary>
    [Fact]
    public void AddExternalId_NonExternalMode_Throws()
    {
        var services = new ServiceCollection();
        using var certificate = Certificate(Now.AddDays(-1), Now.AddDays(30), true);

        Assert.Throws<InvalidOperationException>(() => services.AddAuthentication()
            .AddAdventuresSuiteExternalId(
                AuthenticationConfiguration.Disabled(),
                new FixedSignedAssertionProvider()));
    }

    /// <summary>Only the exact configured workspace origin can activate OIDC processing.</summary>
    [Theory]
    [InlineData("https", "workspace.example.com", true)]
    [InlineData("https", "creator.example.com", false)]
    [InlineData("https", "unknown.example.com", false)]
    [InlineData("http", "workspace.example.com", false)]
    [InlineData("https", "workspace.example.com:444", false)]
    public void WorkspaceGuard_RequiresCanonicalOrigin(
        string scheme,
        string host,
        bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = HostString.FromUriComponent(host);

        Assert.Equal(
            expected,
            ExternalIdAuthenticationExtensions.IsWorkspaceRequest(
                context.Request,
                Configuration()));
    }

    /// <summary>Caller-supplied forwarding headers cannot turn a public host into the workspace.</summary>
    [Fact]
    public void WorkspaceGuard_ForgedForwardedHeaders_AreIgnored()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("creator.example.com");
        context.Request.Headers["X-Forwarded-Host"] = "workspace.example.com";
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        Assert.False(ExternalIdAuthenticationExtensions.IsWorkspaceRequest(
            context.Request,
            Configuration()));
    }

    /// <summary>A failed session write rolls identity creation back in the same transaction.</summary>
    [Fact]
    public async Task EstablishSessionAsync_SessionWriteFails_DoesNotCommitIdentity()
    {
        var transaction = new AtomicAuthenticationTransaction(failSessionWrite: true);
        var issuer = new ExternalIdSessionIssuer(
            Configuration(),
            new AtomicPersistenceFactory(transaction),
            new DeterministicIdentityGenerator(),
            new FixedClock());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            issuer.EstablishSessionAsync(
                Principal("https://issuer.example.com/v2.0", "subject")));

        Assert.Equal("Authentication could not be completed.", exception.Message);
        Assert.True(transaction.Disposed);
        Assert.False(transaction.Committed);
        Assert.True(transaction.IdentityResolved);
    }

    private static ClaimsPrincipal Principal(
        string issuer,
        string subject,
        params Claim[] additionalClaims) =>
        new(new ClaimsIdentity(
            [new Claim("iss", issuer), new Claim("sub", subject), .. additionalClaims],
            "validated-oidc"));

    private static ExternalIdentityProviderId Provider() => new("entra_external_id");

    private static AuthenticationConfiguration Configuration() => new(
        AuthenticationMode.ExternalProvider,
        "https://workspace.example.com",
        Provider(),
        "https://tenant.ciamlogin.com/tenant/v2.0",
        "client-id",
        "certificate-reference",
        "/signin-oidc",
        "/signout-callback-oidc",
        TimeSpan.FromHours(8),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5));

    private static ServiceProvider ExternalIdServices(X509Certificate2 certificate)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAuthentication().AddAdventuresSuiteExternalId(
            Configuration(),
            new FixedSignedAssertionProvider());
        return services.BuildServiceProvider();
    }

    private static X509Certificate2 Certificate(
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool clientAuthentication)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Slice5D-Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature,
            critical: true));
        var usages = new OidCollection
        {
            new(clientAuthentication ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1")
        };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, true));
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private sealed class FixedCertificateSource(X509Certificate2 certificate)
        : IExternalIdClientCertificateSource
    {
        public X509Certificate2 Resolve(string certificateReference) => certificate;
    }

    private sealed class FixedSignedAssertionProvider : IExternalIdClientAssertionProvider
    {
        public CredentialSource CredentialSource => CredentialSource.CustomSignedAssertion;
        public string Name => "TestSignedAssertion";
        public string? ClientId { get; private set; }
        public Uri? TokenEndpoint { get; private set; }

        public Task LoadIfNeededAsync(
            CredentialDescription credentialDescription,
            CredentialSourceLoaderParameters? parameters = null)
        {
            credentialDescription.CachedValue = new FixedClientAssertion();
            credentialDescription.Skip = false;
            return Task.CompletedTask;
        }

        public Task<string> CreateClientAssertionAsync(
            string clientId,
            Uri tokenEndpoint,
            CancellationToken cancellationToken = default)
        {
            ClientId = clientId;
            TokenEndpoint = tokenEndpoint;
            return Task.FromResult("test-assertion");
        }
    }

    private sealed class FixedClientAssertion : ClientAssertionProviderBase
    {
        protected override Task<ClientAssertion> GetClientAssertionAsync(
            AssertionRequestOptions? assertionRequestOptions) =>
            Task.FromResult(new ClientAssertion("test-assertion", Now.AddMinutes(5)));
    }

    private sealed class FixedClock : IAuthenticationClock
    {
        public DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingSessionAuthenticator(bool valid) : IServerSessionAuthenticator
    {
        public int CallCount { get; private set; }
        public string? Origin { get; private set; }
        public AuthenticationSessionTicket? Ticket { get; private set; }

        public Task<SessionAuthenticationResult> AuthenticateAsync(
            string requestOrigin,
            AuthenticationSessionTicket? ticket,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Origin = requestOrigin;
            Ticket = ticket;
            return Task.FromResult(valid
                ? SessionAuthenticationResult.Authenticated(new ActorIdentity(
                    ActorType.Human,
                    ticket!.UserId.Value,
                    ticket.UserId))
                : SessionAuthenticationResult.Failed());
        }
    }

    private sealed class DeterministicIdentityGenerator : IAuthenticationIdentityGenerator
    {
        public UserId CreateUserId() => new("user_external_01");
        public ExternalIdentityId CreateExternalIdentityId() => new("external_identity_01");
        public UserSessionId CreateSessionId() => new("session_external_01");
    }

    private sealed class AtomicPersistenceFactory(AtomicAuthenticationTransaction transaction)
        : IAuthenticationPersistenceTransactionFactory
    {
        public Task<IAuthenticationPersistenceTransaction> BeginAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IAuthenticationPersistenceTransaction>(transaction);

        public Task<ExternalIdentityMapping> ResolveOrCreateUserAsync(
            PlatformUser proposedUser,
            ExternalIdentityMapping proposedExternalIdentity,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The non-transactional path must not be used.");
    }

    private sealed class AtomicAuthenticationTransaction(bool failSessionWrite)
        : IAuthenticationPersistenceTransaction
    {
        private ExternalIdentityMapping? mapping;
        private PlatformUser? user;

        public bool IdentityResolved { get; private set; }
        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }
        public IPlatformUserRepository Users => new AtomicUserRepository(() => user);
        public IExternalIdentityRepository ExternalIdentities =>
            new AtomicExternalIdentityRepository(() => mapping);
        public IUserSessionRepository Sessions => new FailingSessionRepository(failSessionWrite);

        public Task<ExternalIdentityMapping> ResolveOrCreateUserAsync(
            PlatformUser proposedUser,
            ExternalIdentityMapping proposedExternalIdentity,
            CancellationToken cancellationToken = default)
        {
            user = proposedUser;
            mapping = proposedExternalIdentity;
            IdentityResolved = true;
            return Task.FromResult(proposedExternalIdentity);
        }

        public Task CreateUserWithIdentityAsync(
            PlatformUser createdUser,
            ExternalIdentityMapping externalIdentity,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AtomicUserRepository(Func<PlatformUser?> user) : IPlatformUserRepository
    {
        public Task<PlatformUser?> GetAsync(UserId userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(user());
        public Task AddAsync(PlatformUser value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task UpdateAsync(PlatformUser value, SecurityVersion expectedSecurityVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class AtomicExternalIdentityRepository(Func<ExternalIdentityMapping?> mapping)
        : IExternalIdentityRepository
    {
        public Task<ExternalIdentityMapping?> GetByKeyAsync(
            ExternalIdentityKey key,
            CancellationToken cancellationToken = default) => Task.FromResult(mapping());
        public Task AddAsync(ExternalIdentityMapping value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> DisableAsync(ExternalIdentityId externalIdentityId, DateTimeOffset disabledAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FailingSessionRepository(bool fail) : IUserSessionRepository
    {
        public Task AddAsync(ApplicationSession session, ExternalIdentityId authenticatedIdentityId,
            CancellationToken cancellationToken = default) => fail
                ? throw new InvalidOperationException("private database details")
                : Task.CompletedTask;
        public Task<ApplicationSession?> GetAsync(UserSessionId sessionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ApplicationSession?> GetValidAsync(UserSessionId sessionId, DateTimeOffset utcNow,
            TimeSpan idleTimeout, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RevokeAsync(UserSessionId sessionId, DateTimeOffset revokedAtUtc,
            SessionRevocationReason reason, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<SessionActivityTouchResult> TouchActivityAsync(UserSessionId sessionId,
            DateTimeOffset observedAtUtc, TimeSpan minimumWriteInterval,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
