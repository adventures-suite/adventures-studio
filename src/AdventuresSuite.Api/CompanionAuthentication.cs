using System.Security.Claims;
using System.Text.Encodings.Web;
using AdventuresSuite.Companion.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AdventuresSuite.Api;

/// <summary>Validates and applies the closed Companion bearer-transport configuration.</summary>
public sealed class CompanionBearerConfiguration
{
    private CompanionBearerConfiguration(string issuer, string audience)
    {
        Issuer = issuer;
        Audience = audience;
    }

    /// <summary>Gets the exact HTTPS token issuer.</summary>
    public string Issuer { get; }

    /// <summary>Gets the exact API audience.</summary>
    public string Audience { get; }

    /// <summary>Reads exact non-secret bearer settings and fails closed on unsafe values.</summary>
    public static CompanionBearerConfiguration Parse(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var issuerValue = configuration[CompanionApiConstants.AuthenticationIssuerKey];
        var audience = configuration[CompanionApiConstants.AuthenticationAudienceKey];
        if (string.IsNullOrWhiteSpace(issuerValue)
            || issuerValue.Length > 2048
            || issuerValue != issuerValue.Trim()
            || !Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer)
            || issuer.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(issuer.UserInfo)
            || !string.IsNullOrEmpty(issuer.Query)
            || !string.IsNullOrEmpty(issuer.Fragment)
            || issuer.AbsolutePath.Length <= 1
            || string.IsNullOrWhiteSpace(audience)
            || audience.Length > 256
            || audience.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException("Companion bearer issuer and audience configuration is invalid.");
        }

        return new CompanionBearerConfiguration(issuerValue, audience);
    }

    /// <summary>Applies strict protocol validation without retaining token material.</summary>
    public void Configure(JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Authority = Issuer;
        options.Audience = Audience;
        options.MapInboundClaims = false;
        options.SaveToken = false;
        options.IncludeErrorDetails = false;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            NameClaimType = "sub",
            RoleClaimType = "__companion_roles_not_authoritative"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (!TryValidateExternalIdentity(context.Principal))
                {
                    context.Fail("The bearer identity is invalid.");
                }

                return Task.CompletedTask;
            }
        };
    }

    /// <summary>Checks the exact delegated scope without treating other claims as authority.</summary>
    public static bool HasRequiredScope(ClaimsPrincipal principal) =>
        principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(DeterministicCompanionProjectionService.RequiredScope, StringComparer.Ordinal);

    private bool TryValidateExternalIdentity(ClaimsPrincipal? principal)
    {
        var issuers = principal?.FindAll("iss").Select(claim => claim.Value).ToArray() ?? [];
        var subjects = principal?.FindAll("sub").Select(claim => claim.Value).ToArray() ?? [];
        if (issuers.Length != 1 || subjects.Length != 1)
            return false;

        try
        {
            var issuer = new AdventuresSuite.Identity.ExternalIdentityIssuer(issuers[0]);
            _ = new AdventuresSuite.Identity.ExternalIdentitySubject(subjects[0]);
            return string.Equals(issuer.Value, Issuer, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>Represents the permanently closed authentication scheme used before production OAuth activation.</summary>
public sealed class ClosedCompanionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Gets the closed scheme name.</summary>
    public const string SchemeName = "CompanionClosed";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}

/// <summary>Authenticates only deterministic fictional identities in the Test environment.</summary>
public sealed class TestCompanionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IWebHostEnvironment environment) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Gets the test-only scheme name.</summary>
    public const string SchemeName = "CompanionDeterministicTest";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsEnvironment("Test"))
            return Task.FromResult(AuthenticateResult.Fail("Deterministic authentication is unavailable."));
        if (Request.Headers["X-Companion-Test-Anonymous"] == "true")
            return Task.FromResult(AuthenticateResult.NoResult());

        var user = HeaderOrDefault("X-Companion-Test-User", DeterministicCompanionProjectionService.DemoUserId);
        var traveler = HeaderOrDefault("X-Companion-Test-Traveler", DeterministicCompanionProjectionService.DemoTravelerId);
        var creator = HeaderOrDefault("X-Companion-Test-Creator", DeterministicCompanionProjectionService.DemoCreatorId);
        var scope = HeaderOrDefault("X-Companion-Test-Scope", DeterministicCompanionProjectionService.RequiredScope);
        var revoked = HeaderOrDefault("X-Companion-Test-Revoked", "false");
        var membershipVersion = HeaderOrDefault(
            "X-Companion-Test-Membership-Version",
            DeterministicCompanionAuthorizationFacts.MembershipVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user),
            new Claim("traveler_id", traveler),
            new Claim("creator_id", creator),
            new Claim("scope", scope),
            new Claim("revoked", revoked),
            new Claim("membership_version", membershipVersion)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    private string HeaderOrDefault(string name, string defaultValue) =>
        Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : defaultValue;
}

/// <summary>Provides fixed test time without depending on device or network clocks.</summary>
public sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => value;
}
