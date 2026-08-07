using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Resources;
using TheSimontonAdventures.Web.Services;

namespace TheSimontonAdventures.Web.Validation;

/// <summary>
/// Validates JSON content references, publication rules, public slugs, and
/// local media without crossing Creator ownership boundaries.
/// </summary>
public sealed class CreatorContentValidator : ICreatorContentValidator
{
    private readonly ICreatorService _creatorService;
    private readonly ITravelContentService _contentService;
    private readonly IResourceService _resourceService;

    /// <summary>Initializes Creator-scoped deployed-content validation.</summary>
    /// <param name="hostEnvironment">The application host environment.</param>
    /// <param name="creatorService">The Creator registry.</param>
    /// <param name="contentService">The Creator-scoped Content Engine.</param>
    /// <param name="resourceService">The Creator-scoped Resource Engine.</param>
    public CreatorContentValidator(
        IHostEnvironment hostEnvironment,
        ICreatorService creatorService,
        ITravelContentService contentService,
        IResourceService resourceService)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(creatorService);
        ArgumentNullException.ThrowIfNull(contentService);
        ArgumentNullException.ThrowIfNull(resourceService);

        _creatorService = creatorService;
        _contentService = contentService;
        _resourceService = resourceService;
    }

    /// <inheritdoc />
    public async Task<CreatorContentValidationResult> ValidateAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        if (creatorId == default)
        {
            throw new ArgumentException(
                "A non-default Creator identity is required.",
                nameof(creatorId));
        }

        var creator = await _creatorService.GetByIdAsync(
            creatorId,
            cancellationToken) ?? throw new InvalidDataException(
                $"Creator '{creatorId}' is not registered.");
        var issues = new List<ContentValidationIssue>();
        var volumes = await _contentService.GetVolumesAsync(
            creatorId,
            cancellationToken);

        AddDuplicateIssues(
            creatorId,
            volumes,
            volume => volume.Slug,
            "duplicate-volume-slug",
            "Volume slug",
            issues);

        if (volumes.Count(volume => volume.Status == VolumeStatus.Current) > 1)
        {
            AddIssue(
                creatorId,
                ContentValidationSeverity.Error,
                "multiple-current-volumes",
                "A Creator may have at most one current volume.",
                issues);
        }

        await ValidateResourceImageAsync(
            creatorId,
            creator.Brand.LogoResourceId,
            "Creator logo",
            required: false,
            issues,
            cancellationToken);
        await ValidateResourceImageAsync(
            creatorId,
            creator.Brand.FaviconResourceId,
            "Creator favicon",
            required: true,
            issues,
            cancellationToken);
        var homepageHeroUrl = await _resourceService.GetPublicUrlAsync(
            creatorId,
            creator.Brand.HomeHeroResourceId,
            cancellationToken);
        if (homepageHeroUrl is null)
        {
            AddIssue(
                creatorId,
                ContentValidationSeverity.Error,
                "invalid-homepage-hero-resource",
                $"Homepage hero resource '{creator.Brand.HomeHeroResourceId}' is missing, unpublished, or owned by another Creator.",
                issues);
        }
        var profile = await _contentService.GetCreatorProfileAsync(
            creatorId,
            cancellationToken);
        await ValidateResourceImageAsync(
            creatorId,
            profile?.HeroResourceId,
            "Creator About hero",
            required: profile?.HeroResourceId is not null,
            issues,
            cancellationToken);

        var publicQrSlugs = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var volume in volumes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ValidateResourceImageAsync(
                creatorId,
                volume.CoverResourceId,
                $"Volume '{volume.Slug}' cover",
                required: true,
                issues,
                cancellationToken);
            await ValidateResourceImageAsync(
                creatorId,
                volume.HeroResourceId,
                $"Volume '{volume.Slug}' hero",
                required: false,
                issues,
                cancellationToken);
            ValidateDuplicateReferences(creatorId, volume, issues);

            var destinations = await _contentService.GetDestinationsForVolumeAsync(
                creatorId,
                volume.Slug,
                cancellationToken);
            var destinationKeys = destinations.ToDictionary(
                destination => RouteKey(
                    destination.CountrySlug,
                    destination.Slug),
                StringComparer.OrdinalIgnoreCase);

            foreach (var reference in volume.Destinations.Where(reference =>
                !string.IsNullOrWhiteSpace(reference.CountrySlug)
                && !string.IsNullOrWhiteSpace(reference.DestinationSlug)))
            {
                if (!destinationKeys.ContainsKey(RouteKey(
                    reference.CountrySlug,
                    reference.DestinationSlug)))
                {
                    AddIssue(
                        creatorId,
                        MissingReferenceSeverity(volume.Status),
                        "missing-destination-reference",
                        $"Volume '{volume.Slug}' references missing destination " +
                        $"'{reference.CountrySlug}/{reference.DestinationSlug}'.",
                        issues);
                }
            }

            foreach (var destination in destinations)
            {
                await ValidateDestinationImagesAsync(
                    creatorId,
                    destination,
                    issues,
                    cancellationToken);

                if (volume.Status.IsPubliclyVisible() && destination.Published)
                {
                    RegisterQrSlug(
                        creatorId,
                        destination.QrSlug,
                        destination.Slug,
                        publicQrSlugs,
                        issues);

                    foreach (var alias in destination.QrAliases)
                    {
                        RegisterQrSlug(
                            creatorId,
                            alias,
                            destination.Slug,
                            publicQrSlugs,
                            issues);
                    }
                }
            }

            foreach (var reference in volume.Journeys.Where(reference =>
                !string.IsNullOrWhiteSpace(reference.Slug)))
            {
                var journey = await _contentService.GetJourneyAsync(
                    creatorId,
                    volume.Slug,
                    reference.Slug,
                    cancellationToken);

                if (journey is null)
                {
                    AddIssue(
                        creatorId,
                        MissingReferenceSeverity(volume.Status),
                        "missing-journey-reference",
                        $"Volume '{volume.Slug}' references missing journey " +
                        $"'{reference.Slug}'.",
                        issues);
                }
            }
        }

        return new CreatorContentValidationResult
        {
            CreatorId = creatorId,
            Issues = issues.ToArray()
        };
    }

    private static void ValidateDuplicateReferences(
        CreatorId creatorId,
        Volume volume,
        ICollection<ContentValidationIssue> issues)
    {
        AddDuplicateIssues(
            creatorId,
            volume.Destinations.Where(reference =>
                !string.IsNullOrWhiteSpace(reference.CountrySlug)
                && !string.IsNullOrWhiteSpace(reference.DestinationSlug)),
            reference => RouteKey(
                reference.CountrySlug,
                reference.DestinationSlug),
            "duplicate-destination-reference",
            $"Destination reference in volume '{volume.Slug}'",
            issues);
        AddDuplicateIssues(
            creatorId,
            volume.Journeys.Where(reference =>
                !string.IsNullOrWhiteSpace(reference.Slug)),
            reference => reference.Slug,
            "duplicate-journey-reference",
            $"Journey reference in volume '{volume.Slug}'",
            issues);
    }

    private async Task ValidateDestinationImagesAsync(
        CreatorId creatorId,
        Destination destination,
        ICollection<ContentValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        await ValidateResourceImageAsync(
            creatorId,
            destination.HeroResourceId,
            $"Destination '{destination.Slug}' hero",
            required: true,
            issues,
            cancellationToken);
        await ValidateResourceImageAsync(
            creatorId,
            destination.HomepageResourceId,
            $"Destination '{destination.Slug}' homepage",
            required: true,
            issues,
            cancellationToken);

        foreach (var section in destination.Sections)
        {
            await ValidateResourceImageAsync(
                creatorId,
                section.ImageResourceId,
                $"Destination '{destination.Slug}' section",
                required: section.ImageResourceId is not null,
                issues,
                cancellationToken);
        }

        foreach (var image in destination.Gallery)
        {
            await ValidateResourceImageAsync(
                creatorId,
                image.ResourceId,
                $"Destination '{destination.Slug}' gallery",
                required: true,
                issues,
                cancellationToken);
        }
    }

    private async Task ValidateResourceImageAsync(
        CreatorId creatorId,
        ResourceId? resourceId,
        string owner,
        bool required,
        ICollection<ContentValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (resourceId is null || resourceId == default)
        {
            if (required)
            {
                AddIssue(
                    creatorId,
                    ContentValidationSeverity.Error,
                    "missing-resource-reference",
                    $"{owner} requires a Creator-owned resource identity.",
                    issues);
            }

            return;
        }

        var publicUrl = await _resourceService.GetPublicUrlAsync(
            creatorId,
            resourceId.Value,
            cancellationToken);
        if (publicUrl is null)
        {
            AddIssue(
                creatorId,
                ContentValidationSeverity.Error,
                "invalid-resource-reference",
                $"{owner} resource '{resourceId}' is missing, unpublished, or owned by another Creator.",
                issues);
            return;
        }

    }

    private static void RegisterQrSlug(
        CreatorId creatorId,
        string slug,
        string destinationSlug,
        IDictionary<string, string> registeredSlugs,
        ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            AddIssue(
                creatorId,
                ContentValidationSeverity.Error,
                "missing-public-qr-slug",
                $"Published destination '{destinationSlug}' requires a QR slug.",
                issues);
            return;
        }

        if (!registeredSlugs.TryAdd(slug, destinationSlug))
        {
            AddIssue(
                creatorId,
                ContentValidationSeverity.Error,
                "duplicate-public-qr-slug",
                $"Public QR slug '{slug}' is registered more than once.",
                issues);
        }
    }

    private static void AddDuplicateIssues<T>(
        CreatorId creatorId,
        IEnumerable<T> values,
        Func<T, string> keySelector,
        string code,
        string label,
        ICollection<ContentValidationIssue> issues)
    {
        foreach (var duplicate in values
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            AddIssue(
                creatorId,
                ContentValidationSeverity.Error,
                code,
                $"{label} '{duplicate.Key}' is registered more than once.",
                issues);
        }
    }

    private static ContentValidationSeverity MissingReferenceSeverity(
        VolumeStatus status) => status is VolumeStatus.Draft or VolumeStatus.Planned
            ? ContentValidationSeverity.Warning
            : ContentValidationSeverity.Error;

    private static string RouteKey(string countrySlug, string destinationSlug) =>
        $"{countrySlug}/{destinationSlug}";

    private static void AddIssue(
        CreatorId creatorId,
        ContentValidationSeverity severity,
        string code,
        string message,
        ICollection<ContentValidationIssue> issues)
    {
        issues.Add(new ContentValidationIssue
        {
            CreatorId = creatorId,
            Severity = severity,
            Code = code,
            Message = message
        });
    }
}
