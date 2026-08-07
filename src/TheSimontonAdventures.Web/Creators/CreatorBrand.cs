using TheSimontonAdventures.Web.Resources;

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

    /// <summary>Gets the optional stable Creator-owned logo resource identity.</summary>
    public ResourceId? LogoResourceId { get; init; }

    /// <summary>Gets the stable Creator-owned favicon resource identity.</summary>
    public ResourceId FaviconResourceId { get; init; }

    /// <summary>Gets the stable Creator-owned homepage hero resource identity.</summary>
    public ResourceId HomeHeroResourceId { get; init; }

    /// <summary>
    /// Gets the immediately renderable homepage hero URL. Startup validation
    /// requires this URL to match <see cref="HomeHeroResourceId"/>.
    /// </summary>
    /// <summary>Gets the Creator-authored homepage hero headline.</summary>
    public string HomeHeroHeadline { get; init; } = string.Empty;

    /// <summary>Gets the Creator-authored homepage hero description.</summary>
    public string HomeHeroDescription { get; init; } = string.Empty;

    /// <summary>Gets the Creator-authored homepage hero action label.</summary>
    public string HomeHeroActionLabel { get; init; } = string.Empty;

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
