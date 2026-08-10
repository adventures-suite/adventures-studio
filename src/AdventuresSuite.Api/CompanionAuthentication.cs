using System.Security.Claims;
using System.Text.Encodings.Web;
using AdventuresSuite.Companion.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AdventuresSuite.Api;

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
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user),
            new Claim("traveler_id", traveler),
            new Claim("creator_id", creator),
            new Claim("scope", scope),
            new Claim("revoked", revoked)
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
