using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using TheSimontonAdventures.Web.Planning;

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

/// <summary>Defines deterministic FootStep presentation ordering.</summary>
public enum PlannerFootStepSort
{
    /// <summary>Uses the authorized catalog's deterministic order.</summary>
    Catalog,
    /// <summary>Orders by localized title.</summary>
    Title,
    /// <summary>Orders shortest duration first.</summary>
    ShortestDuration,
    /// <summary>Orders longest duration first.</summary>
    LongestDuration,
    /// <summary>Orders by attribution and then title.</summary>
    Source
}

/// <summary>Defines optional presentation-only FootStep grouping.</summary>
public enum PlannerFootStepGrouping
{
    /// <summary>Does not add group headings.</summary>
    None,
    /// <summary>Groups by FootStep kind.</summary>
    Kind,
    /// <summary>Groups by the item's transportation-mode combination.</summary>
    Transportation,
    /// <summary>Groups by the first ordinal category.</summary>
    Category,
    /// <summary>Groups by source attribution.</summary>
    Source
}

/// <summary>Defines the visual density of FootStep results.</summary>
public enum PlannerFootStepView
{
    /// <summary>Shows visual cards.</summary>
    Cards,
    /// <summary>Shows a compact list.</summary>
    List,
    /// <summary>Shows aligned columns and rows in an accessible table.</summary>
    Tabular
}

/// <summary>Renders a responsive, presentation-only projection beside the authoritative itinerary.</summary>
public partial class PlannerContextualIdeasRail : ComponentBase, IAsyncDisposable
{
    private static readonly IReadOnlyList<int> RailPageSizeOptions = [1, 2, 3];
    private const string PageSizePreferenceKey = "adventures-suite.planner.footsteps.rail-page-size";
    /// <summary>Gets the minimum supported FootSteps rail width in pixels.</summary>
    public const int MinimumWidthPixels = 272;
    /// <summary>Gets the maximum supported FootSteps rail width in pixels.</summary>
    public const int MaximumWidthPixels = 960;
    /// <summary>Gets the width adjustment applied by the visible controls and arrow keys.</summary>
    public const int ResizeStepPixels = 64;

    private double? PointerStartX { get; set; }
    private int PointerStartWidth { get; set; }
    private bool FocusCloseAfterRender { get; set; }
    private bool FocusOpenAfterRender { get; set; }
    private IJSObjectReference? PreferenceModule { get; set; }
    private DotNetObjectReference<PlannerContextualIdeasRail>? DragDropReference { get; set; }

    [Inject]
    private IJSRuntime JavaScript { get; set; } = null!;
    private string? PreviousContextKey { get; set; }
    private string? SelectedType { get; set; }
    private HashSet<string> SelectedFacets { get; } = new(StringComparer.Ordinal);
    private HashSet<string> SelectedExplorationAreas { get; } = new(StringComparer.Ordinal);
    private int? MinimumDays { get; set; }
    private int? MaximumDays { get; set; }
    private int PageSize { get; set; } = 3;
    private int CurrentPage { get; set; } = 1;
    private PlannerFootStepSort SortBy { get; set; } = PlannerFootStepSort.Catalog;
    private PlannerFootStepGrouping GroupBy { get; set; }
    private PlannerFootStepView ViewType { get; set; } = PlannerFootStepView.Cards;
    private Dictionary<string, string> ApplicationKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the selected authorized canvas context.</summary>
    [Parameter]
    public PlannerIdeasContext? Context { get; set; }
    /// <summary>Gets or sets the already authorized FootStep projections.</summary>
    [Parameter]
    public IReadOnlyList<PlannerFootStepDefinition> AuthorizedItems { get; set; } = [];
    /// <summary>Gets or sets whether the authorized actor may apply a supported FootStep.</summary>
    [Parameter]
    public bool CanEdit { get; set; }
    /// <summary>Gets or sets the authoritative plan version rendered into application forms.</summary>
    [Parameter]
    public long ExpectedVersion { get; set; }
    /// <summary>Gets or sets the plan's inclusive start date.</summary>
    [Parameter]
    public DateOnly PlanStartDate { get; set; }
    /// <summary>Gets or sets the plan's inclusive end date.</summary>
    [Parameter]
    public DateOnly PlanEndDate { get; set; }
    /// <summary>Gets or sets the protected Destination FootStep application path.</summary>
    [Parameter]
    public string ApplyDestinationPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the protected manual activity creation path.</summary>
    [Parameter]
    public string AddActivityPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the authorized itinerary-day targets for the selected context.</summary>
    [Parameter]
    public IReadOnlyList<PlannerActivityTarget> ActivityTargets { get; set; } = [];
    /// <summary>Gets or sets safe Post/Redirect/Get feedback for FootStep application.</summary>
    [Parameter]
    public string? ApplicationStatusMessage { get; set; }
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
    /// <summary>Gets or sets the callback raised when an applicable destination FootStep begins dragging.</summary>
    [Parameter]
    public EventCallback<PlannerFootStepDefinition> OnDestinationDragStarted { get; set; }
    /// <summary>Gets or sets the callback raised when destination FootStep dragging ends.</summary>
    [Parameter]
    public EventCallback OnDestinationDragEnded { get; set; }
    /// <summary>Gets or sets the callback raised after a dropped catalog item is revalidated.</summary>
    [Parameter]
    public EventCallback<PlannerFootStepDefinition> OnDestinationDropped { get; set; }
    /// <summary>Gets or sets the callback raised when an applicable Activity FootStep begins dragging.</summary>
    [Parameter]
    public EventCallback<PlannerFootStepDefinition> OnActivityDragStarted { get; set; }
    /// <summary>Gets or sets the callback raised when Activity FootStep dragging ends.</summary>
    [Parameter]
    public EventCallback OnActivityDragEnded { get; set; }
    /// <summary>Gets or sets the callback raised after an Activity FootStep is dropped on an authorized day.</summary>
    [Parameter]
    public EventCallback<PlannerActivityFootStepDrop> OnActivityDropped { get; set; }
    /// <summary>Gets whether the desktop rail is collapsed.</summary>
    public bool IsCollapsed { get; private set; }
    /// <summary>Gets whether the narrow-screen drawer is open.</summary>
    public bool IsDrawerOpen { get; private set; }
    /// <summary>Gets the effective state after Development-only source gating.</summary>
    public PlannerIdeasState EffectiveState => StateOverride ?? (Context is null
        ? PlannerIdeasState.NoSelection
        : AuthorizedItems.Count > 0 ? PlannerIdeasState.Populated : PlannerIdeasState.Empty);
    /// <summary>Gets the authorized cards supplied by the application query boundary.</summary>
    internal IReadOnlyList<PlannerFootStepDefinition> Ideas =>
        EffectiveState == PlannerIdeasState.Populated ? AuthorizedItems : [];
    /// <summary>Gets the FootStep types available for the selected context.</summary>
    internal IReadOnlyList<string> AvailableTypes => Ideas
        .Select(idea => idea.Kind)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    /// <summary>Gets the FootSteps matching the transient type filter.</summary>
    internal IReadOnlyList<PlannerFootStepDefinition> FilteredIdeas => SelectedType is null
        ? Ideas
        : Ideas.Where(idea => idea.Kind == SelectedType).ToArray();
    /// <summary>Gets the cards matching every selected stable facet.</summary>
    internal IReadOnlyList<PlannerFootStepDefinition> GeographicallyFilteredIdeas => SelectedExplorationAreas.Count == 0
        ? FilteredIdeas
        : FilteredIdeas.Where(MatchesExplorationArea).ToArray();
    /// <summary>Gets the cards matching every selected stable facet.</summary>
    internal IReadOnlyList<PlannerFootStepDefinition> FacetedIdeas => GeographicallyFilteredIdeas
        .Where(MatchesSelectedFacets)
        .Where(item => !MinimumDays.HasValue || item.DurationDays >= MinimumDays)
        .Where(item => !MaximumDays.HasValue || item.DurationDays <= MaximumDays)
        .ToArray();
    /// <summary>Gets the FootSteps visible on the selected page.</summary>
    internal IReadOnlyList<PlannerFootStepDefinition> SortedIdeas => SortBy switch
    {
        PlannerFootStepSort.Title => FacetedIdeas.OrderBy(item => item.Title, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        PlannerFootStepSort.ShortestDuration => FacetedIdeas.OrderBy(item => item.DurationDays ?? int.MaxValue).ThenBy(item => item.Title, StringComparer.Ordinal).ToArray(),
        PlannerFootStepSort.LongestDuration => FacetedIdeas.OrderByDescending(item => item.DurationDays ?? int.MinValue).ThenBy(item => item.Title, StringComparer.Ordinal).ToArray(),
        PlannerFootStepSort.Source => FacetedIdeas.OrderBy(item => item.Attribution, StringComparer.Ordinal).ThenBy(item => item.Title, StringComparer.Ordinal).ToArray(),
        _ => SelectedExplorationAreas.Count == 0
            ? FacetedIdeas
            : FacetedIdeas.OrderByDescending(GeographicMatchScore)
                .ThenBy(item => item.Title, StringComparer.Ordinal).ToArray()
    };
    /// <summary>Gets the FootSteps visible on the selected page.</summary>
    internal IReadOnlyList<PlannerFootStepDefinition> PagedIdeas => SortedIdeas
        .Skip((CurrentPage - 1) * PageSize)
        .Take(PageSize)
        .ToArray();
    /// <summary>Gets presentation groups over only the current authorized page.</summary>
    internal IReadOnlyList<PlannerFootStepGroup> PagedGroups => PagedIdeas
        .GroupBy(GroupKey, StringComparer.Ordinal)
        .OrderBy(group => GroupBy == PlannerFootStepGrouping.None ? 0 : 1)
        .ThenBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new PlannerFootStepGroup(
            GroupBy == PlannerFootStepGrouping.None ? null : DisplayFacet(group.Key), group.ToArray()))
        .ToArray();
    /// <summary>Gets the CSS classes representing rail state.</summary>
    public string RailClasses => $"planner-ideas{(IsCollapsed ? " planner-ideas--collapsed" : string.Empty)}{(IsDrawerOpen ? " planner-ideas--drawer-open" : string.Empty)}{(ViewType == PlannerFootStepView.List ? " planner-ideas--list-view" : string.Empty)}{(ViewType == PlannerFootStepView.Tabular ? " planner-ideas--tabular-view" : string.Empty)}";

    private ElementReference OpenButton { get; set; }
    private ElementReference CloseButton { get; set; }
    private ElementReference RailElement { get; set; }
    private void ToggleCollapsed() => IsCollapsed = !IsCollapsed;
    private void SelectType(string? type)
    {
        SelectedType = type;
        CurrentPage = 1;
    }
    private Task ShowWholeAdventureAsync() => OnAdventureContextRequested.InvokeAsync();
    private bool CanDrag(PlannerFootStepDefinition item) => CanDragDestination(item) || CanDragActivity(item);
    private bool CanDragDestination(PlannerFootStepDefinition item) =>
        CanEdit && item.DestinationDraft is not null && !string.IsNullOrWhiteSpace(ApplyDestinationPath);
    private bool CanDragActivity(PlannerFootStepDefinition item) =>
        CanEdit && item.ActivityDraft is not null && ActivityTargets.Count > 0 && !string.IsNullOrWhiteSpace(AddActivityPath);
    private string DragKind(PlannerFootStepDefinition item) => CanDragDestination(item) ? "destination" : CanDragActivity(item) ? "activity" : string.Empty;
    private string DraggableValue(PlannerFootStepDefinition item) => CanDrag(item) ? "true" : "false";
    private Task BeginDestinationDragAsync(PlannerFootStepDefinition item) =>
        CanDrag(item) ? OnDestinationDragStarted.InvokeAsync(item) : Task.CompletedTask;
    private Task EndDestinationDragAsync() => OnDestinationDragEnded.InvokeAsync();

    /// <summary>Revalidates an untrusted browser drop against authorized destination FootSteps.</summary>
    [JSInvokable]
    public Task HandleDestinationFootStepDropAsync(string footStepId)
    {
        var item = AuthorizedItems.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, footStepId, StringComparison.Ordinal));
        return item is not null && CanDrag(item)
            ? OnDestinationDropped.InvokeAsync(item)
            : Task.CompletedTask;
    }

    /// <summary>Revalidates a pointer drag start before exposing a drop target.</summary>
    [JSInvokable]
    public Task HandleDestinationFootStepDragStartedAsync(string footStepId)
    {
        var item = AuthorizedItems.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, footStepId, StringComparison.Ordinal));
        return item is not null && CanDrag(item)
            ? OnDestinationDragStarted.InvokeAsync(item)
            : Task.CompletedTask;
    }

    /// <summary>Ends the transient pointer drag presentation without changing the Journey.</summary>
    [JSInvokable]
    public Task HandleDestinationFootStepDragEndedAsync() => OnDestinationDragEnded.InvokeAsync();

    /// <summary>Revalidates an untrusted Activity FootStep drop and its authorized day target.</summary>
    [JSInvokable]
    public Task HandleActivityFootStepDropAsync(string footStepId, string itineraryDayId)
    {
        var item = AuthorizedItems.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, footStepId, StringComparison.Ordinal));
        var target = ActivityTargets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, itineraryDayId, StringComparison.Ordinal));
        return item is not null && target is not null && CanDragActivity(item)
            ? OnActivityDropped.InvokeAsync(new PlannerActivityFootStepDrop(item, target))
            : Task.CompletedTask;
    }

    /// <summary>Revalidates an Activity FootStep drag start before exposing day targets.</summary>
    [JSInvokable]
    public Task HandleActivityFootStepDragStartedAsync(string footStepId)
    {
        var item = AuthorizedItems.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, footStepId, StringComparison.Ordinal));
        return item is not null && CanDragActivity(item)
            ? OnActivityDragStarted.InvokeAsync(item)
            : Task.CompletedTask;
    }

    /// <summary>Ends transient Activity FootStep drag presentation without changing the plan.</summary>
    [JSInvokable]
    public Task HandleActivityFootStepDragEndedAsync() => OnActivityDragEnded.InvokeAsync();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var contextKey = Context is null ? null : $"{Context.Kind}:{Context.Id}";
        if (!string.Equals(contextKey, PreviousContextKey, StringComparison.Ordinal))
        {
            PreviousContextKey = contextKey;
            SelectedType = null;
            SelectedFacets.Clear();
            SelectedExplorationAreas.Clear();
            MinimumDays = null;
            MaximumDays = null;
            SortBy = PlannerFootStepSort.Catalog;
            GroupBy = PlannerFootStepGrouping.None;
            ApplicationKeys.Clear();
            CurrentPage = 1;
        }
    }
    private IReadOnlyList<PlannerFacetOption> AvailableFacets => Ideas.SelectMany(Facets)
        .Distinct().OrderBy(option => option.Group, StringComparer.Ordinal)
        .ThenBy(option => option.Value, StringComparer.Ordinal).ToArray();
    private IReadOnlyList<PlannerFacetGroup> AvailableFacetGroups => AvailableFacets
        .GroupBy(option => option.Group, StringComparer.Ordinal)
        .Select(group => new PlannerFacetGroup(group.Key, group.ToArray()))
        .ToArray();
    private void ToggleFacet(PlannerFacetOption facet)
    {
        if (!SelectedFacets.Add(facet.Key))
        {
            SelectedFacets.Remove(facet.Key);
        }
        CurrentPage = 1;
    }
    private void ClearFilters()
    {
        SelectedType = null;
        SelectedFacets.Clear();
        MinimumDays = null;
        MaximumDays = null;
        CurrentPage = 1;
    }
    private void ChangeMinimumDays(ChangeEventArgs args) => ChangeDuration(args, true);
    private void ChangeMaximumDays(ChangeEventArgs args) => ChangeDuration(args, false);
    private void ChangeDuration(ChangeEventArgs args, bool minimum)
    {
        var value = int.TryParse(args.Value?.ToString(), out var parsed) && parsed is >= 1 and <= 365
            ? parsed : (int?)null;
        if (minimum) MinimumDays = value; else MaximumDays = value;
        CurrentPage = 1;
    }
    private void ChangeSort(ChangeEventArgs args)
    {
        if (Enum.TryParse<PlannerFootStepSort>(args.Value?.ToString(), out var value)) SortBy = value;
        CurrentPage = 1;
    }
    private void ChangeGrouping(ChangeEventArgs args)
    {
        if (Enum.TryParse<PlannerFootStepGrouping>(args.Value?.ToString(), out var value)) GroupBy = value;
    }
    private void ChangeView(PlannerFootStepView view) => ViewType = view;
    private Task ChangeExplorationAreasAsync(IReadOnlySet<string> selectedAreas)
    {
        SelectedExplorationAreas.Clear();
        SelectedExplorationAreas.UnionWith(selectedAreas);
        CurrentPage = 1;
        return Task.CompletedTask;
    }
    private string GroupKey(PlannerFootStepDefinition item) => GroupBy switch
    {
        PlannerFootStepGrouping.Kind => item.Kind,
        PlannerFootStepGrouping.Transportation => GroupValues(item.TransportationModes),
        PlannerFootStepGrouping.Category => GroupValues(item.Categories),
        PlannerFootStepGrouping.Source => item.Attribution,
        _ => "All FootSteps"
    };
    private static string GroupValues(IEnumerable<string> values)
    {
        var ordered = values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return ordered.Length == 0 ? "Other" : string.Join(" + ", ordered);
    }
    private bool MatchesSelectedFacets(PlannerFootStepDefinition item) => SelectedFacets
        .Select(PlannerFacetOption.Parse).GroupBy(option => option.Group, StringComparer.Ordinal)
        .All(group => group.Any(selected => Facets(item).Contains(selected)));
    private static IReadOnlySet<PlannerFacetOption> Facets(PlannerFootStepDefinition item) =>
        Options("place", item.Places).Concat(Options("transportation", item.TransportationModes))
        .Concat(Options("category", item.Categories)).Concat(Options("route style", item.RouteStyles))
        .Concat(Options("surface", item.Surfaces)).Concat(Options("accessibility", item.Accessibility))
        .Concat(Options("pace", item.Paces)).Concat(Options("season", item.Seasons))
        .Concat(Options("equipment", item.EquipmentNeeds)).Concat(Options("budget", item.BudgetBands))
        .Concat(Options("travelers", item.TravelerCompositions)).Concat(Options("source", item.SourceClasses))
        .Concat(Options("language", item.Languages)).ToHashSet();
    private static IEnumerable<PlannerFacetOption> Options(string group, IEnumerable<string> values) =>
        values.Select(value => new PlannerFacetOption(group, value));
    private static string DisplayFacet(string value) => value switch
    {
        "journey-pattern" => "Journey Blueprint",
        "route-pattern" => "Route Style",
        _ => string.Join(' ', value.Split('-'))
    };
    private static string DisplayValues(IEnumerable<string> values)
    {
        var labels = values.OrderBy(value => value, StringComparer.Ordinal).Select(DisplayFacet).ToArray();
        return labels.Length == 0 ? "—" : string.Join(", ", labels);
    }
    private static string DisplayDuration(int? days) => days switch
    {
        1 => "1 day",
        > 1 => $"{days} days",
        _ => "—"
    };
    private static string Monogram(PlannerFootStepDefinition item) =>
        string.Concat(item.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));
    private string ApplicationKey(PlannerFootStepDefinition item)
    {
        if (!ApplicationKeys.TryGetValue(item.Id, out var key))
        {
            key = $"footstep_{Guid.NewGuid():N}";
            ApplicationKeys[item.Id] = key;
        }
        return key;
    }
    private DateOnly SuggestedEnd(PlannerFootStepDefinition item)
    {
        var candidate = item.DurationDays is > 1
            ? PlanStartDate.AddDays(item.DurationDays.Value - 1)
            : PlanStartDate;
        return candidate > PlanEndDate ? PlanEndDate : candidate;
    }
    private string ContextReason => Context?.Kind switch
    {
        PlannerIdeasContextKind.Day => "Fits the selected itinerary day.",
        PlannerIdeasContextKind.Destination => "Matches the selected destination visit.",
        _ => "Helps develop the whole Adventure."
    };
    private string EmptyMatchMessage => SelectedExplorationAreas.Count == 0
        ? "Your plan is unchanged. Remove a filter or clear all filters to see more inspiration."
        : "Your plan is unchanged. Explore another region or use Add destination to enter a place manually.";
    private bool MatchesExplorationArea(PlannerFootStepDefinition item) =>
        SelectedExplorationAreas.Any(selectedArea => item.Places.Any(place =>
            PlannerJourneyExplorationFocus.AreaIsSameOrDescendant(
                PlannerJourneyExplorationFocus.DefaultAreas, place, selectedArea)));
    private int GeographicMatchScore(PlannerFootStepDefinition item) => item.Places
        .SelectMany(place => SelectedExplorationAreas.Select(selected => GeographicMatchScore(place, selected)))
        .DefaultIfEmpty(0)
        .Max();
    private static int GeographicMatchScore(string place, string selected) =>
        string.Equals(place, selected, StringComparison.Ordinal) ? 3
        : PlannerJourneyExplorationFocus.AreaIsSameOrDescendant(
            PlannerJourneyExplorationFocus.DefaultAreas, place, selected) ? 2
        : 0;
    private string WhyThisFootStep(PlannerFootStepDefinition item)
    {
        if (SelectedExplorationAreas.Count == 0)
        {
            return ContextReason;
        }

        var matches = SelectedExplorationAreas
            .Where(selected => item.Places.Any(place => PlannerJourneyExplorationFocus.AreaIsSameOrDescendant(
                PlannerJourneyExplorationFocus.DefaultAreas, place, selected)))
            .Select(selected => PlannerJourneyExplorationFocus.DefaultAreas.First(area => area.Id == selected).Name)
            .ToArray();
        return $"Matches your {string.Join(" and ", matches)} exploration focus.";
    }
    private async Task ChangePageSizeAsync(int pageSize)
    {
        PageSize = pageSize;
        CurrentPage = 1;
        if (PreferenceModule is not null)
        {
            await PreferenceModule.InvokeVoidAsync("writePageSize", PageSizePreferenceKey, pageSize);
        }
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
        if (firstRender)
        {
            PreferenceModule = await JavaScript.InvokeAsync<IJSObjectReference>(
                "import", "./js/plannerPreferences.js");
            DragDropReference = DotNetObjectReference.Create(this);
            await PreferenceModule.InvokeVoidAsync(
                "enableDestinationDragDrop", RailElement, DragDropReference);
            var savedPageSize = await PreferenceModule.InvokeAsync<int?>(
                "readPageSize", PageSizePreferenceKey);
            if (savedPageSize is { } value && RailPageSizeOptions.Contains(value) && value != PageSize)
            {
                PageSize = value;
                CurrentPage = 1;
                await InvokeAsync(StateHasChanged);
            }
        }

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
        "ArrowLeft" => OnResizeRequested.InvokeAsync(WidthPixels + ResizeStepPixels),
        "ArrowRight" => OnResizeRequested.InvokeAsync(WidthPixels - ResizeStepPixels),
        "Home" => OnResizeRequested.InvokeAsync(MinimumWidthPixels),
        "End" => OnResizeRequested.InvokeAsync(MaximumWidthPixels),
        _ => Task.CompletedTask
    };

    private Task NarrowRailAsync() =>
        OnResizeRequested.InvokeAsync(Math.Max(MinimumWidthPixels, WidthPixels - ResizeStepPixels));

    private Task WidenRailAsync() =>
        OnResizeRequested.InvokeAsync(Math.Min(MaximumWidthPixels, WidthPixels + ResizeStepPixels));

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

    /// <summary>Releases the browser preference module owned by this component.</summary>
    public async ValueTask DisposeAsync()
    {
        if (PreferenceModule is not null)
        {
            try
            {
                await PreferenceModule.InvokeVoidAsync("disableDestinationDragDrop", RailElement);
                await PreferenceModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Browser teardown already owns the disconnected module.
            }
        }

        DragDropReference?.Dispose();

        GC.SuppressFinalize(this);
    }
}

internal sealed record PlannerFacetOption(string Group, string Value)
{
    public string Key => $"{Group}:{Value}";
    public static PlannerFacetOption Parse(string key)
    {
        var separator = key.IndexOf(':');
        return new(key[..separator], key[(separator + 1)..]);
    }
}

internal sealed record PlannerFacetGroup(
    string Name,
    IReadOnlyList<PlannerFacetOption> Options);

internal sealed record PlannerFootStepGroup(
    string? Label,
    IReadOnlyList<PlannerFootStepDefinition> Items);
