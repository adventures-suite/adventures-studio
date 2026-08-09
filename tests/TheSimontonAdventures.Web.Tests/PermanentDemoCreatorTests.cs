using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Services;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies the permanent browser-visible development Creator fixture.
/// </summary>
public sealed class PermanentDemoCreatorTests
{
    /// <summary>
    /// Ensures the explicit local alias selects the demo Creator and its
    /// independently owned current content.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_DemoLocalhost_ReturnsDemoContent()
    {
        var environment = TestContentServiceFactory.CreateHostEnvironment();
        var creatorService = new JsonCreatorService(environment);
        var resolver = new CreatorResolver(
            creatorService,
            environment,
            Options.Create(new CreatorResolutionOptions
            {
                DevelopmentAliases = new Dictionary<string, string>
                {
                    ["demo.localhost"] = "creator_demo_01"
                }
            }),
            new ConfigurationBuilder().Build());
        var contentService = new JsonTravelContentService(
            environment,
            creatorService);

        var context = await resolver.ResolveAsync(
            new HostString("demo.localhost", 5018));
        var volume = await contentService.GetCurrentVolumeAsync(context!.Id);
        var destination = await contentService.GetDestinationAsync(
            context.Id,
            "demo-aegean-notebook",
            "greece",
            "athens");

        Assert.Equal(new CreatorId("creator_demo_01"), context.Id);
        Assert.Equal("Aegean Field Notes", context.Brand.SiteName);
        Assert.False(context.Features.EnableAbout);
        Assert.False(context.Features.EnableCompanion);
        Assert.Equal("Aegean Notebook", volume?.Title);
        Assert.Equal("Athens Field Notes", destination?.Title);
    }

    /// <summary>
    /// Ensures the same public slug resolves to separate targets for the
    /// flagship and demo Creators.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_SharedAthensSlug_ReturnsCreatorOwnedTargets()
    {
        var environment = TestContentServiceFactory.CreateHostEnvironment();
        var creatorService = new JsonCreatorService(environment);
        var addressService = new AddressableContentService(
            new JsonTravelContentService(environment, creatorService));

        var flagship = await addressService.ResolveAsync(
            new CreatorId("creator_tsa_01"),
            "athens");
        var demo = await addressService.ResolveAsync(
            new CreatorId("creator_demo_01"),
            "athens");

        Assert.NotNull(flagship);
        Assert.NotNull(demo);
        Assert.Equal("/volumes/italy-greece-croatia/greece/athens", flagship.TargetUrl);
        Assert.Equal("/volumes/demo-aegean-notebook/greece/athens", demo.TargetUrl);
        Assert.NotEqual(flagship.CreatorId, demo.CreatorId);
    }
}
