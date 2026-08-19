using System.Collections.ObjectModel;
using System.Globalization;

namespace AdventuresSuite.Localization;

/// <summary>
/// Defines supported Companion locales and deterministic fallback behavior.
/// </summary>
public static class CompanionLocales
{
    /// <summary>
    /// The required fallback and initial-release locale.
    /// </summary>
    public const string DefaultLanguageTag = "en-US";

    private static readonly ReadOnlyCollection<CompanionLocale> SupportedValues =
        Array.AsReadOnly(
        [
            new CompanionLocale(DefaultLanguageTag, "English (United States)", true),
            new CompanionLocale("es", "Spanish", false),
            new CompanionLocale("fr", "French", false),
            new CompanionLocale("it", "Italian", false)
        ]);

    /// <summary>
    /// Gets the ordered locales supported by the product direction.
    /// </summary>
    public static IReadOnlyList<CompanionLocale> Supported => SupportedValues;

    /// <summary>
    /// Resolves the first supported preference and falls back to United States English.
    /// Regional Spanish, French, and Italian preferences fall back to their neutral
    /// language resource until a reviewed regional variant is introduced.
    /// </summary>
    /// <param name="preferredLanguageTags">Language tags in descending preference order.</param>
    /// <returns>The supported locale selected for presentation.</returns>
    public static CompanionLocale Resolve(params IEnumerable<string?> preferredLanguageTags)
    {
        ArgumentNullException.ThrowIfNull(preferredLanguageTags);

        foreach (var candidate in preferredLanguageTags)
        {
            var languageTag = Normalize(candidate);
            var locale = SupportedValues.FirstOrDefault(
                value => string.Equals(value.LanguageTag, languageTag, StringComparison.OrdinalIgnoreCase));

            if (locale is not null)
            {
                return locale;
            }
        }

        return SupportedValues[0];
    }

    private static string? Normalize(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(candidate.Trim().Replace('_', '-'));
            return culture.TwoLetterISOLanguageName switch
            {
                "en" => DefaultLanguageTag,
                "es" => "es",
                "fr" => "fr",
                "it" => "it",
                _ => null
            };
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
