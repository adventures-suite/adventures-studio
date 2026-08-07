namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Represents a tenant that owns AdventuresSuite content, addresses, resources,
/// branding, and feature configuration.
/// </summary>
public sealed class Creator
{
    /// <summary>Gets the stable, storage-independent Creator identity.</summary>
    public required CreatorId Id { get; init; }

    /// <summary>Gets the mutable human-readable slug used to organize Creator data.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the Creator's public display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the Creator's lifecycle and public-availability status.</summary>
    public CreatorStatus Status { get; init; } = CreatorStatus.Draft;

    /// <summary>
    /// Gets whether the Creator may resolve only in the Development environment.
    /// </summary>
    public bool DevelopmentOnly { get; init; }

    /// <summary>Gets the canonical public domain used for durable URLs.</summary>
    public required string PrimaryDomain { get; init; }

    /// <summary>Gets every explicitly approved public domain for the Creator.</summary>
    public IReadOnlyList<string> Domains { get; init; } = [];

    /// <summary>Gets the Creator's structured presentation configuration.</summary>
    public CreatorBrand Brand { get; init; } = new();

    /// <summary>Gets the Creator-scoped feature configuration.</summary>
    public CreatorFeatures Features { get; init; } = new();

    /// <summary>Gets the default locale used for Creator-owned experiences.</summary>
    public string Locale { get; init; } = "en-US";

    /// <summary>Gets the IANA time-zone identifier used for Creator-owned content.</summary>
    public string TimeZone { get; init; } = "UTC";

    /// <summary>
    /// Gets the application-relative directory containing this Creator's travel
    /// content during the JSON-backed transition.
    /// </summary>
    public required string ContentRoot { get; init; }
}
