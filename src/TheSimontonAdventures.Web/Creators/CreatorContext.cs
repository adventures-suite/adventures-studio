namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Provides an immutable request view of the resolved Creator identity and the
/// Creator-owned values required by downstream platform capabilities.
/// </summary>
public sealed record CreatorContext
{
    /// <summary>Gets the stable identity required by core engine operations.</summary>
    public required CreatorId Id { get; init; }

    /// <summary>Gets the Creator's public slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the Creator's public display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the normalized host that resolved this context.</summary>
    public required string RequestedHost { get; init; }

    /// <summary>Gets the canonical public domain used for durable URLs.</summary>
    public required string PrimaryDomain { get; init; }

    /// <summary>Gets the Creator's structured presentation configuration.</summary>
    public required CreatorBrand Brand { get; init; }

    /// <summary>Gets the Creator-owned homepage composition.</summary>
    public CreatorHomepage Homepage { get; init; } = new();

    /// <summary>Gets the Creator-scoped feature configuration.</summary>
    public required CreatorFeatures Features { get; init; }

    /// <summary>Gets the default locale for Creator-owned experiences.</summary>
    public required string Locale { get; init; }

    /// <summary>Gets the IANA time-zone identifier for Creator-owned content.</summary>
    public required string TimeZone { get; init; }

    /// <summary>Gets the validated content-root identity used during JSON migration.</summary>
    public required string ContentRoot { get; init; }
}
