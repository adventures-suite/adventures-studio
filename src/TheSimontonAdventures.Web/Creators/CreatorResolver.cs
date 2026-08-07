using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Resolves approved request hosts and explicit development aliases to active
/// Creator Context instances.
/// </summary>
public sealed class CreatorResolver : ICreatorResolver
{
    private readonly ICreatorService _creatorService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IReadOnlyDictionary<string, CreatorId> _developmentAliases;
    private readonly IReadOnlyDictionary<string, CreatorId> _environmentAliases;

    /// <summary>Initializes host-based Creator resolution.</summary>
    /// <param name="creatorService">The validated Creator retrieval service.</param>
    /// <param name="hostEnvironment">The active application environment.</param>
    /// <param name="options">Environment-specific Creator resolution settings.</param>
    /// <param name="configuration">
    /// Trusted process configuration, including Azure's environment hostname.
    /// </param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="InvalidDataException">
    /// A development alias host or Creator identity is invalid or duplicated.
    /// </exception>
    public CreatorResolver(
        ICreatorService creatorService,
        IHostEnvironment hostEnvironment,
        IOptions<CreatorResolutionOptions> options,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(creatorService);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        _creatorService = creatorService;
        _hostEnvironment = hostEnvironment;
        _developmentAliases = BuildDevelopmentAliases(options.Value);
        _environmentAliases = BuildEnvironmentAliases(
            options.Value,
            configuration);
    }

    /// <inheritdoc />
    public async Task<CreatorContext?> ResolveAsync(
        HostString host,
        CancellationToken cancellationToken = default)
    {
        if (!CreatorHost.TryNormalize(host.Host, out var normalizedHost))
        {
            return null;
        }

        Creator? creator = null;

        if (_hostEnvironment.IsDevelopment()
            && _developmentAliases.TryGetValue(
                normalizedHost,
                out var aliasedCreatorId))
        {
            creator = await _creatorService.GetByIdAsync(
                aliasedCreatorId,
                cancellationToken);
        }
        else if (_environmentAliases.TryGetValue(
            normalizedHost,
            out var environmentCreatorId))
        {
            creator = await _creatorService.GetByIdAsync(
                environmentCreatorId,
                cancellationToken);
        }
        else
        {
            creator = await _creatorService.GetByHostAsync(
                normalizedHost,
                cancellationToken);
        }

        return creator is { Status: CreatorStatus.Active }
            && (_hostEnvironment.IsDevelopment() || !creator.DevelopmentOnly)
            ? CreateContext(creator, normalizedHost)
            : null;
    }

    private static IReadOnlyDictionary<string, CreatorId> BuildEnvironmentAliases(
        CreatorResolutionOptions options,
        IConfiguration configuration)
    {
        var azureHost = configuration["WEBSITE_HOSTNAME"];

        if (string.IsNullOrWhiteSpace(azureHost))
        {
            return new Dictionary<string, CreatorId>();
        }

        if (!CreatorHost.TryNormalize(azureHost, out var normalizedHost))
        {
            throw new InvalidDataException(
                "Azure WEBSITE_HOSTNAME is not a valid host name.");
        }

        CreatorId creatorId;

        try
        {
            creatorId = new CreatorId(options.AzureDefaultCreatorId);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "CreatorResolution:AzureDefaultCreatorId must identify the " +
                "Creator assigned to Azure WEBSITE_HOSTNAME.",
                exception);
        }

        return new Dictionary<string, CreatorId>(StringComparer.OrdinalIgnoreCase)
        {
            [normalizedHost] = creatorId
        };
    }

    private static IReadOnlyDictionary<string, CreatorId> BuildDevelopmentAliases(
        CreatorResolutionOptions options)
    {
        var aliases = new Dictionary<string, CreatorId>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var alias in options.DevelopmentAliases)
        {
            if (!CreatorHost.TryNormalize(alias.Key, out var normalizedHost))
            {
                throw new InvalidDataException(
                    $"Development Creator alias '{alias.Key}' is not a valid host.");
            }

            CreatorId creatorId;

            try
            {
                creatorId = new CreatorId(alias.Value);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    $"Development Creator alias '{alias.Key}' has an invalid identity.",
                    exception);
            }

            if (!aliases.TryAdd(normalizedHost, creatorId))
            {
                throw new InvalidDataException(
                    $"Development Creator alias '{alias.Key}' is duplicated.");
            }
        }

        return aliases;
    }

    private static CreatorContext CreateContext(
        Creator creator,
        string requestedHost)
    {
        return new CreatorContext
        {
            Id = creator.Id,
            Slug = creator.Slug,
            DisplayName = creator.DisplayName,
            RequestedHost = requestedHost,
            PrimaryDomain = creator.PrimaryDomain,
            Brand = creator.Brand,
            Homepage = creator.Homepage,
            Features = creator.Features,
            Locale = creator.Locale,
            TimeZone = creator.TimeZone,
            ContentRoot = creator.ContentRoot
        };
    }
}
