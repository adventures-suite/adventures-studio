using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheSimontonAdventures.Web.Components;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the presentation-only Planner FootSteps rail contract.</summary>
public sealed class PlannerContextualIdeasRailTests
{
    /// <summary>The rail begins with an announced prompt and no fictional cards.</summary>
    [Fact]
    public async Task Rail_WithoutSelection_RendersPromptWithoutIdeas()
    {
        var html = await RenderAsync(new());

        Assert.Contains("Choose a destination or day", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Fictional local Alpha demo", html, StringComparison.Ordinal);
        Assert.Contains("role=\"complementary\"", html, StringComparison.Ordinal);
    }

    /// <summary>Fictional cards remain fail-closed unless the explicit Development gate is enabled.</summary>
    [Fact]
    public async Task Rail_SelectedContext_RequiresDevelopmentGateForFictionalIdeas()
    {
        var context = new PlannerIdeasContext(PlannerIdeasContextKind.Destination, "destination-1", "Example coast");
        var productionHtml = await RenderAsync(new() { [nameof(PlannerContextualIdeasRail.Context)] = context });
        var developmentHtml = await RenderAsync(new()
        {
            [nameof(PlannerContextualIdeasRail.Context)] = context,
            [nameof(PlannerContextualIdeasRail.AuthorizedItems)] = Items(PlannerIdeasContextKind.Destination)
        });

        Assert.Contains("No FootSteps for this context", productionHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Fictional local Alpha demo", productionHtml, StringComparison.Ordinal);
        Assert.Contains("FootSteps for", developmentHtml, StringComparison.Ordinal);
        Assert.Contains("Example coast", developmentHtml, StringComparison.Ordinal);
        Assert.Contains("Fictional local Alpha demo", developmentHtml, StringComparison.Ordinal);
        Assert.Contains("not booked, available, or added to your plan", developmentHtml, StringComparison.Ordinal);
        Assert.Contains("Cards per page", developmentHtml, StringComparison.Ordinal);
        Assert.Contains("Filter FootSteps", developmentHtml, StringComparison.Ordinal);
        Assert.Contains("Minimum days", developmentHtml, StringComparison.Ordinal);
        Assert.Contains("motorcycle", developmentHtml, StringComparison.Ordinal);
    }

    /// <summary>Development FootSteps change type and content with the selected planning context.</summary>
    [Fact]
    public async Task Rail_DevelopmentIdeas_AreContextSensitive()
    {
        var adventureHtml = await RenderDevelopmentContextAsync(
            new PlannerIdeasContext(PlannerIdeasContextKind.Adventure, "plan-1", "Atlantic light"));
        var destinationHtml = await RenderDevelopmentContextAsync(
            new PlannerIdeasContext(PlannerIdeasContextKind.Destination, "destination-1", "Example coast"));
        var dayHtml = await RenderDevelopmentContextAsync(
            new PlannerIdeasContext(PlannerIdeasContextKind.Day, "day-1", "Arrival day"));

        Assert.Contains("route pattern", adventureHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Journey · suggestion", adventureHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Sample day", adventureHtml, StringComparison.Ordinal);
        Assert.Contains("sample day", destinationHtml, StringComparison.Ordinal);
        Assert.Contains("activity", destinationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Journey", destinationHtml, StringComparison.Ordinal);
        Assert.Contains("One memorable local anchor", dayHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Stay pattern", dayHtml, StringComparison.Ordinal);
    }

    /// <summary>Populated contexts expose a labeled type filter and a route back to the whole Adventure.</summary>
    [Fact]
    public async Task Rail_ContextMenu_UsesPressedButtonsAndAdventureReset()
    {
        var destinationHtml = await RenderDevelopmentContextAsync(
            new PlannerIdeasContext(PlannerIdeasContextKind.Destination, "destination-1", "Example coast"));
        var adventureHtml = await RenderDevelopmentContextAsync(
            new PlannerIdeasContext(PlannerIdeasContextKind.Adventure, "plan-1", "Atlantic light"));

        Assert.Contains("aria-label=\"FootStep types for Example coast\"", destinationHtml, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"true\"", destinationHtml, StringComparison.Ordinal);
        Assert.Contains("Whole Adventure", destinationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Whole Adventure", adventureHtml, StringComparison.Ordinal);
    }

    /// <summary>Every non-populated state has explicit accessible status content.</summary>
    [Theory]
    [InlineData(PlannerIdeasState.Loading, "Finding FootSteps")]
    [InlineData(PlannerIdeasState.Empty, "No FootSteps for this context")]
    [InlineData(PlannerIdeasState.Unavailable, "FootSteps temporarily unavailable")]
    [InlineData(PlannerIdeasState.Denied, "FootSteps unavailable")]
    public async Task Rail_RendersExplicitState(PlannerIdeasState state, string title)
    {
        var html = await RenderAsync(new() { [nameof(PlannerContextualIdeasRail.StateOverride)] = state });

        Assert.Contains(title, html, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", html, StringComparison.Ordinal);
    }

    /// <summary>The rail exposes keyboard resize bounds and a narrow-screen dialog trigger.</summary>
    [Fact]
    public async Task Rail_RendersResponsiveInteractionContract()
    {
        var html = await RenderAsync(new() { [nameof(PlannerContextualIdeasRail.WidthPixels)] = 320 });

        Assert.Contains("aria-haspopup=\"dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"separator\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuemin=\"272\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuemax=\"400\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuenow=\"320\"", html, StringComparison.Ordinal);
        var markup = File.ReadAllText(Path.Combine(FindApplicationRoot(), "Components", "Planner", "PlannerContextualIdeasRail.razor"));
        var codeBehind = File.ReadAllText(Path.Combine(FindApplicationRoot(), "Components", "Planner", "PlannerContextualIdeasRail.razor.cs"));
        Assert.Contains("@onpointermove", markup, StringComparison.Ordinal);
        Assert.Contains("role=\"@(IsDrawerOpen ? \"dialog\" : \"complementary\")\"", markup, StringComparison.Ordinal);
        Assert.Contains("protected override async Task OnAfterRenderAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("FocusCloseAfterRender = true;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("FocusOpenAfterRender = true;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("await CloseButton.FocusAsync();", codeBehind, StringComparison.Ordinal);
        Assert.Contains("await OpenButton.FocusAsync();", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>Authorized results expose grouping, deterministic sorting, and equivalent card/list views.</summary>
    [Fact]
    public async Task Rail_PopulatedResults_ExposeDisplayControls()
    {
        var html = await RenderDevelopmentContextAsync(
            new PlannerIdeasContext(PlannerIdeasContextKind.Destination, "destination-1", "Example coast"));

        Assert.Contains("Group by", html, StringComparison.Ordinal);
        Assert.Contains("FootStep type", html, StringComparison.Ordinal);
        Assert.Contains("Transportation", html, StringComparison.Ordinal);
        Assert.Contains("Sort by", html, StringComparison.Ordinal);
        Assert.Contains("Shortest duration", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"FootStep view type\"", html, StringComparison.Ordinal);
        Assert.Contains(">Cards</button>", html, StringComparison.Ordinal);
        Assert.Contains(">List</button>", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(Dictionary<string, object?> parameters)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime, StaticTestJavaScriptRuntime>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<PlannerContextualIdeasRail>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }

    private static Task<string> RenderDevelopmentContextAsync(PlannerIdeasContext context) => RenderAsync(new()
    {
        [nameof(PlannerContextualIdeasRail.Context)] = context,
        [nameof(PlannerContextualIdeasRail.AuthorizedItems)] = Items(context.Kind)
    });

    private static IReadOnlyList<PlannerFootStepDefinition> Items(PlannerIdeasContextKind kind) => kind switch
    {
        PlannerIdeasContextKind.Adventure => [Item("route-pattern", "Scenic motorcycle touring rhythm")],
        PlannerIdeasContextKind.Destination =>
            [Item("sample-day", "A gentle first day"), Item("activity", "One memorable local anchor")],
        _ => [Item("activity", "One memorable local anchor")]
    };

    private static PlannerFootStepDefinition Item(string kind, string title) => new()
    {
        Id = $"footstep_{kind}",
        Version = "1.0",
        Kind = kind,
        Title = title,
        Summary = "Fictional summary.",
        Attribution = "Fictional local Alpha demo",
        Freshness = "Demo snapshot",
        ContextKinds = new HashSet<PlannerFootStepContextKind> { PlannerFootStepContextKind.Adventure, PlannerFootStepContextKind.Destination, PlannerFootStepContextKind.Day },
        TransportationModes = new HashSet<string>(StringComparer.Ordinal) { "motorcycle" },
        Categories = new HashSet<string>(StringComparer.Ordinal) { "outdoors" },
        RouteStyles = new HashSet<string>(StringComparer.Ordinal) { "scenic" },
        Surfaces = new HashSet<string>(StringComparer.Ordinal) { "paved" }
    };

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

        throw new DirectoryNotFoundException("The web application root could not be found.");
    }
}
