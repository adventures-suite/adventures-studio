using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Resources;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Provides deterministic Creator-scoped resource references to validation tests.</summary>
internal sealed class StubResourceService : IResourceService
{
    private readonly bool _resolveKnownHeroForEveryCreator;
    private readonly string _knownHeroUrl;
    private readonly Dictionary<(CreatorId CreatorId, ResourceId ResourceId), ResourceRecord> _resources = [];

    internal StubResourceService(
        bool resolveKnownHeroForEveryCreator = true,
        string knownHeroUrl = "/images/test-hero.jpeg")
    {
        _resolveKnownHeroForEveryCreator = resolveKnownHeroForEveryCreator;
        _knownHeroUrl = knownHeroUrl;
    }

    internal void Add(ResourceRecord resource) =>
        _resources.Add((resource.CreatorId, resource.Id), resource);

    public Task<IReadOnlyList<ResourceRecord>> GetAllAsync(CreatorId creatorId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ResourceRecord>>(
            _resources.Values.Where(resource => resource.CreatorId == creatorId).ToArray());

    public Task<ResourceRecord?> GetByIdAsync(CreatorId creatorId, ResourceId resourceId, CancellationToken cancellationToken = default)
    {
        _resources.TryGetValue((creatorId, resourceId), out var resource);
        return Task.FromResult(resource);
    }

    public Task<string?> GetPublicUrlAsync(CreatorId creatorId, ResourceId resourceId, CancellationToken cancellationToken = default)
    {
        if (_resources.TryGetValue((creatorId, resourceId), out var resource)
            && resource.PublicationStatus == ResourcePublicationStatus.Published)
        {
            return Task.FromResult<string?>(resource.StorageLocation);
        }

        return Task.FromResult<string?>(
            _resolveKnownHeroForEveryCreator
                ? _knownHeroUrl
                : null);
    }

    public async Task<ResolvedResource?> ResolvePublicAsync(CreatorId creatorId, ResourceId resourceId, CancellationToken cancellationToken = default)
    {
        var url = await GetPublicUrlAsync(creatorId, resourceId, cancellationToken);
        if (url is null)
        {
            return null;
        }

        var resource = await GetByIdAsync(creatorId, resourceId, cancellationToken)
            ?? new ResourceRecord
            {
                Id = resourceId,
                CreatorId = creatorId,
                AlternativeText = "Test resource",
                StorageLocation = url,
                PublicationStatus = ResourcePublicationStatus.Published
            };
        return new ResolvedResource { Resource = resource, PublicUrl = url };
    }
}
