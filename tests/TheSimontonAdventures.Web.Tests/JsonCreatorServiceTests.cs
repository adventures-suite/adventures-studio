using System.Text.Json;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies JSON-backed Creator retrieval and cross-manifest uniqueness rules.
/// </summary>
public sealed class JsonCreatorServiceTests
{
    /// <summary>Ensures the immutable registry exposes the flagship Creator.</summary>
    [Fact]
    public async Task GetAllAsync_ReturnsValidatedCreatorSnapshot()
    {
        var service = new JsonCreatorService(
            TestContentServiceFactory.CreateHostEnvironment());

        var creators = await service.GetAllAsync();

        Assert.Equal(2, creators.Count);
        Assert.Contains(creators, creator =>
            creator.Id == new CreatorId("creator_tsa_01"));
        Assert.Contains(creators, creator =>
            creator.Id == new CreatorId("creator_demo_01")
            && creator.DevelopmentOnly);
    }

    /// <summary>Ensures the flagship Creator can be retrieved by stable identity.</summary>
    [Fact]
    public async Task GetByIdAsync_FlagshipIdentity_ReturnsCreator()
    {
        var service = new JsonCreatorService(
            TestContentServiceFactory.CreateHostEnvironment());

        var creator = await service.GetByIdAsync(
            new CreatorId("creator_tsa_01"));

        Assert.NotNull(creator);
        Assert.Equal("the-simonton-adventures", creator.Slug);
        Assert.Equal(CreatorStatus.Active, creator.Status);
    }

    /// <summary>
    /// Ensures approved host lookup is case-insensitive and tolerates the DNS
    /// trailing-dot form.
    /// </summary>
    [Theory]
    [InlineData("thesimontonadventures.com")]
    [InlineData("THESIMONTONADVENTURES.COM")]
    [InlineData("www.thesimontonadventures.com.")]
    public async Task GetByHostAsync_ApprovedHost_ReturnsActiveCreator(string host)
    {
        var service = new JsonCreatorService(
            TestContentServiceFactory.CreateHostEnvironment());

        var creator = await service.GetByHostAsync(host);

        Assert.NotNull(creator);
        Assert.Equal(new CreatorId("creator_tsa_01"), creator.Id);
    }

    /// <summary>Ensures malformed and unknown hosts do not select a Creator.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("https://thesimontonadventures.com")]
    [InlineData("unknown.example.com")]
    public async Task GetByHostAsync_UnapprovedHost_ReturnsNull(string host)
    {
        var service = new JsonCreatorService(
            TestContentServiceFactory.CreateHostEnvironment());

        var creator = await service.GetByHostAsync(host);

        Assert.Null(creator);
    }

    /// <summary>
    /// Ensures a development-only Creator cannot resolve by its registered
    /// domain when the application is running in Production.
    /// </summary>
    [Fact]
    public async Task GetByHostAsync_DevelopmentOnlyCreatorInProduction_ReturnsNull()
    {
        var service = new JsonCreatorService(
            TestContentServiceFactory.CreateHostEnvironment(
                Environments.Production));

        var creator = await service.GetByHostAsync(
            "demo.adventuressuite.test");

        Assert.Null(creator);
    }

    /// <summary>
    /// Ensures two manifests cannot register the same normalized domain, which
    /// would make host resolution ambiguous.
    /// </summary>
    [Fact]
    public async Task GetByHostAsync_DomainRegisteredTwice_ThrowsInvalidDataException()
    {
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"adventures-creator-tests-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(temporaryRoot, "Content", "Volumes"));
            await WriteCreatorManifestAsync(
                temporaryRoot,
                "creator-one",
                CreateCreator(
                    "creator_one_01",
                    "creator-one",
                    "shared.example.com"));
            await WriteCreatorManifestAsync(
                temporaryRoot,
                "creator-two",
                CreateCreator(
                    "creator_two_01",
                    "creator-two",
                    "SHARED.EXAMPLE.COM."));

            var service = new JsonCreatorService(
                TestContentServiceFactory.CreateHostEnvironment(
                    contentRootPath: temporaryRoot));

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.GetByHostAsync("shared.example.com"));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// Ensures malformed Creator JSON produces an observable file-specific
    /// failure rather than silently behaving like missing content.
    /// </summary>
    [Fact]
    public async Task GetByHostAsync_MalformedManifest_ThrowsInvalidDataException()
    {
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"adventures-creator-tests-{Guid.NewGuid():N}");

        try
        {
            var creatorDirectory = Path.Combine(
                temporaryRoot,
                "Content",
                "Creators",
                "broken");
            Directory.CreateDirectory(creatorDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(creatorDirectory, "creator.json"),
                "{ not valid JSON }");

            var service = new JsonCreatorService(
                TestContentServiceFactory.CreateHostEnvironment(
                    contentRootPath: temporaryRoot));

            var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.GetByHostAsync("example.com"));

            Assert.Contains("creator.json", exception.Message);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static Creator CreateCreator(
        string creatorId,
        string slug,
        string domain)
    {
        return new Creator
        {
            Id = new CreatorId(creatorId),
            Slug = slug,
            DisplayName = slug,
            Status = CreatorStatus.Active,
            PrimaryDomain = domain,
            Domains = [domain],
            ContentRoot = "Content/Volumes"
        };
    }

    private static async Task WriteCreatorManifestAsync(
        string root,
        string directoryName,
        Creator creator)
    {
        var creatorDirectory = Path.Combine(
            root,
            "Content",
            "Creators",
            directoryName);
        Directory.CreateDirectory(creatorDirectory);

        var json = JsonSerializer.Serialize(
            creator,
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(
            Path.Combine(creatorDirectory, "creator.json"),
            json);
    }
}
