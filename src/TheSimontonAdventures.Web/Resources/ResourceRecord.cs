using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Resources;

/// <summary>Stores Creator-owned identity, storage, accessibility, and rights metadata for a resource.</summary>
public sealed class ResourceRecord
{
    /// <summary>Gets the stable resource identity.</summary>
    public ResourceId Id { get; init; }

    /// <summary>Gets the Creator that owns the resource.</summary>
    public CreatorId CreatorId { get; init; }

    /// <summary>Gets the resource media type.</summary>
    public ResourceType Type { get; init; }

    /// <summary>Gets the human-readable resource title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets an optional editorial description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the provider responsible for resolving the storage location.</summary>
    public string StorageProvider { get; init; } = string.Empty;

    /// <summary>Gets the provider-specific, non-public storage location.</summary>
    public string StorageLocation { get; init; } = string.Empty;

    /// <summary>Gets the resource Internet media type.</summary>
    public string MediaType { get; init; } = string.Empty;

    /// <summary>Gets the accessible alternative text for image resources.</summary>
    public string AlternativeText { get; init; } = string.Empty;

    /// <summary>Gets the public attribution required when displaying the resource.</summary>
    public string Attribution { get; init; } = string.Empty;

    /// <summary>Gets the copyright notice associated with the resource.</summary>
    public string Copyright { get; init; } = string.Empty;

    /// <summary>Gets the usage-rights statement governing the resource.</summary>
    public string UsageRights { get; init; } = string.Empty;

    /// <summary>Gets the resource publication state.</summary>
    public ResourcePublicationStatus PublicationStatus { get; init; }
}
