using System.Text.Json;
using System.Text.Json.Serialization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Services;
using TheSimontonAdventures.Web.Validation;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies that JSON travel content remains confined to its owning Creator.
/// </summary>
public sealed class CreatorScopedTravelContentServiceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Ensures identical route and QR slugs resolve independently per Creator.
    /// </summary>
    [Fact]
    public async Task ContentQueries_IsolateCreatorsWithMatchingSlugs()
    {
        using var content = new TemporaryCreatorContent();
        var firstId = new CreatorId("creator_one_01");
        var secondId = new CreatorId("creator_two_01");
        await content.AddCreatorAsync(firstId, "one", "One's Athens");
        await content.AddCreatorAsync(secondId, "two", "Two's Athens");
        var service = content.CreateService();

        var first = await service.GetDestinationAsync(
            firstId, "shared-volume", "greece", "athens");
        var second = await service.GetDestinationAsync(
            secondId, "shared-volume", "greece", "athens");
        var firstRoute = await service.GetDestinationRouteByQrSlugAsync(
            firstId, "shared-qr");
        var secondRoute = await service.GetDestinationRouteByQrSlugAsync(
            secondId, "shared-qr");

        Assert.Equal("One's Athens", first?.Title);
        Assert.Equal("Two's Athens", second?.Title);
        Assert.NotNull(firstRoute);
        Assert.NotNull(secondRoute);
        Assert.Equal("shared-qr", firstRoute.QrSlug);
        Assert.Equal("shared-qr", secondRoute.QrSlug);
    }

    /// <summary>
    /// Ensures a Creator cannot retrieve content that exists only for another
    /// Creator.
    /// </summary>
    [Fact]
    public async Task ContentQueries_DoNotCrossCreatorBoundaries()
    {
        using var content = new TemporaryCreatorContent();
        var firstId = new CreatorId("creator_one_01");
        var secondId = new CreatorId("creator_two_01");
        await content.AddCreatorAsync(firstId, "one", "One's Athens");
        await content.AddCreatorAsync(secondId, "two", "Two's Athens", "sparta");
        var service = content.CreateService();

        var result = await service.GetDestinationAsync(
            firstId, "shared-volume", "greece", "sparta");

        Assert.Null(result);
    }

    /// <summary>
    /// Ensures traversal-shaped slugs are rejected before filesystem access.
    /// </summary>
    [Theory]
    [InlineData("../athens")]
    [InlineData("athens/secret")]
    [InlineData("Athens")]
    public async Task GetDestinationAsync_InvalidSlug_ThrowsArgumentException(
        string destinationSlug)
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        var service = content.CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetDestinationAsync(
                creatorId,
                "shared-volume",
                "greece",
                destinationSlug));
    }

    /// <summary>
    /// Ensures malformed Creator content is surfaced with its source path.
    /// </summary>
    [Fact]
    public async Task GetVolumesAsync_MalformedManifest_ThrowsInvalidDataException()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        await File.WriteAllTextAsync(
            Path.Combine(content.RootPath, "Content", "one", "Volumes", "Volume-1", "volume.json"),
            "{ invalid json");
        var service = content.CreateService();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.GetVolumesAsync(creatorId));

        Assert.Contains("volume.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Ensures unknown Creator identities expose no content.</summary>
    [Fact]
    public async Task GetVolumesAsync_UnknownCreator_ReturnsEmptyCollection()
    {
        using var content = new TemporaryCreatorContent();
        var service = content.CreateService();

        var result = await service.GetVolumesAsync(
            new CreatorId("creator_missing_01"));

        Assert.Empty(result);
    }

    /// <summary>
    /// Ensures duplicate volume slugs are reported within one Creator index.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_DuplicateVolumeSlug_ReturnsScopedError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.DuplicateVolume("one");
        var validator = content.CreateValidator();

        var result = await validator.ValidateAsync(creatorId);

        var issue = Assert.Single(
            result.Issues,
            issue => issue.Code == "duplicate-volume-slug");
        Assert.Equal(creatorId, issue.CreatorId);
        Assert.Equal(ContentValidationSeverity.Error, issue.Severity);
    }

    /// <summary>
    /// Ensures a published volume's broken destination reference blocks startup.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_MissingPublishedDestination_ReturnsError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.DeleteDestination("one", "athens");
        var validator = content.CreateValidator();

        var result = await validator.ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue =>
            issue.CreatorId == creatorId
            && issue.Code == "missing-destination-reference"
            && issue.Severity == ContentValidationSeverity.Error);
    }

    /// <summary>Ensures missing optional local media remains an observable warning.</summary>
    [Fact]
    public async Task ValidateAsync_MissingLocalImage_ReturnsWarning()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(
            creatorId,
            "one",
            "Athens",
            homepageImage: "/images/missing.jpg");
        var validator = content.CreateValidator();

        var result = await validator.ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue =>
            issue.CreatorId == creatorId
            && issue.Code == "missing-image"
            && issue.Severity == ContentValidationSeverity.Warning);
        Assert.False(result.HasErrors);
    }

    private sealed class TemporaryCreatorContent : IDisposable
    {
        internal TemporaryCreatorContent()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                $"adventures-content-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
        }

        internal string RootPath { get; }

        internal async Task AddCreatorAsync(
            CreatorId id,
            string slug,
            string title,
            string destinationSlug = "athens",
            string homepageImage = "")
        {
            var creatorDirectory = Path.Combine(
                RootPath, "Content", "Creators", slug);
            var volumeDirectory = Path.Combine(
                RootPath, "Content", slug, "Volumes", "Volume-1");
            var destinationsDirectory = Path.Combine(
                volumeDirectory, "destinations");
            Directory.CreateDirectory(creatorDirectory);
            Directory.CreateDirectory(destinationsDirectory);

            var creator = new Creator
            {
                Id = id,
                Slug = slug,
                DisplayName = title,
                Status = CreatorStatus.Active,
                PrimaryDomain = $"{slug}.example.test",
                Domains = [$"{slug}.example.test"],
                Brand = new CreatorBrand
                {
                    SiteName = title,
                    Tagline = "Creator-scoped test content",
                    HomeHeroImageUrl = "https://example.test/hero.jpg",
                    HomeHeroImageAlt = "A test journey",
                    HomeHeroHeadline = "An independently owned journey",
                    HomeHeroDescription = "Creator-owned integration test copy.",
                    HomeHeroActionLabel = "Explore"
                },
                ContentRoot = $"Content/{slug}/Volumes"
            };
            var volume = new Volume
            {
                Number = 1,
                Slug = "shared-volume",
                Title = "Shared volume",
                Status = VolumeStatus.Published,
                Destinations =
                [
                    new VolumeDestinationReference
                    {
                        CountrySlug = "greece",
                        DestinationSlug = destinationSlug,
                        DisplayOrder = 1
                    }
                ]
            };
            var destination = new Destination
            {
                VolumeSlug = "shared-volume",
                Country = "Greece",
                CountrySlug = "greece",
                Slug = destinationSlug,
                QrSlug = "shared-qr",
                Title = title,
                HomepageImage = homepageImage,
                Published = true
            };

            await WriteJsonAsync(
                Path.Combine(creatorDirectory, "creator.json"),
                creator);
            await WriteJsonAsync(
                Path.Combine(volumeDirectory, "volume.json"),
                volume);
            await WriteJsonAsync(
                Path.Combine(destinationsDirectory, $"{destinationSlug}.json"),
                destination);
        }

        internal JsonTravelContentService CreateService()
        {
            var environment = TestContentServiceFactory.CreateHostEnvironment(
                contentRootPath: RootPath);
            return new JsonTravelContentService(
                environment,
                new JsonCreatorService(environment));
        }

        internal CreatorContentValidator CreateValidator()
        {
            var environment = TestContentServiceFactory.CreateHostEnvironment(
                contentRootPath: RootPath);
            var creatorService = new JsonCreatorService(environment);
            return new CreatorContentValidator(
                environment,
                creatorService,
                new JsonTravelContentService(environment, creatorService));
        }

        internal void DeleteDestination(string creatorSlug, string destinationSlug)
        {
            File.Delete(Path.Combine(
                RootPath,
                "Content",
                creatorSlug,
                "Volumes",
                "Volume-1",
                "destinations",
                $"{destinationSlug}.json"));
        }

        internal void DuplicateVolume(string creatorSlug)
        {
            var source = Path.Combine(
                RootPath,
                "Content",
                creatorSlug,
                "Volumes",
                "Volume-1");
            var destination = Path.Combine(
                RootPath,
                "Content",
                creatorSlug,
                "Volumes",
                "Volume-2");
            Directory.CreateDirectory(destination);
            File.Copy(
                Path.Combine(source, "volume.json"),
                Path.Combine(destination, "volume.json"));
        }

        public void Dispose() => Directory.Delete(RootPath, recursive: true);

        private static Task WriteJsonAsync<T>(string path, T value) =>
            File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(value, SerializerOptions));
    }
}
