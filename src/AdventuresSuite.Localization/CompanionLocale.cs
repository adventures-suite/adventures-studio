using System.Globalization;

namespace AdventuresSuite.Localization;

/// <summary>
/// Describes one locale supported by AdventuresCompanion presentation.
/// </summary>
/// <param name="LanguageTag">The canonical BCP 47 language tag.</param>
/// <param name="EnglishName">The administrative English name.</param>
/// <param name="IsInitialReleaseLocale">Whether the locale ships in the initial release.</param>
public sealed record CompanionLocale(
    string LanguageTag,
    string EnglishName,
    bool IsInitialReleaseLocale)
{
    /// <summary>
    /// Creates the culture used to format and retrieve presentation resources.
    /// </summary>
    /// <returns>The corresponding .NET culture.</returns>
    public CultureInfo CreateCulture() => CultureInfo.GetCultureInfo(LanguageTag);
}
