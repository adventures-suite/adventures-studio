using Microsoft.AspNetCore.Antiforgery;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>
/// Applies antiforgery validation by default to cookie-authenticated unsafe HTTP requests.
/// </summary>
public sealed class CookieAuthenticatedAntiforgeryMiddleware(
    RequestDelegate next,
    AuthenticationConfiguration configuration)
{
    private readonly RequestDelegate next =
        next ?? throw new ArgumentNullException(nameof(next));
    private readonly AuthenticationConfiguration configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));

    /// <summary>Validates an unsafe authenticated request before invoking its endpoint.</summary>
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(antiforgery);

        if (IsUnsafe(context.Request.Method)
            && context.User.Identity?.IsAuthenticated == true
            && context.Request.Cookies.ContainsKey(
                BrowserAuthenticationDefaults.ApplicationCookieName)
            && !context.Request.Path.StartsWithSegments("/_blazor")
            && !IsProtocolEndpoint(context.Request.Path)
            && context.GetEndpoint()?.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation
                != false)
        {
            await antiforgery.ValidateRequestAsync(context);
        }

        await next(context);
    }

    private static bool IsUnsafe(string method) =>
        HttpMethods.IsPost(method)
        || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method)
        || HttpMethods.IsDelete(method);

    private bool IsProtocolEndpoint(PathString path) =>
        configuration.Mode == AuthenticationMode.ExternalProvider
        && (path.Equals(configuration.CallbackPath)
            || path.Equals(configuration.SignedOutCallbackPath));
}
