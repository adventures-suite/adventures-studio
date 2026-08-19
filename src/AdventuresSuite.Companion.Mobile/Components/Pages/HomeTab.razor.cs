using AdventuresSuite.Localization;
using Microsoft.AspNetCore.Components;

namespace AdventuresSuite.Companion.Mobile.Components.Pages;

/// <summary>
/// Supplies localized shared chrome to the Companion home tab.
/// </summary>
public partial class HomeTab
{
    [Inject]
    private CompanionTextCatalog TextCatalog { get; set; } = null!;

    private CompanionLocale Locale { get; } =
        CompanionLocales.Resolve([System.Globalization.CultureInfo.CurrentUICulture.Name]);
}
