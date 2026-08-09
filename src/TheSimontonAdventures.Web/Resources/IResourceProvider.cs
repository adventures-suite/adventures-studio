namespace TheSimontonAdventures.Web.Resources;

/// <summary>Resolves provider-specific storage locations into public resource URLs.</summary>
public interface IResourceProvider
{
    /// <summary>Gets the stable provider key stored in resource records.</summary>
    string Key { get; }

    /// <summary>Validates storage and returns the resource's public URL.</summary>
    /// <param name="resource">The published resource to resolve.</param>
    /// <returns>The storage-independent public URL.</returns>
    string GetPublicUrl(ResourceRecord resource);
}
