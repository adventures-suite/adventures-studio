using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdventuresSuite.Identity.ExternalId;

/// <summary>Composes fixed-identity development authentication over the normal application session.</summary>
public static class DevelopmentAuthenticationExtensions
{
    /// <summary>Adds the hardened application cookie without an external protocol handler.</summary>
    public static AuthenticationBuilder AddAdventuresSuiteDevelopmentAuthentication(
        this AuthenticationBuilder builder,
        AuthenticationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Mode != AuthenticationMode.Development)
        {
            throw new InvalidOperationException("Development authentication requires development mode.");
        }

        builder.Services.AddScoped<IServerSessionAuthenticator, ServerSessionAuthenticator>();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<AuthenticationStateProvider,
            AdventuresSuiteCircuitAuthenticationStateProvider>();
        builder.Services.AddSingleton<IValidateOptions<CookieAuthenticationOptions>,
            ApplicationCookieOptionsValidator>();
        builder.AddCookie(ExternalIdAuthenticationExtensions.InternalCookieScheme,
            options => ApplicationCookieConfiguration.Configure(options, configuration));
        builder.AddScheme<AuthenticationSchemeOptions, RejectedWorkspaceAuthenticationHandler>(
            ExternalIdAuthenticationExtensions.RejectedWorkspaceScheme, _ => { });
        builder.AddPolicyScheme(ExternalIdAuthenticationExtensions.SessionScheme, null, options =>
        {
            options.ForwardDefaultSelector = context =>
                ExternalIdAuthenticationExtensions.IsWorkspaceRequest(context.Request, configuration)
                    ? ExternalIdAuthenticationExtensions.InternalCookieScheme
                    : ExternalIdAuthenticationExtensions.RejectedWorkspaceScheme;
        });
        return builder;
    }

    /// <summary>Maps development-only sign-in and the shared authoritative sign-out flow.</summary>
    public static IEndpointRouteBuilder MapAdventuresSuiteDevelopmentAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints,
        AuthenticationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Mode != AuthenticationMode.Development)
        {
            throw new InvalidOperationException("Development endpoints require development mode.");
        }

        endpoints.MapPost(ExternalIdBrowserEndpoints.SignInPath, SignInAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));
        endpoints.MapPost(ExternalIdBrowserEndpoints.SignOutPath,
                (HttpContext context, IAuthenticationClock clock,
                    Identity.Persistence.IAuthenticationPersistenceTransactionFactory factory,
                    string? returnUrl) => ExternalIdBrowserEndpoints.CompleteSignOutAsync(
                        context, configuration, clock, factory, returnUrl))
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));
        return endpoints;

        async Task<IResult> SignInAsync(
            HttpContext context,
            DevelopmentAuthenticationAdapter adapter,
            IAuthenticationClock clock,
            string? returnUrl)
        {
            if (!ExternalIdAuthenticationExtensions.IsWorkspaceRequest(context.Request, configuration))
            {
                return Results.NotFound();
            }

            var authenticatedAtUtc = clock.GetUtcNow();
            var ticket = await adapter.EstablishSessionAsync(context.RequestAborted);
            var principal = ApplicationCookiePrincipal.Create(
                ticket, authenticatedAtUtc, ApplicationCookiePrincipal.DevelopmentAuthenticationMethod);
            var properties = new AuthenticationProperties();
            ApplicationCookiePrincipal.ApplyLifetime(
                properties, authenticatedAtUtc, configuration.AbsoluteSessionLifetime);
            await context.SignInAsync(
                ExternalIdAuthenticationExtensions.InternalCookieScheme, principal, properties);
            return Results.LocalRedirect(WorkspaceReturnTarget.ValidateOrDefault(returnUrl));
        }
    }
}
