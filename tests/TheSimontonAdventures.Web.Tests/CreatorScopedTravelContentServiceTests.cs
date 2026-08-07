using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Resources;
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

    /// <summary>Ensures a valid IANA destination time zone passes validation.</summary>
    [Fact]
    public async Task ValidateAsync_ValidIanaTimeZone_ReturnsNoTimeZoneError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.UpdateDestination("one", "athens", manifest =>
            manifest["timeZone"] = "Europe/Athens");

        var result = await content.CreateValidator().ValidateAsync(creatorId);

        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code == "invalid-destination-time-zone");
    }

    /// <summary>Ensures an invalid zone is not replaced with Creator defaults.</summary>
    [Fact]
    public async Task ValidateAsync_InvalidTimeZone_ReturnsScopedError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.UpdateDestination("one", "athens", manifest =>
            manifest["timeZone"] = "Mars/Olympus_Mons");

        var result = await content.CreateValidator().ValidateAsync(creatorId);

        var issue = Assert.Single(result.Issues, issue =>
            issue.Code == "invalid-destination-time-zone");
        Assert.Equal(creatorId, issue.CreatorId);
        Assert.Contains("greece/athens", issue.Message);
    }

    /// <summary>Ensures reversed planned and visited ranges are rejected.</summary>
    [Theory]
    [InlineData("plannedArrivalDate", "plannedDepartureDate", "reversed-planned-date-range")]
    [InlineData("visitedFrom", "visitedTo", "reversed-visited-date-range")]
    public async Task ValidateAsync_ReversedDateRange_ReturnsError(
        string fromProperty,
        string toProperty,
        string expectedCode)
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.UpdateDestination("one", "athens", manifest =>
        {
            manifest[fromProperty] = "2027-10-29";
            manifest[toProperty] = "2027-10-25";
        });

        var result = await content.CreateValidator().ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    /// <summary>Ensures either half of a date range cannot be authored alone.</summary>
    [Theory]
    [InlineData("plannedArrivalDate", "incomplete-planned-date-range")]
    [InlineData("plannedDepartureDate", "incomplete-planned-date-range")]
    [InlineData("visitedFrom", "incomplete-visited-date-range")]
    [InlineData("visitedTo", "incomplete-visited-date-range")]
    public async Task ValidateAsync_IncompleteDateRange_ReturnsError(
        string propertyName,
        string expectedCode)
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.UpdateDestination("one", "athens", manifest =>
            manifest[propertyName] = "2027-10-25");

        var result = await content.CreateValidator().ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    /// <summary>Ensures every lifecycle timestamp requires a zero UTC offset.</summary>
    [Theory]
    [InlineData("createdAtUtc")]
    [InlineData("updatedAtUtc")]
    [InlineData("publishedAtUtc")]
    [InlineData("lastPublishedAtUtc")]
    public async Task ValidateAsync_NonUtcLifecycleTimestamp_ReturnsError(
        string propertyName)
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.UpdateDestination("one", "athens", manifest =>
        {
            manifest["publishedAtUtc"] = "2026-08-07T18:30:00Z";
            manifest[propertyName] = "2026-08-07T18:30:00-07:00";
        });

        var result = await content.CreateValidator().ValidateAsync(creatorId);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "non-utc-content-timestamp"
                && issue.Message.Contains(propertyName));
    }

    /// <summary>Ensures authored and publication timestamp ordering is enforced.</summary>
    [Theory]
    [InlineData("updatedAtUtc", "createdAtUtc")]
    [InlineData("publishedAtUtc", "createdAtUtc")]
    [InlineData("lastPublishedAtUtc", "publishedAtUtc")]
    public async Task ValidateAsync_ReversedLifecycleTimestamps_ReturnsError(
        string laterProperty,
        string earlierProperty)
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.UpdateDestination("one", "athens", manifest =>
        {
            manifest[earlierProperty] = "2026-08-08T18:30:00Z";
            manifest[laterProperty] = "2026-08-07T18:30:00Z";
        });

        var result = await content.CreateValidator().ValidateAsync(creatorId);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "invalid-content-timestamp-order"
                && issue.Message.Contains(laterProperty));
    }

    /// <summary>Ensures latest publication metadata requires first publication.</summary>
    [Fact]
    public async Task ValidateAsync_LastPublishedWithoutPublished_ReturnsError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.UpdateDestination("one", "athens", manifest =>
            manifest["lastPublishedAtUtc"] = "2026-08-07T18:30:00Z");

        var result = await content.CreateValidator().ValidateAsync(creatorId);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "missing-first-publication-timestamp");
    }

    /// <summary>Ensures a valid typed port call is loaded and accepted.</summary>
    [Fact]
    public async Task ValidateAsync_ValidJourneyVisitSchedule_ReturnsNoScheduleErrors()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.AddJourneySchedule("one");

        var journey = await content.CreateService().GetJourneyAsync(
            creatorId,
            "shared-volume",
            "test-journey");
        var result = await content.CreateValidator().ValidateAsync(creatorId);

        Assert.Equal(
            new TimeOnly(8, 0),
            journey?.Segments.Single().VisitSchedule?.PlannedGangwayDownTime);
        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code.Contains("visit", StringComparison.Ordinal)
                || issue.Code.Contains("gangway", StringComparison.Ordinal));
    }

    /// <summary>Ensures visit schedules require a valid IANA time zone.</summary>
    [Fact]
    public async Task ValidateAsync_InvalidVisitTimeZone_ReturnsScopedError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.AddJourneySchedule("one", schedule =>
            schedule["timeZone"] = "Ship/Imaginary");

        var result = await content.CreateValidator().ValidateAsync(creatorId);

        var issue = Assert.Single(
            result.Issues,
            issue => issue.Code == "invalid-visit-time-zone");
        Assert.Equal(creatorId, issue.CreatorId);
        Assert.Contains("test-journey", issue.Message);
    }

    /// <summary>Ensures typed visit dates are complete and ordered.</summary>
    [Theory]
    [InlineData("plannedDepartureDate", null, "incomplete-visit-date-range")]
    [InlineData("plannedDepartureDate", "2027-05-19", "reversed-visit-date-range")]
    public async Task ValidateAsync_InvalidVisitDates_ReturnsError(
        string propertyName,
        string? value,
        string expectedCode)
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.AddJourneySchedule("one", schedule =>
            schedule[propertyName] = value);

        var result = await content.CreateValidator().ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    /// <summary>Ensures gangway operations remain within arrival and departure.</summary>
    [Theory]
    [InlineData("plannedGangwayUpTime", null, "incomplete-gangway-window")]
    [InlineData("plannedGangwayDownTime", "06:00:00", "gangway-before-arrival")]
    [InlineData("plannedGangwayUpTime", "07:30:00", "reversed-gangway-window")]
    [InlineData("plannedGangwayUpTime", "19:00:00", "gangway-after-departure")]
    public async Task ValidateAsync_InvalidGangwayWindow_ReturnsError(
        string propertyName,
        string? value,
        string expectedCode)
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        content.AddJourneySchedule("one", schedule =>
            schedule[propertyName] = value);

        var result = await content.CreateValidator().ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    /// <summary>Ensures a missing section resource blocks startup.</summary>
    [Fact]
    public async Task ValidateAsync_MissingSectionResource_ReturnsError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(
            creatorId,
            "one",
            "Athens",
            homepageImage: "/images/missing.jpg");
        var validator = content.CreateValidator(new StubResourceService(false));

        var result = await validator.ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue =>
            issue.CreatorId == creatorId
            && issue.Code == "invalid-resource-reference"
            && issue.Message.Contains("section", StringComparison.OrdinalIgnoreCase)
            && issue.Severity == ContentValidationSeverity.Error);
        Assert.True(result.HasErrors);
    }

    /// <summary>Ensures a missing homepage resource blocks Creator publication.</summary>
    [Fact]
    public async Task ValidateAsync_MissingHomepageResource_ReturnsError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        var validator = content.CreateValidator(new StubResourceService(false));

        var result = await validator.ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "invalid-homepage-hero-resource"
            && issue.Severity == ContentValidationSeverity.Error);
    }

    /// <summary>Ensures a draft homepage resource cannot enter public presentation.</summary>
    [Fact]
    public async Task ValidateAsync_DraftHomepageResource_ReturnsError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        var resources = new StubResourceService(false);
        resources.Add(CreateHeroResource(creatorId, ResourcePublicationStatus.Draft));
        var validator = content.CreateValidator(resources);

        var result = await validator.ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "invalid-homepage-hero-resource"
            && issue.Severity == ContentValidationSeverity.Error);
    }

    /// <summary>Ensures another Creator's hero cannot satisfy the current Creator's reference.</summary>
    [Fact]
    public async Task ValidateAsync_CrossCreatorHomepageResource_ReturnsError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        var resources = new StubResourceService(false);
        resources.Add(CreateHeroResource(
            new CreatorId("creator_two_01"),
            ResourcePublicationStatus.Published));
        var validator = content.CreateValidator(resources);

        var result = await validator.ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "invalid-homepage-hero-resource"
            && issue.CreatorId == creatorId);
    }

    /// <summary>Ensures a missing volume cover resource blocks publication.</summary>
    [Fact]
    public async Task ValidateAsync_MissingVolumeCoverResource_ReturnsError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        var resources = CreateResourcesWithPublishedHomepage(creatorId);
        var validator = content.CreateValidator(resources);

        var result = await validator.ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "invalid-resource-reference"
            && issue.Message.Contains("cover", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Ensures a draft volume cover cannot enter public presentation.</summary>
    [Fact]
    public async Task ValidateAsync_DraftVolumeCoverResource_ReturnsError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        var resources = CreateResourcesWithPublishedHomepage(creatorId);
        resources.Add(CreateResource(
            creatorId,
            "resource_volume_cover",
            ResourcePublicationStatus.Draft));
        var validator = content.CreateValidator(resources);

        var result = await validator.ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "invalid-resource-reference"
            && issue.Message.Contains("cover", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Ensures another Creator's volume cover cannot satisfy a reference.</summary>
    [Fact]
    public async Task ValidateAsync_CrossCreatorVolumeCoverResource_ReturnsError()
    {
        using var content = new TemporaryCreatorContent();
        var creatorId = new CreatorId("creator_one_01");
        await content.AddCreatorAsync(creatorId, "one", "Athens");
        var resources = CreateResourcesWithPublishedHomepage(creatorId);
        resources.Add(CreateResource(
            new CreatorId("creator_two_01"),
            "resource_volume_cover",
            ResourcePublicationStatus.Published));
        var validator = content.CreateValidator(resources);

        var result = await validator.ValidateAsync(creatorId);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "invalid-resource-reference"
            && issue.CreatorId == creatorId);
    }

    private static StubResourceService CreateResourcesWithPublishedHomepage(
        CreatorId creatorId)
    {
        var resources = new StubResourceService(false);
        resources.Add(CreateResource(
            creatorId,
            "resource_home_hero",
            ResourcePublicationStatus.Published));
        return resources;
    }

    private static ResourceRecord CreateHeroResource(
        CreatorId creatorId,
        ResourcePublicationStatus status) =>
        CreateResource(creatorId, "resource_home_hero", status);

    private static ResourceRecord CreateResource(
        CreatorId creatorId,
        string resourceId,
        ResourcePublicationStatus status) => new()
        {
            Id = new ResourceId(resourceId),
            CreatorId = creatorId,
            Type = ResourceType.Image,
            Title = "Test homepage hero",
            StorageProvider = "test",
            StorageLocation = "/images/test-hero.jpeg",
            MediaType = "image/jpeg",
            AlternativeText = "A test journey",
            Attribution = "Test Creator",
            Copyright = "Copyright Test Creator",
            UsageRights = "Test use",
            PublicationStatus = status
        };

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
                    FaviconResourceId = new ResourceId("resource_favicon"),
                    HomeHeroResourceId = new ResourceId("resource_home_hero"),
                    HomeHeroHeadline = "An independently owned journey",
                    HomeHeroDescription = "Creator-owned integration test copy.",
                    HomeHeroActionLabel = "Explore"
                },
                Homepage = new CreatorHomepage
                {
                    Sections = [CreatorHomepageSectionType.CurrentAdventure]
                },
                ContentRoot = $"Content/{slug}/Volumes"
            };
            var volume = new Volume
            {
                Number = 1,
                Slug = "shared-volume",
                Title = "Shared volume",
                Status = VolumeStatus.Published,
                CoverResourceId = new ResourceId("resource_volume_cover"),
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
                HeroResourceId = new ResourceId("resource_destination_hero"),
                HomepageResourceId = new ResourceId("resource_destination_card"),
                Sections = string.IsNullOrWhiteSpace(homepageImage)
                    ? []
                    :
                    [
                        new DestinationSection
                        {
                            Heading = "Test section",
                            ImageResourceId = new ResourceId("resource_missing_section")
                        }
                    ],
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

        internal CreatorContentValidator CreateValidator(
            IResourceService? resourceService = null)
        {
            var environment = TestContentServiceFactory.CreateHostEnvironment(
                contentRootPath: RootPath);
            var creatorService = new JsonCreatorService(environment);
            return new CreatorContentValidator(
                environment,
                creatorService,
                new JsonTravelContentService(environment, creatorService),
                resourceService ?? new StubResourceService());
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

        internal void UpdateDestination(
            string creatorSlug,
            string destinationSlug,
            Action<JsonObject> update)
        {
            var path = Path.Combine(
                RootPath,
                "Content",
                creatorSlug,
                "Volumes",
                "Volume-1",
                "destinations",
                $"{destinationSlug}.json");
            var manifest = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                ?? throw new InvalidDataException(
                    $"Destination fixture '{path}' is not a JSON object.");

            update(manifest);
            File.WriteAllText(
                path,
                manifest.ToJsonString(SerializerOptions));
        }

        internal void AddJourneySchedule(
            string creatorSlug,
            Action<JsonObject>? update = null)
        {
            var volumeDirectory = Path.Combine(
                RootPath,
                "Content",
                creatorSlug,
                "Volumes",
                "Volume-1");
            var volumePath = Path.Combine(volumeDirectory, "volume.json");
            var volume = JsonNode.Parse(File.ReadAllText(volumePath))?.AsObject()
                ?? throw new InvalidDataException(
                    $"Volume fixture '{volumePath}' is not a JSON object.");
            volume["journeys"] = new JsonArray
            {
                new JsonObject
                {
                    ["slug"] = "test-journey",
                    ["title"] = "Test journey",
                    ["journeyType"] = "Editorial",
                    ["featured"] = true,
                    ["displayOrder"] = 1
                }
            };
            File.WriteAllText(
                volumePath,
                volume.ToJsonString(SerializerOptions));

            var schedule = new JsonObject
            {
                ["timeZone"] = "America/St_Thomas",
                ["plannedArrivalDate"] = "2027-05-20",
                ["plannedArrivalTime"] = "07:00:00",
                ["plannedGangwayDownTime"] = "08:00:00",
                ["plannedGangwayUpTime"] = "17:00:00",
                ["plannedDepartureDate"] = "2027-05-20",
                ["plannedDepartureTime"] = "18:00:00"
            };
            update?.Invoke(schedule);

            var journey = new JsonObject
            {
                ["slug"] = "test-journey",
                ["volumeSlug"] = "shared-volume",
                ["title"] = "Test journey",
                ["journeyType"] = "Editorial",
                ["published"] = true,
                ["displayOrder"] = 1,
                ["segments"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["from"] = "Miami",
                        ["to"] = "Charlotte Amalie",
                        ["travelMode"] = "Cruise",
                        ["countrySlug"] = "greece",
                        ["destinationSlug"] = "athens",
                        ["displayOrder"] = 1,
                        ["visitSchedule"] = schedule
                    }
                }
            };
            var journeyDirectory = Path.Combine(volumeDirectory, "journeys");
            Directory.CreateDirectory(journeyDirectory);
            File.WriteAllText(
                Path.Combine(journeyDirectory, "test-journey.json"),
                journey.ToJsonString(SerializerOptions));
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
