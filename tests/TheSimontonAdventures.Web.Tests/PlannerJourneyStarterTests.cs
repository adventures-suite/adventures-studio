using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheSimontonAdventures.Web.Components;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the pre-plan Journey discovery workflow.</summary>
public sealed class PlannerJourneyStarterTests
{
    /// <summary>The starter presents only honest, actionable starting choices before browsing.</summary>
    [Fact]
    public async Task Starter_InitiallyOffersScratchAndJourneyDiscovery()
    {
        var html = await RenderAsync(enableDevelopmentIdeas: true);

        Assert.Contains("Start a journey", html, StringComparison.Ordinal);
        Assert.Contains("Start from scratch", html, StringComparison.Ordinal);
        Assert.Contains("Browse journey ideas", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Preview and customize", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Complete Journey template preview", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Import", html, StringComparison.Ordinal);
        Assert.DoesNotContain("template", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Production rendering does not expose fictional development Journey content.</summary>
    [Fact]
    public async Task Starter_ProductionGateDoesNotRenderFictionalIdeas()
    {
        var html = await RenderAsync(enableDevelopmentIdeas: false, browse: true);

        Assert.Contains("Journey ideas are coming soon", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Portugal by rail", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Adriatic coast and islands", html, StringComparison.Ordinal);
    }

    /// <summary>Development browsing renders previewable Journey seeds outside an existing plan.</summary>
    [Fact]
    public async Task Starter_DevelopmentBrowseRendersJourneyPreviews()
    {
        var html = await RenderAsync(enableDevelopmentIdeas: true, browse: true);

        Assert.Contains("Portugal by rail", html, StringComparison.Ordinal);
        Assert.Contains("Lisbon", html, StringComparison.Ordinal);
        Assert.Contains("Coimbra", html, StringComparison.Ordinal);
        Assert.Contains("Porto", html, StringComparison.Ordinal);
        Assert.Contains("Preview and customize", html, StringComparison.Ordinal);
        Assert.Contains("not bookings, availability, or changes to a plan", html, StringComparison.Ordinal);
    }

    /// <summary>A selected template previews its complete blueprint and preserves the Alpha mutation boundary.</summary>
    [Fact]
    public async Task Starter_SelectedTemplateRendersCompleteReviewWithoutClaimingInstantiation()
    {
        var html = await RenderAsync(enableDevelopmentIdeas: true, browse: true, previewKey: "portugal-rail");

        Assert.Contains("Complete Journey template preview", html, StringComparison.Ordinal);
        Assert.Contains("Destinations", html, StringComparison.Ordinal);
        Assert.Contains("Sample itinerary", html, StringComparison.Ordinal);
        Assert.Contains("Travel methods", html, StringComparison.Ordinal);
        Assert.Contains("Stay patterns", html, StringComparison.Ordinal);
        Assert.Contains("Lisbon", html, StringComparison.Ordinal);
        Assert.Contains("Coimbra", html, StringComparison.Ordinal);
        Assert.Contains("Porto", html, StringComparison.Ordinal);
        Assert.Contains("Preview only", html, StringComparison.Ordinal);
        Assert.Contains("does not yet copy stops or itinerary items", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Use as a starting point", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Book now", html, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> RenderAsync(bool enableDevelopmentIdeas, bool browse = false, string? previewKey = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var output = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RenderComponentAsync<PlannerJourneyStarter>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(PlannerJourneyStarter.EnableDevelopmentIdeas)] = enableDevelopmentIdeas,
                    [nameof(PlannerJourneyStarter.StartWithIdeasOpen)] = browse,
                    [nameof(PlannerJourneyStarter.InitialPreviewKey)] = previewKey
                })));
        return await renderer.Dispatcher.InvokeAsync(output.ToHtmlString);
    }
}
