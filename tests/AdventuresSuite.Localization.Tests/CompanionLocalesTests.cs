using AdventuresSuite.Localization;
using Xunit;

namespace AdventuresSuite.Localization.Tests;

/// <summary>
/// Verifies supported Companion locale resolution and resource lookup.
/// </summary>
public sealed class CompanionLocalesTests
{
    /// <summary>
    /// Verifies regional preferences resolve to their supported locale.
    /// </summary>
    /// <param name="requested">The requested language tag.</param>
    /// <param name="expected">The expected supported language tag.</param>
    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("en-GB", "en-US")]
    [InlineData("es-MX", "es")]
    [InlineData("fr-CA", "fr")]
    [InlineData("it-IT", "it")]
    public void Resolve_MapsSupportedRegionalPreferences(string requested, string expected)
    {
        Assert.Equal(expected, CompanionLocales.Resolve([requested]).LanguageTag);
    }

    /// <summary>
    /// Verifies unsupported preferences do not prevent a later supported preference.
    /// </summary>
    [Fact]
    public void Resolve_UsesNextPreferenceBeforeDefault()
    {
        Assert.Equal("fr", CompanionLocales.Resolve(["de-DE", "fr-FR"]).LanguageTag);
    }

    /// <summary>
    /// Verifies missing, malformed, or unsupported preferences fail safely to English.
    /// </summary>
    /// <param name="requested">The requested language tag.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-locale")]
    [InlineData("de-DE")]
    public void Resolve_FallsBackToUnitedStatesEnglish(string? requested)
    {
        Assert.Equal(CompanionLocales.DefaultLanguageTag, CompanionLocales.Resolve([requested]).LanguageTag);
    }

    /// <summary>
    /// Verifies the catalog returns reviewed localized shared chrome.
    /// </summary>
    /// <param name="tag">The supported language tag.</param>
    /// <param name="key">The resource key.</param>
    /// <param name="expected">The expected localized value.</param>
    [Theory]
    [InlineData("en-US", CompanionStringKeys.NavigationHome, "Home")]
    [InlineData("es", CompanionStringKeys.NavigationHome, "Inicio")]
    [InlineData("fr", CompanionStringKeys.NavigationMemories, "Souvenirs")]
    [InlineData("it", CompanionStringKeys.NavigationJourney, "Viaggio")]
    public void Catalog_ReturnsLocalizedSharedChrome(string tag, string key, string expected)
    {
        var catalog = new CompanionTextCatalog();

        Assert.Equal(expected, catalog.Get(key, CompanionLocales.Resolve([tag])));
    }

    /// <summary>
    /// Verifies every declared key resolves for every supported locale.
    /// </summary>
    [Fact]
    public void Catalog_AllDeclaredKeysResolveForEverySupportedLocale()
    {
        var catalog = new CompanionTextCatalog();
        string[] keys =
        [
            CompanionStringKeys.AppName,
            CompanionStringKeys.PreparingAdventure,
            CompanionStringKeys.Adventure,
            CompanionStringKeys.Current,
            CompanionStringKeys.Planned,
            CompanionStringKeys.Offline,
            CompanionStringKeys.NavigationHome,
            CompanionStringKeys.NavigationJourney,
            CompanionStringKeys.NavigationReady,
            CompanionStringKeys.NavigationMemories
        ];

        foreach (var locale in CompanionLocales.Supported)
        {
            foreach (var key in keys)
            {
                Assert.False(string.IsNullOrWhiteSpace(catalog.Get(key, locale)));
            }
        }
    }
}
