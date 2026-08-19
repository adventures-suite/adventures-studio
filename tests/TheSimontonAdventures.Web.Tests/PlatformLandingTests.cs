using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheSimontonAdventures.Web.Components.Platform;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the public AdventuresSuite product entrance.</summary>
public sealed class PlatformLandingTests
{
    /// <summary>Ensures the platform story and customer entry points render together.</summary>
    [Fact]
    public async Task LandingPage_RendersStoryApplicationsAndSignIn()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<PlatformLanding>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(PlatformLanding.SignInUrl)] = "https://workspace.example.com/authentication/sign-in",
                    [nameof(PlatformLanding.FeaturedAdventureUrl)] = "https://creator.example.com/",
                    [nameof(PlatformLanding.JourneyImageUrl)] = "/platform-journey.jpeg",
                    [nameof(PlatformLanding.HeroImageUrl)] = "/platform-hero.jpeg",
                    [nameof(PlatformLanding.StoryImageUrl)] = "/platform-story.jpeg",
                    [nameof(PlatformLanding.FeaturedImageUrl)] = "/platform-featured.jpeg"
                }));
            return output.ToHtmlString();
        });

        Assert.Contains("Your adventures deserve to become more than memories", html);
        Assert.Contains("Why we exist", html);
        Assert.Contains("Creator Workspace", html);
        Assert.Contains("AdventuresCompanion", html);
        Assert.Contains("Become an AdventuresSuite Creator", html);
        Assert.Contains("https://workspace.example.com/authentication/sign-in", html);
        Assert.Contains("Explore The Simonton Adventures", html);
        Assert.Contains("https://creator.example.com/", html);
        Assert.Contains("/platform-journey.jpeg", html);
        Assert.Contains("Sometimes it starts with two passports", html);
        Assert.Contains("/platform-hero.jpeg", html);
        Assert.Contains("/platform-story.jpeg", html);
        Assert.Contains("/platform-featured.jpeg", html);
        Assert.Contains("See what a journey can become", html);
        Assert.DoesNotContain("creator_tsa_01", html);
    }
}
