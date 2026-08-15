using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AdventuresSuite.Api.Tests;

/// <summary>Proves real bearer transport validation without activating authoritative projections.</summary>
public sealed class CompanionBearerAuthenticationTests : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly RsaSecurityKey _key;
    private readonly BearerCompanionApiFactory _factory;

    /// <summary>Creates one isolated fictional signing authority.</summary>
    public CompanionBearerAuthenticationTests()
    {
        _key = new RsaSecurityKey(_rsa) { KeyId = "fictional-companion-test-key" };
        _factory = new BearerCompanionApiFactory(_key);
    }

    /// <summary>Ensures a valid transport identity cannot reach projections before authoritative context resolution.</summary>
    [Fact]
    public async Task ValidBearerAuthenticatesButProjectionRemainsClosed()
    {
        using var response = await SendAsync(CreateToken());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("resource_unavailable", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>Ensures caller claims cannot supply Creator, traveler, membership, ownership, or revocation authority.</summary>
    [Fact]
    public async Task CallerAuthorizationClaimsDoNotOpenProjection()
    {
        var claims = new Dictionary<string, object>
        {
            ["creator_id"] = "creator_attacker",
            ["traveler_id"] = "traveler_attacker",
            ["membership_version"] = 999,
            ["role"] = "Owner",
            ["ownership"] = true,
            ["revoked"] = false
        };
        using var response = await SendAsync(CreateToken(claims: claims));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("resource_unavailable", body, StringComparison.Ordinal);
        Assert.DoesNotContain("attacker", body, StringComparison.Ordinal);
    }

    /// <summary>Ensures signature, issuer, audience, and token-time failures are indistinguishable.</summary>
    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("not-yet-valid")]
    [InlineData("signature")]
    [InlineData("unsigned")]
    [InlineData("algorithm")]
    public async Task InvalidProtocolBindingIsUnauthorized(string variation)
    {
        using var otherRsa = RSA.Create(2048);
        var token = variation switch
        {
            "issuer" => CreateToken(issuer: "https://identity.example.test/other/v2.0"),
            "audience" => CreateToken(audience: "api://other"),
            "expired" => CreateToken(notBefore: DateTime.UtcNow.AddMinutes(-10), expires: DateTime.UtcNow.AddMinutes(-2)),
            "not-yet-valid" => CreateToken(notBefore: DateTime.UtcNow.AddMinutes(5)),
            "signature" => CreateToken(signingKey: new RsaSecurityKey(otherRsa) { KeyId = "other" }),
            "unsigned" => CreateToken(signingKey: null, unsigned: true),
            "algorithm" => CreateToken(
                signingKey: new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)),
                algorithm: SecurityAlgorithms.HmacSha256),
            _ => throw new InvalidOperationException()
        };
        using var response = await SendAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("authentication_required", body, StringComparison.Ordinal);
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
        foreach (var detail in new[] { "issuer", "audience", "signature", "expired", "not-yet-valid" })
            Assert.DoesNotContain(detail, body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ensures the delegated scope is checked as an exact space-delimited value.</summary>
    [Theory]
    [InlineData("Wrong.Scope")]
    [InlineData("Companion.Access.More")]
    [InlineData("companion.access")]
    [InlineData("api://fictional/Companion.Access")]
    public async Task MissingExactScopeIsForbidden(string scope)
    {
        using var response = await SendAsync(CreateToken(scope: scope));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Ensures alternate claim types and role values cannot substitute for delegated scope.</summary>
    [Theory]
    [InlineData("scp")]
    [InlineData("role")]
    [InlineData("roles")]
    public async Task AlternateScopeClaimsAreForbidden(string claimType)
    {
        using var response = await SendAsync(CreateToken(
            scope: null,
            claims: new Dictionary<string, object> { [claimType] = "Companion.Access" }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Ensures missing or ambiguous immutable subjects fail token validation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrDuplicateSubjectIsUnauthorized(bool duplicate)
    {
        var claims = duplicate
            ? new Dictionary<string, object> { ["sub"] = new[] { "subject-one", "subject-two" } }
            : new Dictionary<string, object>();
        using var response = await SendAsync(CreateToken(subject: null, claims: claims));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Ensures malformed immutable identity values fail without normalization.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" subject")]
    [InlineData("subject ")]
    [InlineData("subject\nvalue")]
    public async Task MalformedSubjectIsUnauthorized(string subject)
    {
        using var response = await SendAsync(CreateToken(subject: subject));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Ensures missing, duplicate, malformed, or case-altered issuers fail exact comparison.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("case")]
    [InlineData("space")]
    [InlineData("empty")]
    [InlineData("malformed")]
    public async Task InvalidIssuerIdentityIsUnauthorized(string variation)
    {
        var claims = variation == "duplicate"
            ? new Dictionary<string, object>
            {
                ["iss"] = new[]
                {
                    BearerCompanionApiFactory.Issuer,
                    BearerCompanionApiFactory.Issuer
                }
            }
            : null;
        var issuer = variation switch
        {
            "missing" or "duplicate" => null,
            "case" => "https://IDENTITY.example.test/companion/v2.0",
            "space" => $" {BearerCompanionApiFactory.Issuer}",
            "empty" => string.Empty,
            "malformed" => "not-an-absolute-issuer",
            _ => throw new InvalidOperationException()
        };
        using var response = await SendAsync(CreateToken(issuer: issuer, claims: claims));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Ensures the subject uses the provider-neutral exact bound without truncation.</summary>
    [Fact]
    public async Task OversizedSubjectIsUnauthorized()
    {
        using var response = await SendAsync(CreateToken(subject: new string('s', 256)));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Ensures missing or unsafe non-secret authority settings fail before host activation.</summary>
    [Theory]
    [InlineData(null, "api://companion-test")]
    [InlineData("http://identity.example.test/tenant/v2.0", "api://companion-test")]
    [InlineData("https://identity.example.test", "api://companion-test")]
    [InlineData("https://identity.example.test/tenant/v2.0?unsafe=true", "api://companion-test")]
    [InlineData("https://identity.example.test/tenant/v2.0", null)]
    [InlineData("https://identity.example.test/tenant/v2.0", "audience with spaces")]
    public void InvalidBearerConfigurationFailsClosed(string? issuer, string? audience)
    {
        var values = new Dictionary<string, string?>
        {
            ["Authentication:CompanionApi:Issuer"] = issuer,
            ["Authentication:CompanionApi:Audience"] = audience
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.Throws<InvalidOperationException>(() => CompanionBearerConfiguration.Parse(configuration));
    }

    /// <summary>Ensures configuration validation does not canonicalize the issuer before token comparison.</summary>
    [Fact]
    public void BearerConfigurationPreservesExactIssuerText()
    {
        const string exactIssuer = "https://IDENTITY.example.test/Companion/v2.0/";
        var values = new Dictionary<string, string?>
        {
            ["Authentication:CompanionApi:Issuer"] = exactIssuer,
            ["Authentication:CompanionApi:Audience"] = BearerCompanionApiFactory.Audience
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var bearer = CompanionBearerConfiguration.Parse(configuration);
        var options = new JwtBearerOptions();
        bearer.Configure(options);
        Assert.Equal(exactIssuer, bearer.Issuer);
        Assert.Equal(exactIssuer, options.Authority);
        Assert.Equal(exactIssuer, options.TokenValidationParameters.ValidIssuer);
    }

    /// <summary>Ensures bearer transport cannot be paired with SQL before access-context resolution exists.</summary>
    [Fact]
    public void BearerSqlCompositionFailsStartup()
    {
        using var factory = new InvalidBearerSqlCompanionApiFactory();
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("authoritative access-context", exception.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Ensures deterministic identities and projections cannot activate outside Test.</summary>
    [Theory]
    [InlineData("Development", "Closed", "Closed")]
    [InlineData("Development", "Bearer", "Sql")]
    [InlineData("Production", "Closed", "Sql")]
    [InlineData("Production", "Bearer", "Closed")]
    public void DeterministicCompositionFailsOutsideTest(
        string environment, string authenticationMode, string projectionProvider)
    {
        using var factory = new InvalidDeterministicEnvironmentCompanionApiFactory(
            environment, authenticationMode, projectionProvider);
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("only in Test", exception.ToString(), StringComparison.Ordinal);
    }

    private async Task<HttpResponseMessage> SendAsync(string token)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return await client.GetAsync("/v1/companion/adventures");
    }

    private string CreateToken(
        string? issuer = BearerCompanionApiFactory.Issuer,
        string audience = BearerCompanionApiFactory.Audience,
        string? subject = "fictional-subject",
        string? scope = "Companion.Access",
        DateTime? notBefore = null,
        DateTime? expires = null,
        SecurityKey? signingKey = null,
        string algorithm = SecurityAlgorithms.RsaSha256,
        bool unsigned = false,
        IDictionary<string, object>? claims = null)
    {
        var identity = new ClaimsIdentity();
        if (subject is not null) identity.AddClaim(new Claim("sub", subject));
        if (scope is not null) identity.AddClaim(new Claim("scope", scope));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = identity,
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = unsigned
                ? null
                : new SigningCredentials(signingKey ?? _key, algorithm),
            Claims = claims
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _factory.Dispose();
        _rsa.Dispose();
    }
}
