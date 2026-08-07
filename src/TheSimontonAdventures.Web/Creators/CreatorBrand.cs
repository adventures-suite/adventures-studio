namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Defines structured brand values owned by a Creator and consumed by shared
/// presentation capabilities.
/// </summary>
public sealed class CreatorBrand
{
    /// <summary>Gets the short name presented in shared site chrome.</summary>
    public string SiteName { get; init; } = string.Empty;

    /// <summary>Gets the Creator's concise public positioning statement.</summary>
    public string Tagline { get; init; } = string.Empty;

    /// <summary>Gets the root-relative or absolute logo resource URL.</summary>
    public string LogoUrl { get; init; } = string.Empty;

    /// <summary>Gets the root-relative or absolute favicon resource URL.</summary>
    public string FaviconUrl { get; init; } = string.Empty;

    /// <summary>Gets the root-relative or absolute homepage hero image URL.</summary>
    public string HomeHeroImageUrl { get; init; } = string.Empty;

    /// <summary>Gets accessible alternative text for the homepage hero image.</summary>
    public string HomeHeroImageAlt { get; init; } = string.Empty;

    /// <summary>Gets the copyright notice shown in Creator-branded output.</summary>
    public string CopyrightNotice { get; init; } = string.Empty;

    /// <summary>Gets the default browser and search-result title.</summary>
    public string DefaultSeoTitle { get; init; } = string.Empty;

    /// <summary>Gets the default search-result description.</summary>
    public string DefaultSeoDescription { get; init; } = string.Empty;

    /// <summary>Gets the primary hexadecimal color used by shared chrome.</summary>
    public string PrimaryColor { get; init; } = "#1a2327";

    /// <summary>Gets the accent hexadecimal color used by shared controls.</summary>
    public string AccentColor { get; init; } = "#9a6e3a";

    /// <summary>
    /// Gets the approved typography token used by shared presentation styles.
    /// </summary>
    public CreatorTypography Typography { get; init; } =
        CreatorTypography.Classic;
}
