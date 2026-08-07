using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Resources;

/// <summary>Warms and validates every Creator-owned resource registry during application startup.</summary>
public sealed class ResourceValidationHostedService : IHostedService
{
    private readonly ICreatorService _creatorService;
    private readonly IResourceService _resourceService;
    private readonly ILogger<ResourceValidationHostedService> _logger;

    /// <summary>Initializes startup resource validation.</summary>
    /// <param name="creatorService">The immutable Creator registry.</param>
    /// <param name="resourceService">The Creator-scoped resource registry.</param>
    /// <param name="logger">The startup diagnostic logger.</param>
    public ResourceValidationHostedService(ICreatorService creatorService, IResourceService resourceService, ILogger<ResourceValidationHostedService> logger)
    {
        _creatorService = creatorService;
        _resourceService = resourceService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var creators = await _creatorService.GetAllAsync(cancellationToken);
        var resourceCount = 0;
        foreach (var creator in creators)
        {
            resourceCount += (await _resourceService.GetAllAsync(creator.Id, cancellationToken)).Count;
        }

        _logger.LogInformation("Validated {ResourceCount} resource record(s) across {CreatorCount} Creator(s).", resourceCount, creators.Count);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
