using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheSimontonAdventures.Web.Authorization;

namespace AdventuresSuite.Identity.ExternalId;

/// <summary>Creates and validates the minimal protected application-cookie principal.</summary>
internal static class ApplicationCookiePrincipal
{
    internal const string UserIdClaim = "adventures_suite_user_id";
    internal const string SessionIdClaim = "adventures_suite_session_id";
    internal const string SecurityVersionClaim = "adventures_suite_security_version";
    internal const string AuthenticationTimeClaim = "adventures_suite_auth_time";
    internal const string AuthenticationMethodClaim = "adventures_suite_auth_method";
    internal const string OidcAuthenticationMethod = "oidc";

    private static readonly HashSet<string> AllowedClaimTypes =
    [
        UserIdClaim,
        SessionIdClaim,
        SecurityVersionClaim,
        AuthenticationTimeClaim,
        AuthenticationMethodClaim
    ];

    public static ClaimsPrincipal Create(
        AuthenticationSessionTicket ticket,
        DateTimeOffset authenticatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (authenticatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Authentication time must be UTC.", nameof(authenticatedAtUtc));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(UserIdClaim, ticket.UserId.Value),
            new Claim(SessionIdClaim, ticket.SessionId.Value),
            new Claim(SecurityVersionClaim, ticket.SecurityVersion.Value.ToString(CultureInfo.InvariantCulture)),
            new Claim(AuthenticationTimeClaim, authenticatedAtUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim(AuthenticationMethodClaim, OidcAuthenticationMethod)
        ], ExternalIdAuthenticationExtensions.InternalCookieScheme));
    }

    public static ApplicationCookieData? Parse(
        ClaimsPrincipal? principal,
        DateTimeOffset utcNow)
    {
        if (utcNow.Offset != TimeSpan.Zero
            || principal?.Identity?.IsAuthenticated != true
            || principal.Identities.Count() != 1
            || principal.Claims.Count() != AllowedClaimTypes.Count
            || principal.Claims.Any(claim => !AllowedClaimTypes.Contains(claim.Type)))
        {
            return null;
        }

        var userId = Single(principal, UserIdClaim);
        var sessionId = Single(principal, SessionIdClaim);
        var securityVersion = Single(principal, SecurityVersionClaim);
        var authenticationTime = Single(principal, AuthenticationTimeClaim);
        var authenticationMethod = Single(principal, AuthenticationMethodClaim);
        if (userId is null
            || sessionId is null
            || securityVersion is null
            || authenticationTime is null
            || userId.Length > 64
            || sessionId.Length > 64
            || securityVersion.Length > 19
            || authenticationTime.Length > 12
            || !long.TryParse(securityVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            || version <= 0
            || !long.TryParse(authenticationTime, NumberStyles.None,
                CultureInfo.InvariantCulture, out var authenticationSeconds)
            || !string.Equals(authenticationMethod, OidcAuthenticationMethod, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var authenticatedAtUtc = DateTimeOffset.FromUnixTimeSeconds(authenticationSeconds);
            if (authenticatedAtUtc > utcNow)
            {
                return null;
            }

            return new ApplicationCookieData(
                new AuthenticationSessionTicket(
                    new UserSessionId(sessionId),
                    new UserId(userId),
                    new SecurityVersion(version)),
                authenticatedAtUtc,
                OidcAuthenticationMethod);
        }
        catch
        {
            return null;
        }
    }

    public static void ApplyLifetime(
        AuthenticationProperties properties,
        DateTimeOffset authenticatedAtUtc,
        TimeSpan absoluteLifetime)
    {
        ArgumentNullException.ThrowIfNull(properties);
        if (authenticatedAtUtc.Offset != TimeSpan.Zero || absoluteLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentException("A UTC authentication time and positive lifetime are required.");
        }

        properties.IssuedUtc = authenticatedAtUtc;
        properties.ExpiresUtc = authenticatedAtUtc + absoluteLifetime;
        properties.AllowRefresh = false;
    }

    private static string? Single(ClaimsPrincipal principal, string type)
    {
        var claims = principal.Claims
            .Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))
            .ToArray();
        return claims.Length == 1 ? claims[0].Value : null;
    }
}

internal sealed record ApplicationCookieData(
    AuthenticationSessionTicket Ticket,
    DateTimeOffset AuthenticatedAtUtc,
    string AuthenticationMethod);

/// <summary>Rejects private authentication operations outside the canonical workspace.</summary>
internal sealed class RejectedWorkspaceAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}

internal static class ApplicationCookieConfiguration
{
    public static void Configure(
        CookieAuthenticationOptions options,
        AuthenticationConfiguration configuration)
    {
        options.Cookie.Name = "__Host-AdventuresSuite.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.Cookie.Domain = null;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = configuration.AbsoluteSessionLifetime;
        options.SlidingExpiration = false;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                if (!ExternalIdAuthenticationExtensions.IsWorkspaceRequest(
                        context.Request,
                        configuration))
                {
                    context.RejectPrincipal();
                    return;
                }

                var clock = context.HttpContext.RequestServices
                    .GetRequiredService<IAuthenticationClock>();
                var cookie = ApplicationCookiePrincipal.Parse(
                    context.Principal,
                    clock.GetUtcNow());
                if (cookie is null)
                {
                    context.RejectPrincipal();
                    return;
                }

                var authenticator = context.HttpContext.RequestServices
                    .GetRequiredService<IServerSessionAuthenticator>();
                var result = await authenticator.AuthenticateAsync(
                    configuration.WorkspaceOrigin!,
                    cookie.Ticket,
                    context.HttpContext.RequestAborted);
                if (result.Outcome != SessionAuthenticationOutcome.Authenticated)
                {
                    context.RejectPrincipal();
                    return;
                }

                context.ShouldRenew = false;
            },
            OnRedirectToLogin = context => RespondAsync(context.Response, StatusCodes.Status401Unauthorized),
            OnRedirectToAccessDenied = context => RespondAsync(context.Response, StatusCodes.Status403Forbidden)
        };
    }

    private static Task RespondAsync(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}

/// <summary>Prevents later configuration sources from weakening cookie invariants.</summary>
internal sealed class ApplicationCookieOptionsValidator
    : IValidateOptions<CookieAuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, CookieAuthenticationOptions options)
    {
        if (!string.Equals(
                name,
                ExternalIdAuthenticationExtensions.InternalCookieScheme,
                StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Skip;
        }

        return options.Cookie.Name == "__Host-AdventuresSuite.Session"
            && options.Cookie.HttpOnly
            && options.Cookie.SecurePolicy == CookieSecurePolicy.Always
            && options.Cookie.SameSite == SameSiteMode.Lax
            && options.Cookie.Path == "/"
            && string.IsNullOrEmpty(options.Cookie.Domain)
            && !options.SlidingExpiration
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Application cookie security requirements cannot be weakened.");
    }
}
