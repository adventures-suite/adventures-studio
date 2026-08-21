using Microsoft.Extensions.Configuration;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Provides validated external destinations shown by the private workspace navigation.</summary>
public sealed class WorkspaceNavigationConfiguration
{
    /// <summary>Identifies the configuration section for workspace navigation destinations.</summary>
    public const string SectionName = "WorkspaceNavigation";

    /// <summary>Gets the optional public site for The Simonton Adventures.</summary>
    public Uri? SimontonAdventuresUrl { get; init; }

    /// <summary>Creates workspace navigation configuration from application settings.</summary>
    /// <param name="configuration">The application configuration source.</param>
    /// <returns>Validated workspace navigation configuration.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a configured destination is not an absolute HTTPS URL without embedded credentials.
    /// </exception>
    public static WorkspaceNavigationConfiguration FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredUrl = configuration[$"{SectionName}:SimontonAdventuresUrl"];
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            return new WorkspaceNavigationConfiguration();
        }

        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var parsedUrl)
            || !string.Equals(parsedUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(parsedUrl.UserInfo))
        {
            throw new InvalidOperationException(
                "WorkspaceNavigation:SimontonAdventuresUrl must be an absolute HTTPS URL without embedded credentials.");
        }

        return new WorkspaceNavigationConfiguration
        {
            SimontonAdventuresUrl = parsedUrl
        };
    }
}
