using System.Security.Cryptography;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Adds the reviewed browser security policy to every application response.</summary>
public sealed class BrowserSecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicyPrefix =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "connect-src 'self'; " +
        "font-src 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; " +
        "img-src 'self' data:; " +
        "object-src 'none'; " +
        "script-src 'self' 'nonce-";
    private const string ContentSecurityPolicySuffix =
        "'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "upgrade-insecure-requests";
    private static readonly object NonceKey = new();

    private readonly RequestDelegate next =
        next ?? throw new ArgumentNullException(nameof(next));

    /// <summary>Adds non-overridable headers before the response begins.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var nonce = RandomNumberGenerator.GetHexString(32, lowercase: true);
        context.Items[NonceKey] = nonce;
        Apply(context.Response.Headers, nonce);
        context.Response.OnStarting(static state =>
        {
            var responseState = (ResponseState)state;
            Apply(responseState.Response.Headers, responseState.Nonce);
            return Task.CompletedTask;
        }, new ResponseState(context.Response, nonce));

        await next(context);
        if (!context.Response.HasStarted)
        {
            Apply(context.Response.Headers, nonce);
        }
    }

    /// <summary>Gets the response-specific CSP nonce for server-rendered inline framework data.</summary>
    public static string GetNonce(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue(NonceKey, out var value) && value is string nonce
            ? nonce
            : throw new InvalidOperationException(
                "The browser security middleware has not established a CSP nonce.");
    }

    private static void Apply(IHeaderDictionary headers, string nonce)
    {
        headers.ContentSecurityPolicy =
            ContentSecurityPolicyPrefix + nonce + ContentSecurityPolicySuffix;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] =
            "camera=(), geolocation=(), microphone=(), payment=(), usb=()";
    }

    private sealed record ResponseState(HttpResponse Response, string Nonce);
}
