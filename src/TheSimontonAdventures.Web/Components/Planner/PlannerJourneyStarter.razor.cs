using Microsoft.AspNetCore.Components;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Describes a non-authoritative Journey idea selected before private plan creation.</summary>
public sealed record PlannerJourneySeed
{
    /// <summary>Initializes a reviewable pre-creation Journey seed.</summary>
    public PlannerJourneySeed(string title, string description) =>
        (Title, Description) = (title, description);

    /// <summary>Gets the suggested private-plan title.</summary>
    public string Title { get; }

    /// <summary>Gets the suggested private-plan working description.</summary>
    public string Description { get; }
}

/// <summary>Renders the pre-plan choice between manual creation and Journey discovery.</summary>
public partial class PlannerJourneyStarter : ComponentBase
{
    private static readonly IReadOnlyList<DevelopmentJourneyIdea> DevelopmentIdeas =
    [
        new(
            "portugal-rail",
            "Portugal by rail",
            "Pair Lisbon's neighborhoods with Porto's riverfront at a comfortable pace.",
            "Lisbon → Coimbra → Porto",
            "8–10 days",
            "PT",
            "AdventuresSuite curated Alpha collection",
            "1.0",
            [
                new("lisbon", "Lisbon", 3, "Neighborhood walks, viewpoints, and flexible arrival time"),
                new("coimbra", "Coimbra", 1, "A slower university-city pause between major stops"),
                new("porto", "Porto", 3, "Riverfront exploration and an unhurried final stay")
            ],
            [
                new("Arrive and settle in", "Lisbon", "A gentle neighborhood orientation"),
                new("Lisbon perspectives", "Lisbon", "One anchor experience with flexible discoveries"),
                new("Pause in Coimbra", "Coimbra", "Rail arrival and a compact historic-center day"),
                new("Porto at the river", "Porto", "Ribeira, viewpoints, and an open evening")
            ],
            [
                new("Lisbon → Coimbra", "Intercity rail", "Reserve seats after dates are confirmed"),
                new("Coimbra → Porto", "Intercity rail", "Keep the arrival afternoon lightly planned")
            ],
            [
                new("Lisbon", "A walkable central base for three nights"),
                new("Coimbra", "One convenient overnight near the historic center"),
                new("Porto", "A river-accessible base for the final three nights")
            ]),
        new(
            "adriatic-coast",
            "Adriatic coast and islands",
            "Balance historic coastal cities with slower island time and flexible sea days.",
            "Split → Hvar → Dubrovnik",
            "9–12 days",
            "AC",
            "AdventuresSuite curated Alpha collection",
            "1.0",
            [
                new("split", "Split", 3, "Old-town exploration with time beyond the palace"),
                new("hvar", "Hvar", 3, "Island pace with weather-flexible choices"),
                new("dubrovnik", "Dubrovnik", 3, "Historic walls and a slower final chapter")
            ],
            [
                new("Settle into Split", "Split", "A light arrival and waterfront evening"),
                new("Island rhythm", "Hvar", "One chosen experience with flexible beach time"),
                new("Dubrovnik perspectives", "Dubrovnik", "Walls, neighborhoods, and an open evening")
            ],
            [
                new("Split → Hvar", "Passenger ferry", "Schedules remain seasonal and unconfirmed"),
                new("Hvar → Dubrovnik", "Passenger ferry", "Retain a weather-aware alternative")
            ],
            [
                new("Split", "A central base with simple port access"),
                new("Hvar", "A quieter base within walking distance of town"),
                new("Dubrovnik", "A base balancing old-city access and calmer evenings")
            ])
    ];

    /// <summary>Gets or sets whether fictional deterministic Journey ideas may appear.</summary>
    [Parameter]
    public bool EnableDevelopmentIdeas { get; set; }

    /// <summary>Gets or sets the callback for a reviewed Journey idea selection.</summary>
    [Parameter]
    public EventCallback<PlannerJourneySeed> OnJourneySelected { get; set; }

    /// <summary>Gets or sets the callback that clears a previously selected Journey idea.</summary>
    [Parameter]
    public EventCallback OnStartFromScratch { get; set; }

    /// <summary>Gets or sets whether Journey discovery is initially expanded.</summary>
    [Parameter]
    public bool StartWithIdeasOpen { get; set; }

    /// <summary>Gets or sets the optional deterministic template initially shown in preview.</summary>
    [Parameter]
    public string? InitialPreviewKey { get; set; }

    /// <summary>Gets whether the Journey discovery panel is open.</summary>
    public bool IsBrowsingIdeas { get; private set; }

    private DevelopmentJourneyIdea? SelectedIdea { get; set; }
    private HashSet<string> SelectedDestinationKeys { get; } = new(StringComparer.Ordinal);
    private string SelectedPace { get; set; } = "Balanced";
    private string SelectedTransport { get; set; } = "Recommended mix";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        IsBrowsingIdeas = StartWithIdeasOpen;
        if (EnableDevelopmentIdeas && InitialPreviewKey is not null)
        {
            var idea = DevelopmentIdeas.FirstOrDefault(candidate => candidate.Key == InitialPreviewKey);
            if (idea is not null)
            {
                PreviewIdea(idea);
            }
        }
    }

    private void BrowseIdeas() => IsBrowsingIdeas = true;

    private async Task StartFromScratchAsync()
    {
        IsBrowsingIdeas = false;
        SelectedIdea = null;
        SelectedDestinationKeys.Clear();
        await OnStartFromScratch.InvokeAsync();
    }

    private void PreviewIdea(DevelopmentJourneyIdea idea)
    {
        SelectedIdea = idea;
        SelectedDestinationKeys.Clear();
        foreach (var destination in idea.Destinations)
        {
            SelectedDestinationKeys.Add(destination.Key);
        }

        SelectedPace = "Balanced";
        SelectedTransport = "Recommended mix";
    }

    private void ToggleDestination(string key, ChangeEventArgs args)
    {
        if (args.Value is true)
        {
            SelectedDestinationKeys.Add(key);
        }
        else
        {
            SelectedDestinationKeys.Remove(key);
        }
    }

    private Task UseSelectedIdeaAsync()
    {
        if (SelectedIdea is null)
        {
            return Task.CompletedTask;
        }

        var includedDestinations = SelectedIdea.Destinations
            .Where(destination => SelectedDestinationKeys.Contains(destination.Key))
            .Select(destination => destination.Name)
            .ToArray();
        var route = includedDestinations.Length == 0
            ? "No template destinations selected"
            : string.Join(" → ", includedDestinations);
        var description = $"{SelectedIdea.Summary} Proposed route: {route}. Pace: {SelectedPace}. Transportation: {SelectedTransport}.";
        return OnJourneySelected.InvokeAsync(new PlannerJourneySeed(SelectedIdea.Title, description));
    }

    private sealed record DevelopmentJourneyIdea(
        string Key,
        string Title,
        string Summary,
        string Route,
        string Duration,
        string Monogram,
        string Source,
        string Version,
        IReadOnlyList<DevelopmentDestination> Destinations,
        IReadOnlyList<DevelopmentDay> Days,
        IReadOnlyList<DevelopmentTravelSegment> TravelSegments,
        IReadOnlyList<DevelopmentStayPattern> StayPatterns);

    private sealed record DevelopmentDestination(string Key, string Name, int Nights, string Highlight);
    private sealed record DevelopmentDay(string Title, string Destination, string Summary);
    private sealed record DevelopmentTravelSegment(string Route, string Method, string Guidance);
    private sealed record DevelopmentStayPattern(string Destination, string Guidance);
}
