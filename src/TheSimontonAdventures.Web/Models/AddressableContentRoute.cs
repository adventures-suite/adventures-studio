namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Represents a stable public address and the current target to which it resolves.
/// </summary>
/// <remarks>
/// The Address Engine returns this model after resolving a creator-scoped public
/// slug.
///
/// The public <see cref="Slug"/> is intended to remain stable even when the
/// internal <see cref="TargetUrl"/> changes. This allows QR codes, printed books,
/// deep links, and other published references to remain valid while AdventuresSuite
/// evolves internally.
/// </remarks>
public sealed class AddressableContentRoute
{
    /// <summary>
    /// Gets the stable public slug used to address the target.
    /// </summary>
    /// <remarks>
    /// The slug should not include route prefixes such as <c>/go/</c>.
    ///
    /// Example:
    /// <c>acropolis</c>
    /// </remarks>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable title of the addressable target.
    /// </summary>
    /// <remarks>
    /// This value may be used by diagnostics, administrative tools, QR manifests,
    /// search results, or other platform features that need descriptive metadata.
    /// </remarks>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the platform content category represented by this address.
    /// </summary>
    public AddressableContentType ContentType { get; init; }

    /// <summary>
    /// Gets the current canonical URL to which the public address resolves.
    /// </summary>
    /// <remarks>
    /// Internal targets should normally use a root-relative application URL.
    ///
    /// Example:
    /// <c>/volumes/italy-greece-croatia/greece/athens/experiences/acropolis</c>
    ///
    /// Approved external targets may use an absolute HTTP or HTTPS URL.
    /// </remarks>
    public string TargetUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the target is available for public use.
    /// </summary>
    /// <remarks>
    /// Unpublished targets should not be exposed through QR generation, public
    /// redirects, search, navigation, or other public platform capabilities.
    /// </remarks>
    public bool Published { get; init; }

    /// <summary>
    /// Gets alternate public slugs that resolve to the same target.
    /// </summary>
    /// <remarks>
    /// Aliases preserve historical links and printed QR codes when naming or
    /// route conventions evolve.
    ///
    /// Alias uniqueness must be validated within the applicable Creator scope.
    /// </remarks>
    public IReadOnlyList<string> Aliases { get; init; } = [];
}