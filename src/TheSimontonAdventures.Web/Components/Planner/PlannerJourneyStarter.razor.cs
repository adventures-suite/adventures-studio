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
            "Portugal by rail",
            "Pair Lisbon's neighborhoods with Porto's riverfront at a comfortable pace.",
            "Lisbon → Coimbra → Porto",
            "8–10 days",
            "PT"),
        new(
            "Adriatic coast and islands",
            "Balance historic coastal cities with slower island time and flexible sea days.",
            "Split → Hvar → Dubrovnik",
            "9–12 days",
            "AC")
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

    /// <summary>Gets whether the Journey discovery panel is open.</summary>
    public bool IsBrowsingIdeas { get; private set; }

    /// <inheritdoc />
    protected override void OnInitialized() => IsBrowsingIdeas = StartWithIdeasOpen;

    private void BrowseIdeas() => IsBrowsingIdeas = true;

    private async Task StartFromScratchAsync()
    {
        IsBrowsingIdeas = false;
        await OnStartFromScratch.InvokeAsync();
    }

    private Task SelectIdeaAsync(DevelopmentJourneyIdea idea) =>
        OnJourneySelected.InvokeAsync(new PlannerJourneySeed(idea.Title, idea.Summary));

    private sealed record DevelopmentJourneyIdea(
        string Title,
        string Summary,
        string Route,
        string Duration,
        string Monogram);
}
