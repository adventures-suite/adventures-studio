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

        Assert.Equal(960, PlannerContextualIdeasRail.MaximumWidthPixels);
        Assert.Equal(64, PlannerContextualIdeasRail.ResizeStepPixels);
        Assert.Contains("aria-haspopup=\"dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"separator\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuemin=\"272\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuemax=\"960\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuenow=\"320\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"FootSteps rail width\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Narrow FootSteps rail\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Widen FootSteps rail\"", html, StringComparison.Ordinal);
        Assert.Contains("title=\"Narrow FootSteps rail\"", html, StringComparison.Ordinal);
        Assert.Contains("title=\"Widen FootSteps rail\"", html, StringComparison.Ordinal);
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

    /// <summary>Authorized results expose grouping, deterministic sorting, and distinct card, list, and tabular views.</summary>
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
        Assert.Contains("aria-label=\"Card view\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"List view\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Tabular view\"", html, StringComparison.Ordinal);
        Assert.Contains("title=\"Card view\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("focusable=\"false\"", html, StringComparison.Ordinal);
        var markup = File.ReadAllText(Path.Combine(
            FindApplicationRoot(), "Components", "Planner", "PlannerContextualIdeasRail.razor"));
        Assert.Contains("<table class=\"planner-ideas__table\">", markup, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">FootStep</th>", markup, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Transportation</th>", markup, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"row\">", markup, StringComparison.Ordinal);
        Assert.Contains("role=\"region\" aria-label=\"FootSteps table\"", markup, StringComparison.Ordinal);
    }

    /// <summary>The Adventure catalog demonstrates diverse and mixed-mode Journeys through result-derived facets.</summary>
    [Fact]
    public async Task Rail_AdventureResults_ExposeDiverseTravelFacets()
    {
        var html = await RenderAsync(new()
        {
            [nameof(PlannerContextualIdeasRail.Context)] = new PlannerIdeasContext(
                PlannerIdeasContextKind.Adventure, "plan-1", "Many ways to travel"),
            [nameof(PlannerContextualIdeasRail.AuthorizedItems)] = DiverseJourneyItems()
        });

        Assert.Contains("motorcycle", html, StringComparison.Ordinal);
        Assert.Contains("rv", html, StringComparison.Ordinal);
        Assert.Contains("bicycle", html, StringComparison.Ordinal);
        Assert.Contains("cruise ship", html, StringComparison.Ordinal);
        Assert.Contains("sailboat", html, StringComparison.Ordinal);
        Assert.Contains("four wheel drive", html, StringComparison.Ordinal);
        Assert.Contains("rail", html, StringComparison.Ordinal);
        Assert.Contains("trekking", html, StringComparison.Ordinal);
        Assert.Contains("mixed mode", html, StringComparison.Ordinal);
        Assert.Contains("Clear all", File.ReadAllText(Path.Combine(
            FindApplicationRoot(), "Components", "Planner", "PlannerContextualIdeasRail.razor")),
            StringComparison.Ordinal);
        Assert.Contains("No FootSteps match these filters", File.ReadAllText(Path.Combine(
            FindApplicationRoot(), "Components", "Planner", "PlannerContextualIdeasRail.razor")),
            StringComparison.Ordinal);
    }

    /// <summary>An editable Destination FootStep renders reviewed dates and exact source fields.</summary>
    [Fact]
    public async Task Rail_EditableDestinationFootStep_RendersReviewedApplyForm()
    {
        var html = await RenderAsync(new()
        {
            [nameof(PlannerContextualIdeasRail.Context)] = new PlannerIdeasContext(
                PlannerIdeasContextKind.Adventure, "plan-1", "Portugal"),
            [nameof(PlannerContextualIdeasRail.AuthorizedItems)] = new[] { DestinationItem() },
            [nameof(PlannerContextualIdeasRail.CanEdit)] = true,
            [nameof(PlannerContextualIdeasRail.ExpectedVersion)] = 4L,
            [nameof(PlannerContextualIdeasRail.PlanStartDate)] = new DateOnly(2027, 5, 1),
            [nameof(PlannerContextualIdeasRail.PlanEndDate)] = new DateOnly(2027, 5, 10),
            [nameof(PlannerContextualIdeasRail.ApplyDestinationPath)] = "/apply-destination"
        });

        Assert.Contains("Preview Add to plan", html, StringComparison.Ordinal);
        Assert.Contains("Lisbon, Portugal", html, StringComparison.Ordinal);
        Assert.Contains("Nothing is booked", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/apply-destination\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"expectedVersion\" value=\"4\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"footStepId\" value=\"footstep_destination_lisbon_gateway\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"footStepVersion\" value=\"1.0\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"timeZoneId\" value=\"Europe/Lisbon\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"startDate\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"endDate\"", html, StringComparison.Ordinal);
        Assert.Contains("Add to plan", html, StringComparison.Ordinal);
    }

    /// <summary>Read-only users see the proposal but no mutation form or usable action.</summary>
    [Fact]
    public async Task Rail_ReadOnlyDestinationFootStep_DoesNotRenderApplyForm()
    {
        var html = await RenderAsync(new()
        {
            [nameof(PlannerContextualIdeasRail.Context)] = new PlannerIdeasContext(
                PlannerIdeasContextKind.Adventure, "plan-1", "Portugal"),
            [nameof(PlannerContextualIdeasRail.AuthorizedItems)] = new[] { DestinationItem() },
            [nameof(PlannerContextualIdeasRail.CanEdit)] = false,
            [nameof(PlannerContextualIdeasRail.ApplyDestinationPath)] = "/apply-destination"
        });

        Assert.DoesNotContain("action=\"/apply-destination\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Add to plan", html, StringComparison.Ordinal);
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

    private static PlannerFootStepDefinition DestinationItem() => new()
    {
        Id = "footstep_destination_lisbon_gateway",
        Version = "1.0",
        Kind = "destination",
        Title = "Lisbon cultural gateway",
        Summary = "Fictional reviewed destination draft.",
        Attribution = "AdventuresSuite fictional editorial demo",
        Freshness = "Demo snapshot",
        ContextKinds = new HashSet<PlannerFootStepContextKind> { PlannerFootStepContextKind.Adventure },
        DestinationDraft = new("Lisbon, Portugal", "Europe/Lisbon")
    };

    private static IReadOnlyList<PlannerFootStepDefinition> DiverseJourneyItems() =>
    [
        DiverseItem("Motorcycle touring", ["motorcycle"], ["road-trip"]),
        DiverseItem("RV parks loop", ["rv"], ["nature"]),
        DiverseItem("Coastal cycling", ["bicycle"], ["cycling"]),
        DiverseItem("Village trek", ["walking"], ["trekking"]),
        DiverseItem("Island sailing", ["sailboat"], ["sailing"]),
        DiverseItem("Classic rail", ["rail"], ["rail-journey"]),
        DiverseItem("Cultural cruise", ["cruise-ship"], ["cruise"]),
        DiverseItem("Desert overland", ["four-wheel-drive"], ["overland"]),
        DiverseItem("Mixed mode islands", ["rail", "ferry", "bicycle"], ["mixed-mode"])
    ];

    private static PlannerFootStepDefinition DiverseItem(
        string title,
        string[] transportationModes,
        string[] categories) => new()
        {
            Id = $"footstep_{title.Replace(' ', '_').ToLowerInvariant()}",
            Version = "1.0",
            Kind = "journey-pattern",
            Title = title,
            Summary = "Fictional diverse Journey pattern.",
            Attribution = "Fictional local Alpha demo",
            Freshness = "Demo snapshot",
            ContextKinds = new HashSet<PlannerFootStepContextKind> { PlannerFootStepContextKind.Adventure },
            TransportationModes = transportationModes.ToHashSet(StringComparer.Ordinal),
            Categories = categories.ToHashSet(StringComparer.Ordinal)
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
