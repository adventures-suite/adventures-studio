using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Identifies the selected Planner canvas context without conveying plan authority.</summary>
public enum PlannerIdeasContextKind
{
    /// <summary>The whole Adventure Plan.</summary>
    Adventure,

    /// <summary>A destination visit.</summary>
    Destination,

    /// <summary>An itinerary day.</summary>
    Day
}

/// <summary>Describes transient Planner context selected for FootSteps presentation.</summary>
public sealed record PlannerIdeasContext
{
    /// <summary>Initializes a transient FootSteps context.</summary>
    public PlannerIdeasContext(PlannerIdeasContextKind kind, string id, string label) =>
        (Kind, Id, Label) = (kind, id, label);

    /// <summary>Gets the context type.</summary>
    public PlannerIdeasContextKind Kind { get; }

    /// <summary>Gets the stable plan-owned identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the authorized display label.</summary>
    public string Label { get; }
}

/// <summary>Defines safe presentation states for the contextual FootSteps rail.</summary>
public enum PlannerIdeasState
{
    /// <summary>No canvas context is selected.</summary>
    NoSelection,

    /// <summary>FootSteps are loading.</summary>
    Loading,

    /// <summary>No suggestions matched.</summary>
    Empty,

    /// <summary>The source is unavailable.</summary>
    Unavailable,

    /// <summary>The projection is denied.</summary>
    Denied,

    /// <summary>Suggestions are available.</summary>
    Populated
}

/// <summary>Renders a responsive, presentation-only projection beside the authoritative itinerary.</summary>
public partial class PlannerContextualIdeasRail : ComponentBase
{
    private static readonly IReadOnlyList<int> RailPageSizeOptions = [1, 2, 3];
    /// <summary>Gets the minimum supported FootSteps rail width in pixels.</summary>
    public const int MinimumWidthPixels = 272;
    /// <summary>Gets the maximum supported FootSteps rail width in pixels.</summary>
    public const int MaximumWidthPixels = 400;

    private double? PointerStartX { get; set; }
    private int PointerStartWidth { get; set; }
    private bool FocusCloseAfterRender { get; set; }
    private bool FocusOpenAfterRender { get; set; }
    private static readonly IReadOnlyDictionary<PlannerIdeasContextKind, IReadOnlyList<PlannerIdeaCard>> DevelopmentIdeas =
        new Dictionary<PlannerIdeasContextKind, IReadOnlyList<PlannerIdeaCard>>
        {
            [PlannerIdeasContextKind.Adventure] =
            [
                new("Destination", "Add a contrasting second stop", "Pair the current route with a nearby destination that offers a different pace.", "Fictional local Alpha demo", "Demo snapshot", "Helps develop the Adventure route.", "DS")
            ],
            [PlannerIdeasContextKind.Destination] =
            [
                new("Sample day", "A gentle first day", "Combine one neighborhood anchor with flexible time to settle in.", "Fictional local Alpha demo", "Demo snapshot", "Matches the selected destination visit.", "SD"),
                new("Activity", "A slower local morning", "Leave room for a neighborhood walk and an unhurried café stop.", "Fictional local Alpha demo", "Demo snapshot", "Matches the selected destination visit.", "AM"),
                new("Stay pattern", "Choose a walkable home base", "Consider a central area that reduces transfers between planned days.", "Fictional local Alpha demo", "Demo snapshot", "Supports destination-level planning.", "ST")
            ],
            [PlannerIdeasContextKind.Day] =
            [
                new("Activity", "One memorable local anchor", "Choose one meaningful experience and preserve room around it.", "Fictional local Alpha demo", "Demo snapshot", "Fits the selected itinerary day.", "AC"),
                new("Meal rhythm", "Protect an unhurried meal", "Reserve a flexible meal window instead of filling every hour.", "Fictional local Alpha demo", "Demo snapshot", "Supports a realistic daily pace.", "MR"),
                new("Pacing", "Keep nearby discoveries flexible", "Group optional discoveries near the day's anchor to avoid unnecessary transfers.", "Fictional local Alpha demo", "Demo snapshot", "Uses the selected day as context.", "PC")
            ]
        };

    private string? PreviousContextKey { get; set; }
    private string? SelectedType { get; set; }
    private int PageSize { get; set; } = 3;
    private int CurrentPage { get; set; } = 1;

    /// <summary>Gets or sets the selected authorized canvas context.</summary>
    [Parameter]
    public PlannerIdeasContext? Context { get; set; }
    /// <summary>Gets or sets whether fictional deterministic cards may appear in explicit Development authentication.</summary>
    [Parameter]
    public bool EnableDevelopmentIdeas { get; set; }
    /// <summary>Gets or sets an explicit state for deterministic component verification.</summary>
    [Parameter]
    public PlannerIdeasState? StateOverride { get; set; }
    /// <summary>Gets or sets the bounded desktop width.</summary>
    [Parameter]
    public int WidthPixels { get; set; } = 320;
    /// <summary>Gets or sets the parent-owned resize callback.</summary>
    [Parameter]
    public EventCallback<int> OnResizeRequested { get; set; }
    /// <summary>Gets or sets the callback that restores whole-Adventure context.</summary>
    [Parameter]
    public EventCallback OnAdventureContextRequested { get; set; }
    /// <summary>Gets whether the desktop rail is collapsed.</summary>
    public bool IsCollapsed { get; private set; }
    /// <summary>Gets whether the narrow-screen drawer is open.</summary>
    public bool IsDrawerOpen { get; private set; }
    /// <summary>Gets the effective state after Development-only source gating.</summary>
    public PlannerIdeasState EffectiveState => StateOverride ?? (Context is null
        ? PlannerIdeasState.NoSelection
        : EnableDevelopmentIdeas ? PlannerIdeasState.Populated : PlannerIdeasState.Empty);
    /// <summary>Gets the rendered fictional cards only when the Development-only gate is active.</summary>
    internal IReadOnlyList<PlannerIdeaCard> Ideas => EffectiveState != PlannerIdeasState.Populated
        || !EnableDevelopmentIdeas
        || Context is null
        || !DevelopmentIdeas.TryGetValue(Context.Kind, out var ideas)
            ? []
            : ideas;
    /// <summary>Gets the FootStep types available for the selected context.</summary>
    internal IReadOnlyList<string> AvailableTypes => Ideas
        .Select(idea => idea.Type)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    /// <summary>Gets the FootSteps matching the transient type filter.</summary>
    internal IReadOnlyList<PlannerIdeaCard> FilteredIdeas => SelectedType is null
        ? Ideas
        : Ideas.Where(idea => idea.Type == SelectedType).ToArray();
    /// <summary>Gets the FootSteps visible on the selected page.</summary>
    internal IReadOnlyList<PlannerIdeaCard> PagedIdeas => FilteredIdeas
        .Skip((CurrentPage - 1) * PageSize)
        .Take(PageSize)
        .ToArray();
    /// <summary>Gets the CSS classes representing rail state.</summary>
    public string RailClasses => $"planner-ideas{(IsCollapsed ? " planner-ideas--collapsed" : string.Empty)}{(IsDrawerOpen ? " planner-ideas--drawer-open" : string.Empty)}";

    private ElementReference OpenButton { get; set; }
    private ElementReference CloseButton { get; set; }
    private void ToggleCollapsed() => IsCollapsed = !IsCollapsed;
    private void SelectType(string? type)
    {
        SelectedType = type;
        CurrentPage = 1;
    }
    private Task ShowWholeAdventureAsync() => OnAdventureContextRequested.InvokeAsync();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var contextKey = Context is null ? null : $"{Context.Kind}:{Context.Id}";
        if (!string.Equals(contextKey, PreviousContextKey, StringComparison.Ordinal))
        {
            PreviousContextKey = contextKey;
            SelectedType = null;
            CurrentPage = 1;
        }
    }
    private Task ChangePageSizeAsync(int pageSize)
    {
        PageSize = pageSize;
        CurrentPage = 1;
        return Task.CompletedTask;
    }

    private Task ChangePageAsync(int page)
    {
        CurrentPage = page;
        return Task.CompletedTask;
    }
    private Task OpenDrawerAsync()
    {
        IsDrawerOpen = true;
        FocusCloseAfterRender = true;
        return Task.CompletedTask;
    }

    private Task CloseDrawerAsync()
    {
        IsDrawerOpen = false;
        FocusOpenAfterRender = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (FocusCloseAfterRender)
        {
            FocusCloseAfterRender = false;
            await CloseButton.FocusAsync();
        }
        else if (FocusOpenAfterRender)
        {
            FocusOpenAfterRender = false;
            await OpenButton.FocusAsync();
        }
    }

    private async Task HandleKeyAsync(KeyboardEventArgs args)
    {
        if (IsDrawerOpen && args.Key == "Escape")
        {
            await CloseDrawerAsync();
        }
        else if (IsDrawerOpen && args.Key == "Tab")
        {
            await CloseButton.FocusAsync();
        }
    }
    private Task HandleResizeKeyAsync(KeyboardEventArgs args) => args.Key switch
    {
        "ArrowLeft" => OnResizeRequested.InvokeAsync(WidthPixels + 16),
        "ArrowRight" => OnResizeRequested.InvokeAsync(WidthPixels - 16),
        "Home" => OnResizeRequested.InvokeAsync(MinimumWidthPixels),
        "End" => OnResizeRequested.InvokeAsync(MaximumWidthPixels),
        _ => Task.CompletedTask
    };

    private async Task BeginPointerResizeAsync(PointerEventArgs args)
    {
        if (args.Button != 0)
        {
            return;
        }

        PointerStartX = args.ClientX;
        PointerStartWidth = WidthPixels;
        await OnResizeRequested.InvokeAsync(WidthPixels);
    }

    private Task ContinuePointerResizeAsync(PointerEventArgs args) => PointerStartX is not { } startX
        ? Task.CompletedTask
        : OnResizeRequested.InvokeAsync(PointerStartWidth - (int)Math.Round(args.ClientX - startX));

    private void EndPointerResize() => PointerStartX = null;
}

/// <summary>Describes one narrow, non-authoritative suggestion card.</summary>
internal sealed record PlannerIdeaCard(
    string Type,
    string Title,
    string Summary,
    string Source,
    string Freshness,
    string Reason,
    string Monogram);
