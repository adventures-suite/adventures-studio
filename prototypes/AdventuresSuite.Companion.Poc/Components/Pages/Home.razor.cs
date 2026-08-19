using AdventuresSuite.Localization;
using Microsoft.AspNetCore.Components;

namespace AdventuresSuite.Companion.Poc.Components.Pages;

/// <summary>
/// Supplies localized shared chrome to the Companion prototype home page.
/// </summary>
public partial class Home
{
    [Inject]
    private CompanionTextCatalog TextCatalog { get; set; } = null!;

    private CompanionLocale Locale { get; } =
        CompanionLocales.Resolve([System.Globalization.CultureInfo.CurrentUICulture.Name]);

    private string Text(string key) => TextCatalog.Get(key, Locale);
}
