namespace AdventuresSuite.Identity;

/// <summary>Classifies the selected provider-neutral authentication mode.</summary>
public enum AuthenticationMode
{
    /// <summary>Private authentication is explicitly disabled.</summary>
    Disabled,
    /// <summary>A configured external provider establishes human identity.</summary>
    ExternalProvider,
    /// <summary>A deterministic adapter is selected for isolated development.</summary>
    Development
}

/// <summary>
/// Defines fail-fast authentication settings without framework or provider types.
/// </summary>
public sealed record AuthenticationConfiguration
{
    /// <summary>Initializes and validates authentication configuration.</summary>
    public AuthenticationConfiguration(
        AuthenticationMode mode,
        string? workspaceOrigin,
        ExternalIdentityProviderId providerId,
        string? authority,
        string? clientId,
        string? clientCertificateReference,
        string? callbackPath,
        string? signedOutCallbackPath,
        TimeSpan absoluteSessionLifetime,
        TimeSpan idleSessionTimeout,
        TimeSpan activityTouchInterval,
        TimeSpan circuitRevalidationInterval)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (mode == AuthenticationMode.Disabled)
        {
            if (workspaceOrigin is not null
                || providerId != default
                || authority is not null
                || clientId is not null
                || clientCertificateReference is not null
                || callbackPath is not null
                || signedOutCallbackPath is not null)
            {
                throw new ArgumentException(
                    "Disabled authentication cannot retain active provider configuration.");
            }
        }
        else if (mode == AuthenticationMode.ExternalProvider)
        {
            WorkspaceOrigin = RequireOrigin(workspaceOrigin, nameof(workspaceOrigin));
            if (providerId == default)
            {
                throw new ArgumentException("An authentication provider is required.", nameof(providerId));
            }

            ProviderId = providerId;
            Authority = RequireHttpsUri(authority, nameof(authority));
            ClientId = RequireOpaqueValue(clientId, nameof(clientId));
            CallbackPath = RequireLocalPath(callbackPath, nameof(callbackPath));
            SignedOutCallbackPath = RequireLocalPath(
                signedOutCallbackPath,
                nameof(signedOutCallbackPath));

            if (CallbackPath == SignedOutCallbackPath)
            {
                throw new ArgumentException("Callback paths must be distinct.");
            }

            ClientCertificateReference = RequireOpaqueValue(
                clientCertificateReference,
                nameof(clientCertificateReference));
        }
        else
        {
            WorkspaceOrigin = RequireOrigin(workspaceOrigin, nameof(workspaceOrigin));
            if (providerId == default)
            {
                throw new ArgumentException("An authentication provider is required.", nameof(providerId));
            }

            if (authority is not null
                || clientId is not null
                || clientCertificateReference is not null
                || callbackPath is not null
                || signedOutCallbackPath is not null)
            {
                throw new ArgumentException(
                    "Development authentication cannot carry external-provider protocol configuration.");
            }

            ProviderId = providerId;
        }

        ValidateDurations(
            absoluteSessionLifetime,
            idleSessionTimeout,
            activityTouchInterval,
            circuitRevalidationInterval);

        Mode = mode;
        AbsoluteSessionLifetime = absoluteSessionLifetime;
        IdleSessionTimeout = idleSessionTimeout;
        ActivityTouchInterval = activityTouchInterval;
        CircuitRevalidationInterval = circuitRevalidationInterval;
    }

    /// <summary>Gets the selected authentication mode.</summary>
    public AuthenticationMode Mode { get; }

    /// <summary>Gets the exact canonical private workspace origin.</summary>
    public string? WorkspaceOrigin { get; }

    /// <summary>Gets the provider-neutral adapter identity.</summary>
    public ExternalIdentityProviderId ProviderId { get; }

    /// <summary>Gets the exact external authority URI.</summary>
    public string? Authority { get; }

    /// <summary>Gets the confidential application client identity.</summary>
    public string? ClientId { get; }

    /// <summary>Gets the opaque certificate reference, never certificate material.</summary>
    public string? ClientCertificateReference { get; }

    /// <summary>Gets the local provider callback path.</summary>
    public string? CallbackPath { get; }

    /// <summary>Gets the local signed-out callback path.</summary>
    public string? SignedOutCallbackPath { get; }

    /// <summary>Gets the non-extendable maximum session lifetime.</summary>
    public TimeSpan AbsoluteSessionLifetime { get; }

    /// <summary>Gets the maximum session inactivity interval.</summary>
    public TimeSpan IdleSessionTimeout { get; }

    /// <summary>Gets the minimum interval between persisted activity touches.</summary>
    public TimeSpan ActivityTouchInterval { get; }

    /// <summary>Gets the interval for interactive circuit session revalidation.</summary>
    public TimeSpan CircuitRevalidationInterval { get; }

    /// <summary>Creates explicit public-only configuration with no provider state.</summary>
    public static AuthenticationConfiguration Disabled() => new(
        AuthenticationMode.Disabled,
        null,
        default,
        null,
        null,
        null,
        null,
        null,
        TimeSpan.FromHours(8),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5));

    private static string RequireOrigin(string? value, string parameterName)
    {
        var origin = RequireHttpsUri(value, parameterName);
        var parsed = new Uri(origin, UriKind.Absolute);
        if (parsed.AbsolutePath != "/"
            || !string.IsNullOrEmpty(parsed.Query)
            || !string.IsNullOrEmpty(parsed.Fragment)
            || !string.IsNullOrEmpty(parsed.UserInfo))
        {
            throw new ArgumentException(
                "The workspace origin must contain only an exact HTTPS scheme and authority.",
                parameterName);
        }

        if (origin.EndsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The workspace origin must not contain a trailing slash.",
                parameterName);
        }

        return origin;
    }

    private static string RequireHttpsUri(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value != value.Trim()
            || !Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(parsed.UserInfo)
            || !string.IsNullOrEmpty(parsed.Query)
            || !string.IsNullOrEmpty(parsed.Fragment))
        {
            throw new ArgumentException("A validated exact HTTPS URI is required.", parameterName);
        }

        return value;
    }

    private static string RequireOpaqueValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 256
            || value != value.Trim()
            || value.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded opaque configuration value is required.", parameterName);
        }

        return value;
    }

    private static string RequireLocalPath(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 256
            || value[0] != '/'
            || value.StartsWith("//", StringComparison.Ordinal)
            || value.Contains('?')
            || value.Contains('#')
            || value.Contains('\\')
            || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "Authentication callback paths must be bounded local absolute paths.",
                parameterName);
        }

        return value;
    }

    private static void ValidateDurations(
        TimeSpan absoluteSessionLifetime,
        TimeSpan idleSessionTimeout,
        TimeSpan activityTouchInterval,
        TimeSpan circuitRevalidationInterval)
    {
        if (absoluteSessionLifetime <= TimeSpan.Zero
            || absoluteSessionLifetime > TimeSpan.FromHours(24)
            || idleSessionTimeout <= TimeSpan.Zero
            || idleSessionTimeout >= absoluteSessionLifetime
            || activityTouchInterval <= TimeSpan.Zero
            || activityTouchInterval > TimeSpan.FromMinutes(5)
            || activityTouchInterval >= idleSessionTimeout
            || circuitRevalidationInterval <= TimeSpan.Zero
            || circuitRevalidationInterval >= idleSessionTimeout)
        {
            throw new ArgumentException(
                "Authentication lifetimes must be positive, bounded, and preserve idle and absolute expiration.");
        }
    }
}
