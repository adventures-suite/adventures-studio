using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AdventuresSuite.Identity.ExternalId;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Authorization.Persistence;

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
            new FixedCertificateSource(certificate),
            Now);
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
        Assert.True(options.ProtocolValidator.RequireStateValidation);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.True(options.TokenValidationParameters.RequireSignedTokens);
        Assert.True(options.TokenValidationParameters.RequireExpirationTime);
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
                new FixedCertificateSource(certificate),
                Now));
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

    private sealed class FixedClock : IAuthenticationClock
    {
        public DateTimeOffset GetUtcNow() => Now;
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
