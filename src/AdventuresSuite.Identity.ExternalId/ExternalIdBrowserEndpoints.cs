using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Authorization.Persistence;

namespace AdventuresSuite.Identity.ExternalId;

/// <summary>Maps the narrowly scoped browser endpoints for External ID authentication.</summary>
public static class ExternalIdBrowserEndpoints
{
    /// <summary>The local endpoint that begins an External ID sign-in.</summary>
    public const string SignInPath = "/authentication/sign-in";

    /// <summary>The local POST endpoint that revokes the current application session.</summary>
    public const string SignOutPath = "/authentication/sign-out";

    /// <summary>The generic page used for authentication failures.</summary>
    public const string FailurePath = "/authentication/failure";

    /// <summary>The generic page used for authorization denials.</summary>
    public const string AccessDeniedPath = "/authentication/access-denied";

    /// <summary>Registers workspace-only browser authentication endpoints.</summary>
    public static IEndpointRouteBuilder MapAdventuresSuiteExternalIdEndpoints(
        this IEndpointRouteBuilder endpoints,
        AuthenticationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Mode != AuthenticationMode.ExternalProvider)
        {
            throw new InvalidOperationException(
                "External ID browser endpoints require external-provider mode.");
        }

        endpoints.MapGet(
            SignInPath,
            (HttpContext context, string? returnUrl) =>
                BeginSignInAsync(context, configuration, returnUrl));
        endpoints.MapPost(
                SignOutPath,
                (HttpContext context,
                    [FromServices] IAuthenticationClock clock,
                    [FromServices] IAuthenticationPersistenceTransactionFactory transactionFactory,
                    string? returnUrl) => CompleteSignOutAsync(
                        context,
                        configuration,
                        clock,
                        transactionFactory,
                        returnUrl))
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));
        endpoints.MapGet(
            FailurePath,
            (HttpContext context) => GenericFailure(context, configuration));
        endpoints.MapGet(
            AccessDeniedPath,
            (HttpContext context) => GenericAccessDenied(context, configuration));
        return endpoints;
    }

    internal static Task<IResult> BeginSignInAsync(
        HttpContext context,
        AuthenticationConfiguration configuration,
        string? returnUrl)
    {
        if (!ExternalIdAuthenticationExtensions.IsWorkspaceRequest(context.Request, configuration))
        {
            return Task.FromResult(Results.NotFound());
        }

        var target = WorkspaceReturnTarget.ValidateOrDefault(returnUrl);
        return Task.FromResult(Results.Challenge(
            new AuthenticationProperties { RedirectUri = target },
            [ExternalIdAuthenticationExtensions.Scheme]));
    }

    internal static async Task<IResult> CompleteSignOutAsync(
        HttpContext context,
        AuthenticationConfiguration configuration,
        IAuthenticationClock clock,
        IAuthenticationPersistenceTransactionFactory transactionFactory,
        string? returnUrl)
    {
        if (!ExternalIdAuthenticationExtensions.IsWorkspaceRequest(context.Request, configuration))
        {
            return Results.NotFound();
        }

        var cookie = ApplicationCookiePrincipal.Parse(context.User, clock.GetUtcNow());
        if (cookie is null)
        {
            // An absent or rejected cookie is already signed out. Avoid challenging
            // and redirecting back into authentication, which can create a loop.
            await context.SignOutAsync(ExternalIdAuthenticationExtensions.InternalCookieScheme);
            return Results.LocalRedirect(WorkspaceReturnTarget.ValidateOrDefault(returnUrl));
        }

        try
        {
            await using var transaction = await transactionFactory.BeginAsync(context.RequestAborted);
            var revoked = await transaction.Sessions.RevokeAsync(
                cookie.Ticket.SessionId,
                clock.GetUtcNow(),
                SessionRevocationReason.SignedOut,
                context.RequestAborted);
            if (!revoked)
            {
                return Results.LocalRedirect(FailurePath);
            }

            await transaction.CommitAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Results.LocalRedirect(FailurePath);
        }

        // The authoritative revocation commits before the browser credential is
        // deleted. A failed revocation therefore cannot leave a usable server
        // session hidden behind a locally deleted cookie.
        await context.SignOutAsync(ExternalIdAuthenticationExtensions.InternalCookieScheme);
        return Results.LocalRedirect(WorkspaceReturnTarget.ValidateOrDefault(returnUrl));
    }

    private static IResult GenericFailure(HttpContext context, AuthenticationConfiguration configuration) =>
        ExternalIdAuthenticationExtensions.IsWorkspaceRequest(context.Request, configuration)
            ? Results.Text("Authentication could not be completed.", statusCode: StatusCodes.Status401Unauthorized)
            : Results.NotFound();

    private static IResult GenericAccessDenied(HttpContext context, AuthenticationConfiguration configuration) =>
        ExternalIdAuthenticationExtensions.IsWorkspaceRequest(context.Request, configuration)
            ? Results.Text("Access was denied.", statusCode: StatusCodes.Status403Forbidden)
            : Results.NotFound();
}

/// <summary>Validates bounded local workspace navigation targets.</summary>
internal static class WorkspaceReturnTarget
{
    private const int MaximumLength = 512;
    private static readonly string[] ReservedPaths =
    [
        ExternalIdBrowserEndpoints.SignInPath,
        ExternalIdBrowserEndpoints.SignOutPath,
        ExternalIdBrowserEndpoints.FailurePath,
        ExternalIdBrowserEndpoints.AccessDeniedPath,
        "/signin-oidc",
        "/signout-callback-oidc"
    ];

    public static string ValidateOrDefault(string? value) => IsValid(value) ? value! : "/";

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || value.Length > MaximumLength
            || value[0] != '/'
            || value.StartsWith("//", StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('%', StringComparison.Ordinal)
            || value.Contains('#', StringComparison.Ordinal)
            || value.Any(char.IsControl))
        {
            return false;
        }

        var candidate = value;
        for (var pass = 0; pass < 2; pass++)
        {
            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(candidate);
            }
            catch
            {
                return false;
            }

            if (decoded.Contains('\\', StringComparison.Ordinal)
                || decoded.StartsWith("//", StringComparison.Ordinal)
                || decoded.Any(char.IsControl)
                || decoded.Contains("../", StringComparison.Ordinal)
                || decoded.Contains("/..", StringComparison.Ordinal))
            {
                return false;
            }

            candidate = decoded;
        }

        if (!candidate.StartsWith("/", StringComparison.Ordinal)
            || candidate.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var path = candidate.Split('?', 2)[0];
        var firstSegment = path.TrimStart('/').Split('/', 2)[0];
        if (firstSegment.Contains('.', StringComparison.Ordinal)
            || firstSegment.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        return !ReservedPaths.Any(reserved =>
            path.Equals(reserved, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(reserved + "/", StringComparison.OrdinalIgnoreCase));
    }
}
