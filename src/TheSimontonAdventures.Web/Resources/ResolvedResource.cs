namespace TheSimontonAdventures.Web.Resources;

/// <summary>Combines a published resource record with its provider-resolved public URL.</summary>
public sealed record ResolvedResource
{
    /// <summary>Gets the validated Creator-owned resource metadata.</summary>
    public required ResourceRecord Resource { get; init; }

    /// <summary>Gets the public URL produced by the configured storage provider.</summary>
    public required string PublicUrl { get; init; }
}
