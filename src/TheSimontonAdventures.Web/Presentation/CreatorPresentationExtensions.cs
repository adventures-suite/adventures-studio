using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Presentation;

/// <summary>
/// Provides safe presentation fallbacks for optional Creator brand values.
/// </summary>
public static class CreatorPresentationExtensions
{
    /// <summary>Returns the configured site name or Creator display name.</summary>
    /// <param name="context">The resolved Creator Context.</param>
    /// <returns>The name displayed by shared site chrome.</returns>
    public static string GetSiteName(this CreatorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueOrFallback(context.Brand.SiteName, context.DisplayName);
    }

    /// <summary>Returns the configured SEO title or effective site name.</summary>
    /// <param name="context">The resolved Creator Context.</param>
    /// <returns>The default page title.</returns>
    public static string GetDefaultSeoTitle(this CreatorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueOrFallback(
            context.Brand.DefaultSeoTitle,
            context.GetSiteName());
    }

    /// <summary>Returns the configured SEO description or brand tagline.</summary>
    /// <param name="context">The resolved Creator Context.</param>
    /// <returns>The default metadata description.</returns>
    public static string GetDefaultSeoDescription(this CreatorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueOrFallback(
            context.Brand.DefaultSeoDescription,
            context.Brand.Tagline);
    }

    /// <summary>Returns the configured copyright or effective site name.</summary>
    /// <param name="context">The resolved Creator Context.</param>
    /// <returns>The shared footer copyright text.</returns>
    public static string GetCopyrightNotice(this CreatorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueOrFallback(
            context.Brand.CopyrightNotice,
            context.GetSiteName());
    }

    /// <summary>Returns the approved CSS font stack for the typography token.</summary>
    /// <param name="context">The resolved Creator Context.</param>
    /// <returns>A platform-owned CSS font stack.</returns>
    public static string GetFontFamily(this CreatorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Brand.Typography switch
        {
            CreatorTypography.Modern =>
                "Inter, ui-sans-serif, system-ui, sans-serif",
            _ => "Georgia, 'Times New Roman', serif"
        };
    }

    /// <summary>Returns the current year in the Creator's configured time zone.</summary>
    /// <param name="context">The resolved Creator Context.</param>
    /// <returns>The Creator-local calendar year.</returns>
    public static int GetLocalYear(this CreatorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow,
            context.TimeZone).Year;
    }

    private static string ValueOrFallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
