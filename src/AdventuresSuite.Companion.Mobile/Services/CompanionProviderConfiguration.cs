using AdventuresSuite.Companion.Mobile.Models;

namespace AdventuresSuite.Companion.Mobile.Services;

/// <summary>Resolves explicit local or packaged non-secret provider configuration.</summary>
public static class CompanionProviderConfiguration
{
    /// <summary>Resolves provider selection without supplying a fallback mode.</summary>
    /// <param name="localProvider">The optional local-development provider override.</param>
    /// <param name="localApiBaseAddress">The optional local-development API origin override.</param>
    /// <param name="packagedProvider">The provider embedded through an MSBuild package property.</param>
    /// <param name="packagedApiBaseAddress">The API origin embedded through an MSBuild package property.</param>
    /// <returns>The validated explicit provider configuration.</returns>
    public static CompanionProviderSettings Resolve(
        string? localProvider,
        string? localApiBaseAddress,
        string? packagedProvider,
        string? packagedApiBaseAddress)
    {
        var providerValue = FirstConfigured(localProvider, packagedProvider);
        if (!Enum.TryParse<CompanionContentProviderKind>(providerValue, ignoreCase: true, out var provider))
        {
            throw new InvalidOperationException(
                "Companion content provider must explicitly select Demo or Api.");
        }

        if (provider == CompanionContentProviderKind.Demo)
        {
            return new(provider, null);
        }

        var apiBaseAddressValue = FirstConfigured(localApiBaseAddress, packagedApiBaseAddress);
        if (!Uri.TryCreate(apiBaseAddressValue, UriKind.Absolute, out var apiBaseAddress) ||
            apiBaseAddress.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(apiBaseAddress.UserInfo))
        {
            throw new InvalidOperationException(
                "Companion API base address must be an absolute HTTPS URI without credentials when Api is selected.");
        }

        return new(provider, apiBaseAddress);
    }

    private static string? FirstConfigured(string? localValue, string? packagedValue) =>
        !string.IsNullOrWhiteSpace(localValue) ? localValue : packagedValue;
}

/// <summary>Contains validated non-secret provider configuration.</summary>
/// <param name="Provider">The explicit provider.</param>
/// <param name="ApiBaseAddress">The API origin required only for API mode.</param>
public sealed record CompanionProviderSettings(
    CompanionContentProviderKind Provider,
    Uri? ApiBaseAddress);
