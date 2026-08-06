using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies that committed travel content can be loaded and that references
/// remain internally consistent.
/// </summary>
public sealed class JsonTravelContentServiceTests
{
    /// <summary>
    /// Ensures every committed volume manifest can be deserialized.
    /// </summary>
    [Fact]
    public async Task GetVolumesAsync_LoadsAllCommittedVolumes()
    {
        var service = TestContentServiceFactory.Create();

        var volumes = await service.GetVolumesAsync();

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
        var volumes = await service.GetVolumesAsync();

        foreach (var volume in volumes.Where(volume =>
            volume.Status is VolumeStatus.Current or VolumeStatus.Published))
        {
            foreach (var reference in volume.Destinations)
            {
                var destination = await service.GetDestinationAsync(
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
        var volumes = await service.GetVolumesAsync();

        foreach (var volume in volumes)
        {
            foreach (var reference in volume.Journeys)
            {
                var journey = await service.GetJourneyAsync(
                    volume.Slug,
                    reference.Slug);

                Assert.NotNull(journey);
                Assert.Equal(volume.Slug, journey.VolumeSlug, ignoreCase: true);
                Assert.Equal(reference.Slug, journey.Slug, ignoreCase: true);
            }
        }
    }

    /// <summary>
    /// Ensures public QR slugs are present and unique without regard to casing.
    /// </summary>
    [Fact]
    public async Task PublishedDestinationQrSlugs_ArePresentAndUnique()
    {
        var service = TestContentServiceFactory.Create();
        var volumes = await service.GetPublicVolumesAsync();
        var qrSlugs = new List<string>();

        foreach (var volume in volumes)
        {
            foreach (var reference in volume.Destinations)
            {
                var destination = await service.GetDestinationAsync(
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

        var current = await service.GetCurrentVolumeAsync();
        var publicVolumes = await service.GetPublicVolumesAsync();

        Assert.NotNull(current);
        Assert.Equal(VolumeStatus.Current, current.Status);
        Assert.All(publicVolumes, volume => Assert.True(volume.Status.IsPubliclyVisible()));
        Assert.DoesNotContain(publicVolumes, volume => volume.Status == VolumeStatus.Draft);
    }
}
