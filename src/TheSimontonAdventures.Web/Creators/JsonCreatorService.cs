using System.Text.Json;

namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Retrieves validated Creator manifests from the application's JSON content
/// directory.
/// </summary>
public sealed class JsonCreatorService : ICreatorService
{
    private readonly string _applicationContentRoot;
    private readonly string _creatorsDirectory;
    private readonly bool _isDevelopment;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly Lazy<Task<CreatorRegistry>> _registry;

    /// <summary>Initializes Creator retrieval from the deployed content root.</summary>
    /// <param name="hostEnvironment">The active application host environment.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="hostEnvironment"/> is <see langword="null"/>.
    /// </exception>
    public JsonCreatorService(IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        _applicationContentRoot = hostEnvironment.ContentRootPath;
        _isDevelopment = hostEnvironment.IsDevelopment();
        _creatorsDirectory = Path.Combine(
            _applicationContentRoot,
            "Content",
            "Creators");
        _registry = new Lazy<Task<CreatorRegistry>>(
            () => LoadRegistryAsync(CancellationToken.None),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Creator>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var registry = await _registry.Value.WaitAsync(cancellationToken);
        return registry.Creators;
    }

    /// <inheritdoc />
    public async Task<Creator?> GetByIdAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        if (creatorId == default)
        {
            throw new ArgumentException(
                "A non-default Creator identity is required.",
                nameof(creatorId));
        }

        var registry = await _registry.Value.WaitAsync(cancellationToken);
        return registry.ById.GetValueOrDefault(creatorId);
    }

    /// <inheritdoc />
    public async Task<Creator?> GetByHostAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        if (!CreatorHost.TryNormalize(host, out var normalizedHost))
        {
            return null;
        }

        var registry = await _registry.Value.WaitAsync(cancellationToken);
        var creator = registry.ByDomain.GetValueOrDefault(normalizedHost);
        return creator is { Status: CreatorStatus.Active }
            && (_isDevelopment || !creator.DevelopmentOnly)
                ? creator
                : null;
    }

    private async Task<CreatorRegistry> LoadRegistryAsync(
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_creatorsDirectory))
        {
            return CreatorRegistry.Empty;
        }

        var creators = new List<Creator>();
        var creatorIds = new HashSet<CreatorId>();
        var creatorSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var approvedDomains = new Dictionary<string, CreatorId>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var manifestPath in Directory
            .EnumerateFiles(
                _creatorsDirectory,
                "creator.json",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var creator = await DeserializeManifestAsync(
                manifestPath,
                cancellationToken);

            CreatorManifestValidator.Validate(
                creator,
                _applicationContentRoot);

            if (!creatorIds.Add(creator.Id))
            {
                throw new InvalidDataException(
                    $"Creator identity '{creator.Id}' is registered more than once.");
            }

            if (!creatorSlugs.Add(creator.Slug))
            {
                throw new InvalidDataException(
                    $"Creator slug '{creator.Slug}' is registered more than once.");
            }

            foreach (var domain in creator.Domains)
            {
                CreatorHost.TryNormalize(domain, out var normalizedDomain);

                if (!approvedDomains.TryAdd(normalizedDomain, creator.Id))
                {
                    throw new InvalidDataException(
                        $"Creator domain '{domain}' is registered to multiple Creators.");
                }
            }

            creators.Add(creator);
        }

        return new CreatorRegistry(
            creators.ToArray(),
            creators.ToDictionary(creator => creator.Id),
            creators
                .SelectMany(creator => creator.Domains.Select(domain =>
                {
                    CreatorHost.TryNormalize(domain, out var normalizedDomain);
                    return new KeyValuePair<string, Creator>(
                        normalizedDomain,
                        creator);
                }))
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase));
    }

    private async Task<Creator> DeserializeManifestAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            var creator = await JsonSerializer.DeserializeAsync<Creator>(
                stream,
                _serializerOptions,
                cancellationToken);

            return creator ?? throw new InvalidDataException(
                $"Creator manifest '{manifestPath}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Creator manifest '{manifestPath}' is invalid.",
                exception);
        }
    }

    private sealed record CreatorRegistry(
        IReadOnlyList<Creator> Creators,
        IReadOnlyDictionary<CreatorId, Creator> ById,
        IReadOnlyDictionary<string, Creator> ByDomain)
    {
        internal static CreatorRegistry Empty { get; } = new(
            [],
            new Dictionary<CreatorId, Creator>(),
            new Dictionary<string, Creator>(StringComparer.OrdinalIgnoreCase));
    }
}
