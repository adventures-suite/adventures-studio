using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheSimontonAdventures.Web.Components;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the pre-plan Journey Template discovery workflow.</summary>
public sealed class PlannerJourneyStarterTests
{
    /// <summary>The starter presents honest manual and template choices before browsing.</summary>
    [Fact]
    public async Task Starter_InitiallyOffersScratchAndJourneyDiscovery()
    {
        var html = await RenderAsync([Template()]);

        Assert.Contains("Start a journey", html, StringComparison.Ordinal);
        Assert.Contains("Start from scratch", html, StringComparison.Ordinal);
        Assert.Contains("Browse Journey FootSteps", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Preview this Journey", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Import", html, StringComparison.Ordinal);
    }

    /// <summary>An empty authorized catalog never exposes fictional templates.</summary>
    [Fact]
    public async Task Starter_EmptyCatalogRendersHonestFallback()
    {
        var html = await RenderAsync([], browse: true);

        Assert.Contains("Journey FootSteps are coming soon", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Portugal by rail", html, StringComparison.Ordinal);
    }

    /// <summary>Browsing renders only the templates supplied by the authorized catalog query.</summary>
    [Fact]
    public async Task Starter_BrowseRendersAuthorizedTemplates()
    {
        var html = await RenderAsync([Template()], browse: true);

        Assert.Contains("Portugal by rail", html, StringComparison.Ordinal);
        Assert.Contains("Lisbon", html, StringComparison.Ordinal);
        Assert.Contains("Preview this Journey", html, StringComparison.Ordinal);
        Assert.Contains("Cards per page", html, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 1 · 1 FootStep", html, StringComparison.Ordinal);
        Assert.Contains("not bookings, prices, or availability", html, StringComparison.Ordinal);
    }

    /// <summary>A selected template offers one direct atomic creation form without pretend customization.</summary>
    [Fact]
    public async Task Starter_SelectedTemplateRendersDirectCreationContract()
    {
        var markup = File.ReadAllText(Path.Combine(
            FindApplicationRoot(), "Components", "Planner", "PlannerJourneyStarter.razor"));

        Assert.Contains("Complete Journey Template preview", markup, StringComparison.Ordinal);
        Assert.Contains("CreateFromTemplatePath", markup, StringComparison.Ordinal);
        Assert.Contains("AntiforgeryToken", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"templateId\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"templateVersion\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"startDate\"", markup, StringComparison.Ordinal);
        Assert.Contains("Create my private Journey", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedPace", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedTransport", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Book now", markup, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> RenderAsync(
        IReadOnlyList<AdventureTemplateBlueprint> templates,
        bool browse = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var output = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RenderComponentAsync<PlannerJourneyStarter>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(PlannerJourneyStarter.Templates)] = templates,
                    [nameof(PlannerJourneyStarter.CreateFromTemplatePath)] = "/template-create",
                    [nameof(PlannerJourneyStarter.StartWithIdeasOpen)] = browse
                })));
        return await renderer.Dispatcher.InvokeAsync(output.ToHtmlString);
    }

    private static AdventureTemplateBlueprint Template() => new()
    {
        VersionId = new("platform.portugal-by-rail", "1.0"),
        OwnerType = AdventureTemplateOwnerType.Platform,
        OwnerId = "adventures-suite",
        SourceLocale = "en-US",
        Attribution = "Curated collection",
        Title = "Portugal by rail",
        WorkingDescription = "A comfortable rail-first Journey.",
        DurationDays = 8,
        Destinations = [new("lisbon", "Lisbon", 0, 2, new("Europe/Lisbon"))],
        Days = [new("day-1", 0, "lisbon", new("Europe/Lisbon"), "Arrive")]
    };

    private static string FindApplicationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "TheSimontonAdventures.Web");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the web application root.");
    }
}
