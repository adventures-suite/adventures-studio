using System.Globalization;
using System.Resources;

namespace AdventuresSuite.Localization;

/// <summary>
/// Retrieves localized Companion presentation text from embedded resources.
/// </summary>
public sealed class CompanionTextCatalog
{
    private static readonly ResourceManager Resources = new(
        "AdventuresSuite.Localization.Resources.CompanionStrings",
        typeof(CompanionTextCatalog).Assembly);

    /// <summary>
    /// Retrieves a required localized value for a supported locale.
    /// </summary>
    /// <param name="key">A stable key from <see cref="CompanionStringKeys"/>.</param>
    /// <param name="locale">The resolved presentation locale.</param>
    /// <returns>The localized value, using the English resource fallback when necessary.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the key is absent from the fallback resource.</exception>
    public string Get(string key, CompanionLocale locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(locale);

        return Resources.GetString(key, locale.CreateCulture())
            ?? Resources.GetString(key, CultureInfo.GetCultureInfo(CompanionLocales.DefaultLanguageTag))
            ?? throw new InvalidOperationException($"Companion resource key '{key}' is not defined.");
    }
}
