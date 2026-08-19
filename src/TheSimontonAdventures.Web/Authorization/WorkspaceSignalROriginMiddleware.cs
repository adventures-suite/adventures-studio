namespace TheSimontonAdventures.Web.Authorization;

/// <summary>
/// Rejects workspace SignalR transports that do not carry the exact canonical Origin.
/// </summary>
public sealed class WorkspaceSignalROriginMiddleware(
    RequestDelegate next,
    AuthenticationConfiguration configuration)
{
    private readonly RequestDelegate next =
        next ?? throw new ArgumentNullException(nameof(next));
    private readonly AuthenticationConfiguration configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));

    /// <summary>Validates negotiate and every subsequent Blazor transport request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (RequiresWorkspaceOrigin(context.Request)
            && !HasExactWorkspaceOrigin(context.Request.Headers.Origin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }

    private bool RequiresWorkspaceOrigin(HttpRequest request)
    {
        if (configuration.Mode == AuthenticationMode.Disabled
            || !IsInteractiveTransport(request.Path))
        {
            return false;
        }

        return IsWorkspaceHost(request.Host)
            || request.Cookies.ContainsKey(BrowserAuthenticationDefaults.ApplicationCookieName);
    }

    private static bool IsInteractiveTransport(PathString path) =>
        path == "/_blazor"
        || path == "/_blazor/negotiate";

    private bool HasExactWorkspaceOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)
            || !Uri.TryCreate(origin, UriKind.Absolute, out var requestOrigin)
            || !Uri.TryCreate(configuration.WorkspaceOrigin, UriKind.Absolute, out var workspace)
            || requestOrigin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(requestOrigin.Query)
            || !string.IsNullOrEmpty(requestOrigin.Fragment)
            || !string.IsNullOrEmpty(requestOrigin.UserInfo))
        {
            return false;
        }

        return string.Equals(requestOrigin.Scheme, workspace.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(requestOrigin.IdnHost, workspace.IdnHost, StringComparison.OrdinalIgnoreCase)
            && requestOrigin.Port == workspace.Port;
    }

    private bool IsWorkspaceHost(HostString host)
    {
        var workspace = new Uri(configuration.WorkspaceOrigin!, UriKind.Absolute);
        return string.Equals(host.Host, workspace.IdnHost, StringComparison.OrdinalIgnoreCase)
            && (host.Port ?? DefaultPort(workspace.Scheme)) == workspace.Port;
    }

    private static int DefaultPort(string scheme) =>
        string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80;
}
