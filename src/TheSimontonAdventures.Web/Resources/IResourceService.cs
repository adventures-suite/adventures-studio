using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Resources;

/// <summary>Defines Creator-scoped lookup of validated resource records.</summary>
public interface IResourceService
{
    /// <summary>Gets every resource owned by a Creator.</summary>
    Task<IReadOnlyList<ResourceRecord>> GetAllAsync(CreatorId creatorId, CancellationToken cancellationToken = default);

    /// <summary>Gets a resource only within the specified Creator boundary.</summary>
    Task<ResourceRecord?> GetByIdAsync(CreatorId creatorId, ResourceId resourceId, CancellationToken cancellationToken = default);

    /// <summary>Resolves a published Creator-owned resource reference to its public URL.</summary>
    Task<string?> GetPublicUrlAsync(CreatorId creatorId, ResourceId resourceId, CancellationToken cancellationToken = default);
}
