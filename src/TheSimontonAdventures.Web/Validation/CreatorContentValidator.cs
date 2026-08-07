using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Services;

namespace TheSimontonAdventures.Web.Validation;

/// <summary>
/// Validates JSON content references, publication rules, public slugs, and
/// local media without crossing Creator ownership boundaries.
/// </summary>
public sealed class CreatorContentValidator : ICreatorContentValidator
{
    private readonly string _webRoot;
    private readonly ICreatorService _creatorService;
    private readonly ITravelContentService _contentService;

    /// <summary>Initializes Creator-scoped deployed-content validation.</summary>
    /// <param name="hostEnvironment">The application host environment.</param>
    /// <param name="creatorService">The Creator registry.</param>
    /// <param name="contentService">The Creator-scoped Content Engine.</param>
    public CreatorContentValidator(
        IHostEnvironment hostEnvironment,
        ICreatorService creatorService,
        ITravelContentService contentService)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(creatorService);
        ArgumentNullException.ThrowIfNull(contentService);

        _webRoot = Path.Combine(hostEnvironment.ContentRootPath, "wwwroot");
        _creatorService = creatorService;
        _contentService = contentService;
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

        ValidateImage(creatorId, creator.Brand.LogoUrl, "Creator logo", issues);
        ValidateImage(creatorId, creator.Brand.FaviconUrl, "Creator favicon", issues);
        ValidateImage(
            creatorId,
            creator.Brand.HomeHeroImageUrl,
            "Creator homepage hero",
            issues);
        var profile = await _contentService.GetCreatorProfileAsync(
            creatorId,
            cancellationToken);
        ValidateImage(
            creatorId,
            profile?.HeroImageUrl,
            "Creator About hero",
            issues);

        var publicQrSlugs = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var volume in volumes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateImage(creatorId, volume.CoverImage, $"Volume '{volume.Slug}' cover", issues);
            ValidateImage(creatorId, volume.HeroImage, $"Volume '{volume.Slug}' hero", issues);
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
                ValidateDestinationImages(creatorId, destination, issues);

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

    private void ValidateDestinationImages(
        CreatorId creatorId,
        Destination destination,
        ICollection<ContentValidationIssue> issues)
    {
        ValidateImage(creatorId, destination.HeroImage, $"Destination '{destination.Slug}' hero", issues);
        ValidateImage(creatorId, destination.HomepageImage, $"Destination '{destination.Slug}' homepage", issues);

        foreach (var section in destination.Sections)
        {
            ValidateImage(creatorId, section.ImageSrc, $"Destination '{destination.Slug}' section", issues);
        }

        foreach (var image in destination.Gallery)
        {
            ValidateImage(creatorId, image.Src, $"Destination '{destination.Slug}' gallery", issues);
        }
    }

    private void ValidateImage(
        CreatorId creatorId,
        string? imageUrl,
        string owner,
        ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absoluteUri)
            && absoluteUri.Scheme is "http" or "https")
        {
            return;
        }

        if (!imageUrl.StartsWith("/", StringComparison.Ordinal)
            || imageUrl.Contains("..", StringComparison.Ordinal))
        {
            AddIssue(
                creatorId,
                ContentValidationSeverity.Error,
                "invalid-image-path",
                $"{owner} image '{imageUrl}' must be root-relative and non-traversing.",
                issues);
            return;
        }

        var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var resolvedPath = Path.GetFullPath(Path.Combine(_webRoot, relativePath));
        var webRootPrefix = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(_webRoot)) + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(webRootPrefix, PathComparison)
            || !File.Exists(resolvedPath))
        {
            AddIssue(
                creatorId,
                ContentValidationSeverity.Warning,
                "missing-image",
                $"{owner} image '{imageUrl}' was not found.",
                issues);
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

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
