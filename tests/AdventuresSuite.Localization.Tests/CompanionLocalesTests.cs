using AdventuresSuite.Localization;
using Xunit;

namespace AdventuresSuite.Localization.Tests;

public sealed class CompanionLocalesTests
{
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

    [Fact]
    public void Resolve_UsesNextPreferenceBeforeDefault()
    {
        Assert.Equal("fr", CompanionLocales.Resolve(["de-DE", "fr-FR"]).LanguageTag);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-locale")]
    [InlineData("de-DE")]
    public void Resolve_FallsBackToUnitedStatesEnglish(string? requested)
    {
        Assert.Equal(CompanionLocales.DefaultLanguageTag, CompanionLocales.Resolve([requested]).LanguageTag);
    }

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
}
