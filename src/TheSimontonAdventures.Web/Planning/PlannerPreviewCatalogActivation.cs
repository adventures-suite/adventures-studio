using AdventuresSuite.Identity.ExternalId;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>
/// Determines whether environment-isolated preview catalogs may be registered
/// without coupling catalog availability to the authentication provider.
/// </summary>
public static class PlannerPreviewCatalogActivation
{
    /// <summary>The exact non-production catalog mode accepted by the application.</summary>
    public const string DevelopmentPreviewMode = "DevelopmentPreview";

    /// <summary>The exact deployment classification required for a hosted preview catalog.</summary>
    public const string DevelopmentDeploymentEnvironment = "Development";

    /// <summary>
    /// Returns whether the fictional, reviewed preview catalogs may be used for
    /// the current configuration.
    /// </summary>
    /// <param name="configuration">The trusted application configuration.</param>
    /// <param name="authenticationMode">The configured authentication mode.</param>
    /// <returns>
    /// <see langword="true"/> for local Development authentication or for an
    /// explicitly classified development deployment using the exact preview mode;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsEnabled(IConfiguration configuration, string? authenticationMode)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.Equals(
            authenticationMode,
            nameof(AuthenticationMode.Development),
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
                authenticationMode,
                nameof(AuthenticationMode.ExternalProvider),
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                configuration["PlannerCatalog:Mode"],
                DevelopmentPreviewMode,
                StringComparison.Ordinal)
            && string.Equals(
                configuration["Deployment:Environment"],
                DevelopmentDeploymentEnvironment,
                StringComparison.Ordinal);
    }
}
