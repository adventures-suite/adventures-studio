using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies that committed travel content can be loaded and that references
/// remain internally consistent.
/// </summary>
public sealed class JsonTravelContentServiceTests
{
    private static readonly CreatorId FlagshipCreatorId =
        new("creator_tsa_01");

    /// <summary>
    /// Ensures Creator-authored About content loads through the scoped Content
    /// Engine and references the corrected hero asset.
    /// </summary>
    [Fact]
    public async Task GetCreatorProfileAsync_Flagship_LoadsCorrectedAboutContent()
    {
        var service = TestContentServiceFactory.Create();

        var profile = await service.GetCreatorProfileAsync(FlagshipCreatorId);

        Assert.NotNull(profile);
        Assert.Equal("Life is better when we explore it together.", profile.Headline);
        Assert.Equal(
            new TheSimontonAdventures.Web.Resources.ResourceId("resource_about_hero"),
            profile.HeroResourceId);
    }

    /// <summary>
    /// Ensures every committed volume manifest can be deserialized.
    /// </summary>
    [Fact]
    public async Task GetVolumesAsync_LoadsAllCommittedVolumes()
    {
        var service = TestContentServiceFactory.Create();

        var volumes = await service.GetVolumesAsync(FlagshipCreatorId);

        Assert.Equal(3, volumes.Count);
        Assert.Equal([1, 2, 3], volumes.Select(volume => volume.Number));
        Assert.All(volumes, volume => Assert.False(string.IsNullOrWhiteSpace(volume.Slug)));
    }

    /// <summary>
    /// Ensures destination references in current and published volumes resolve
    /// to content whose route identity agrees with the containing manifest.
    /// Planned volumes may contain itinerary placeholders before destination
    /// content has been authored.
    /// </summary>
    [Fact]
    public async Task VolumeDestinationReferences_ResolveToMatchingDestinations()
    {
        var service = TestContentServiceFactory.Create();
        var volumes = await service.GetVolumesAsync(FlagshipCreatorId);

        foreach (var volume in volumes.Where(volume =>
            volume.Status is VolumeStatus.Current or VolumeStatus.Published))
        {
            foreach (var reference in volume.Destinations)
            {
                var destination = await service.GetDestinationAsync(
                    FlagshipCreatorId,
                    volume.Slug,
                    reference.CountrySlug,
                    reference.DestinationSlug);

                Assert.NotNull(destination);
                Assert.Equal(volume.Slug, destination.VolumeSlug, ignoreCase: true);
                Assert.Equal(reference.CountrySlug, destination.CountrySlug, ignoreCase: true);
                Assert.Equal(reference.DestinationSlug, destination.Slug, ignoreCase: true);
            }
        }
    }

    /// <summary>
    /// Ensures every journey reference resolves to content associated with the
    /// expected volume and journey slug.
    /// </summary>
    [Fact]
    public async Task VolumeJourneyReferences_ResolveToMatchingJourneys()
    {
        var service = TestContentServiceFactory.Create();
        var volumes = await service.GetVolumesAsync(FlagshipCreatorId);

        foreach (var volume in volumes)
        {
            foreach (var reference in volume.Journeys)
            {
                var journey = await service.GetJourneyAsync(
                    FlagshipCreatorId,
                    volume.Slug,
                    reference.Slug);

                Assert.NotNull(journey);
                Assert.Equal(volume.Slug, journey.VolumeSlug, ignoreCase: true);
                Assert.Equal(reference.Slug, journey.Slug, ignoreCase: true);
            }
        }
    }

    /// <summary>
    /// Ensures planned Adventures use the Journey Engine itinerary contract
    /// instead of the legacy overview-stop representation.
    /// </summary>
    [Fact]
    public async Task PlannedVolumes_UseJourneyEngineSegments()
    {
        var service = TestContentServiceFactory.Create();
        var volumes = await service.GetVolumesAsync(FlagshipCreatorId);
        var plannedVolumes = volumes
            .Where(volume => volume.Status == VolumeStatus.Planned)
            .ToArray();

        Assert.NotEmpty(plannedVolumes);

        foreach (var volume in plannedVolumes)
        {
            Assert.Empty(volume.JourneyStops);
            var reference = Assert.Single(volume.Journeys);
            Assert.True(reference.Featured);

            var journey = await service.GetJourneyAsync(
                FlagshipCreatorId,
                volume.Slug,
                reference.Slug);

            Assert.NotNull(journey);
            Assert.True(journey.Published);
            Assert.NotEmpty(journey.Segments);
            Assert.All(journey.Segments, segment =>
                Assert.False(string.IsNullOrWhiteSpace(segment.TravelDescription)));
        }
    }

    /// <summary>
    /// Ensures public QR slugs are present and unique without regard to casing.
    /// </summary>
    [Fact]
    public async Task PublishedDestinationQrSlugs_ArePresentAndUnique()
    {
        var service = TestContentServiceFactory.Create();
        var volumes = await service.GetPublicVolumesAsync(FlagshipCreatorId);
        var qrSlugs = new List<string>();

        foreach (var volume in volumes)
        {
            foreach (var reference in volume.Destinations)
            {
                var destination = await service.GetDestinationAsync(
                    FlagshipCreatorId,
                    volume.Slug,
                    reference.CountrySlug,
                    reference.DestinationSlug);

                if (destination is { Published: true })
                {
                    Assert.False(string.IsNullOrWhiteSpace(destination.QrSlug));
                    qrSlugs.Add(destination.QrSlug);
                }
            }
        }

        Assert.Equal(
            qrSlugs.Count,
            qrSlugs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// Verifies current-volume selection and public-volume filtering against
    /// the committed manifests.
    /// </summary>
    [Fact]
    public async Task PublicVolumeQueries_RespectVolumeStatus()
    {
        var service = TestContentServiceFactory.Create();

        var current = await service.GetCurrentVolumeAsync(FlagshipCreatorId);
        var publicVolumes = await service.GetPublicVolumesAsync(FlagshipCreatorId);

        Assert.NotNull(current);
        Assert.Equal(VolumeStatus.Current, current.Status);
        Assert.All(publicVolumes, volume => Assert.True(volume.Status.IsPubliclyVisible()));
        Assert.DoesNotContain(publicVolumes, volume => volume.Status == VolumeStatus.Draft);
    }

    /// <summary>
    /// Ensures aggregate destination loading follows the display order declared
    /// by the containing volume manifest.
    /// </summary>
    [Fact]
    public async Task GetDestinationsForVolumeAsync_ReturnsManifestOrder()
    {
        var service = TestContentServiceFactory.Create();

        var destinations = await service.GetDestinationsForVolumeAsync(
            FlagshipCreatorId,
            "italy-greece-croatia");

        Assert.Equal(
            [
                "venice",
                "florence",
                "tuscany",
                "ravenna",
                "explorer-of-the-seas",
                "dubrovnik",
                "athens",
                "santorini",
                "split"
            ],
            destinations.Select(destination => destination.Slug));
    }

    /// <summary>
    /// Ensures published destination queries do not expose unpublished content.
    /// </summary>
    [Fact]
    public async Task GetPublishedDestinationsForVolumeAsync_ReturnsOnlyPublishedContent()
    {
        var service = TestContentServiceFactory.Create();

        var destinations =
            await service.GetPublishedDestinationsForVolumeAsync(
                FlagshipCreatorId,
                "italy-greece-croatia");

        Assert.NotEmpty(destinations);
        Assert.All(destinations, destination => Assert.True(destination.Published));
    }

    /// <summary>
    /// Ensures featured destinations are published and ordered for homepage
    /// presentation.
    /// </summary>
    [Fact]
    public async Task GetFeaturedDestinationsAsync_ReturnsHomepageOrder()
    {
        var service = TestContentServiceFactory.Create();

        var destinations = await service.GetFeaturedDestinationsAsync(
            FlagshipCreatorId,
            "italy-greece-croatia");

        Assert.NotEmpty(destinations);
        Assert.All(destinations, destination =>
        {
            Assert.True(destination.Published);
            Assert.True(destination.Featured);
        });
        Assert.Equal(
            destinations.OrderBy(destination => destination.HomepageOrder),
            destinations);
    }

    /// <summary>
    /// Ensures an unknown volume produces an empty aggregate result.
    /// </summary>
    [Fact]
    public async Task GetDestinationsForVolumeAsync_UnknownVolume_ReturnsEmptyList()
    {
        var service = TestContentServiceFactory.Create();

        var destinations = await service.GetDestinationsForVolumeAsync(
            FlagshipCreatorId,
            "not-a-real-volume");

        Assert.Empty(destinations);
    }
}
