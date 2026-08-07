using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Resources;

namespace TheSimontonAdventures.Web.Tests;

public sealed class ResourceEngineTests
{
    [Fact]
    public async Task SameResourceIdentityResolvesWithinEachCreatorBoundary()
    {
        var service = CreateService();

        var flagship = await service.GetByIdAsync(
            new CreatorId("creator_tsa_01"),
            new ResourceId("resource_home_hero"));
        var demo = await service.GetByIdAsync(
            new CreatorId("creator_demo_01"),
            new ResourceId("resource_home_hero"));

        Assert.NotNull(flagship);
        Assert.NotNull(demo);
        Assert.NotEqual(flagship.StorageLocation, demo.StorageLocation);
        Assert.Equal(new CreatorId("creator_tsa_01"), flagship.CreatorId);
        Assert.Equal(new CreatorId("creator_demo_01"), demo.CreatorId);
    }

    [Fact]
    public async Task UnknownCreatorOwnedReferenceDoesNotCrossTenantBoundary()
    {
        var service = CreateService();

        var result = await service.GetByIdAsync(
            new CreatorId("creator_demo_01"),
            new ResourceId("resource_volume_one_cover"));

        Assert.Null(result);
    }

    [Fact]
    public async Task PublishedReferenceResolvesThroughStorageProvider()
    {
        var service = CreateService();

        var url = await service.GetPublicUrlAsync(
            new CreatorId("creator_tsa_01"),
            new ResourceId("resource_home_hero"));

        Assert.Equal("/images/home/adventures-studio-hero.jpeg", url);
    }

    [Fact]
    public async Task ResolvedResourceIncludesAuthoritativeAccessibilityMetadata()
    {
        var resolved = await CreateService().ResolvePublicAsync(
            new CreatorId("creator_tsa_01"),
            new ResourceId("resource_venice_hero"));

        Assert.NotNull(resolved);
        Assert.Equal("A canal in Venice", resolved.Resource.AlternativeText);
        Assert.Equal("image/jpeg", resolved.Resource.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(resolved.Resource.UsageRights));
    }

    [Fact]
    public void ContentManifestsContainNoRawPresentationImageFields()
    {
        var contentRoot = Path.Combine(FindApplicationRoot(), "Content");
        var forbidden = new[]
        {
            "\"heroImage\"", "\"homepageImage\"", "\"coverImage\"",
            "\"imageSrc\"", "\"heroImageUrl\"", "\"homeHeroImageUrl\""
        };

        foreach (var path in Directory.EnumerateFiles(contentRoot, "*.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Resources{Path.DirectorySeparatorChar}")))
        {
            var json = File.ReadAllText(path);
            Assert.DoesNotContain(forbidden, field => json.Contains(field, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("Resource-1")]
    [InlineData("1_resource")]
    [InlineData("resource/path")]
    public void ResourceIdentityRejectsUnstableValues(string value) =>
        Assert.Throws<ArgumentException>(() => new ResourceId(value));

    private static JsonResourceService CreateService()
    {
        var contentRoot = FindApplicationRoot();
        var environment = new TestWebHostEnvironment(contentRoot);
        var creatorService = new JsonCreatorService(environment);
        var provider = new LocalPublicResourceProvider(environment);
        return new JsonResourceService(creatorService, [provider], environment);
    }

    private static string FindApplicationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "TheSimontonAdventures.Web");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the application content root.");
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "TheSimontonAdventures.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string WebRootPath { get; set; } = Path.Combine(contentRootPath, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(Path.Combine(contentRootPath, "wwwroot"));
    }
}
