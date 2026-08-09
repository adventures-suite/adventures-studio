using System.Text.Json;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Resources;

/// <summary>Loads immutable Creator-owned resource registries from JSON manifests.</summary>
public sealed class JsonResourceService : IResourceService
{
    private readonly ICreatorService _creatorService;
    private readonly IReadOnlyDictionary<string, IResourceProvider> _providers;
    private readonly string _resourcesRoot;
    private readonly JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly Dictionary<CreatorId, Task<ResourceRegistry>> _registries = [];
    private readonly object _registryLock = new();

    /// <summary>Initializes JSON-backed resource retrieval.</summary>
    /// <param name="creatorService">The registry used to validate Creator ownership.</param>
    /// <param name="providers">The configured storage providers, keyed by provider identity.</param>
    /// <param name="environment">The application environment containing resource manifests.</param>
    public JsonResourceService(ICreatorService creatorService, IEnumerable<IResourceProvider> providers, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(creatorService);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(environment);
        _creatorService = creatorService;
        _providers = providers.ToDictionary(provider => provider.Key, StringComparer.OrdinalIgnoreCase);
        _resourcesRoot = Path.Combine(environment.ContentRootPath, "Content", "Resources");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResourceRecord>> GetAllAsync(CreatorId creatorId, CancellationToken cancellationToken = default) =>
        (await GetRegistryAsync(creatorId, cancellationToken)).Resources;

    /// <inheritdoc />
    public async Task<ResourceRecord?> GetByIdAsync(CreatorId creatorId, ResourceId resourceId, CancellationToken cancellationToken = default)
    {
        ValidateIds(creatorId, resourceId);
        return (await GetRegistryAsync(creatorId, cancellationToken)).ById.GetValueOrDefault(resourceId);
    }

    /// <inheritdoc />
    public async Task<string?> GetPublicUrlAsync(CreatorId creatorId, ResourceId resourceId, CancellationToken cancellationToken = default)
        => (await ResolvePublicAsync(creatorId, resourceId, cancellationToken))?.PublicUrl;

    /// <inheritdoc />
    public async Task<ResolvedResource?> ResolvePublicAsync(CreatorId creatorId, ResourceId resourceId, CancellationToken cancellationToken = default)
    {
        var resource = await GetByIdAsync(creatorId, resourceId, cancellationToken);
        if (resource is null || resource.PublicationStatus != ResourcePublicationStatus.Published)
        {
            return null;
        }

        return new ResolvedResource
        {
            Resource = resource,
            PublicUrl = _providers[resource.StorageProvider].GetPublicUrl(resource)
        };
    }

    private async Task<ResourceRegistry> GetRegistryAsync(CreatorId creatorId, CancellationToken cancellationToken)
    {
        if (creatorId == default)
        {
            throw new ArgumentException("A non-default Creator identity is required.", nameof(creatorId));
        }

        Task<ResourceRegistry> registryTask;
        lock (_registryLock)
        {
            if (!_registries.TryGetValue(creatorId, out registryTask!))
            {
                registryTask = LoadRegistryAsync(creatorId);
                _registries.Add(creatorId, registryTask);
            }
        }

        return await registryTask.WaitAsync(cancellationToken);
    }

    private async Task<ResourceRegistry> LoadRegistryAsync(CreatorId creatorId)
    {
        var creator = await _creatorService.GetByIdAsync(creatorId)
            ?? throw new InvalidDataException($"Resource registry references unknown Creator '{creatorId}'.");
        var manifestPath = Path.Combine(_resourcesRoot, creator.Slug, "resources.json");
        if (!File.Exists(manifestPath))
        {
            return ResourceRegistry.Empty;
        }

        try
        {
            await using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<ResourceManifest>(stream, _serializerOptions)
                ?? throw new InvalidDataException($"Resource manifest '{manifestPath}' is empty.");
            Validate(manifest.Resources, creatorId, manifestPath);
            return new ResourceRegistry(manifest.Resources, manifest.Resources.ToDictionary(resource => resource.Id));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Resource manifest '{manifestPath}' is invalid.", exception);
        }
    }

    private void Validate(IReadOnlyList<ResourceRecord> resources, CreatorId creatorId, string manifestPath)
    {
        var ids = new HashSet<ResourceId>();
        foreach (var resource in resources)
        {
            if (resource.Id == default || resource.CreatorId != creatorId || !ids.Add(resource.Id))
            {
                throw new InvalidDataException($"Resource identity or ownership is invalid in '{manifestPath}'.");
            }

            if (string.IsNullOrWhiteSpace(resource.Title)
                || string.IsNullOrWhiteSpace(resource.MediaType)
                || string.IsNullOrWhiteSpace(resource.AlternativeText)
                || string.IsNullOrWhiteSpace(resource.Attribution)
                || string.IsNullOrWhiteSpace(resource.Copyright)
                || string.IsNullOrWhiteSpace(resource.UsageRights)
                || !_providers.TryGetValue(resource.StorageProvider, out var provider))
            {
                throw new InvalidDataException($"Resource '{resource.Id}' has incomplete metadata or an unknown provider.");
            }

            var expectedMediaType = Path.GetExtension(resource.StorageLocation).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".svg" => "image/svg+xml",
                _ => null
            };
            if (expectedMediaType is null
                || !string.Equals(resource.MediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Resource '{resource.Id}' media type does not match its storage file extension.");
            }

            // Provider validation proves that public references are safe and resolvable at startup.
            provider.GetPublicUrl(resource);
        }
    }

    private static void ValidateIds(CreatorId creatorId, ResourceId resourceId)
    {
        if (creatorId == default || resourceId == default)
        {
            throw new ArgumentException("Non-default Creator and resource identities are required.");
        }
    }

    private sealed class ResourceManifest
    {
        public IReadOnlyList<ResourceRecord> Resources { get; init; } = [];
    }

    private sealed record ResourceRegistry(IReadOnlyList<ResourceRecord> Resources, IReadOnlyDictionary<ResourceId, ResourceRecord> ById)
    {
        internal static ResourceRegistry Empty { get; } = new([], new Dictionary<ResourceId, ResourceRecord>());
    }
}
