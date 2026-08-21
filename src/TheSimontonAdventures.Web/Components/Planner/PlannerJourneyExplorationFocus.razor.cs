using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Describes one provider-neutral geographic node used for Planner discovery.</summary>
public sealed record PlannerExplorationArea
{
    /// <summary>Initializes a geographic discovery node.</summary>
    public PlannerExplorationArea(
        string id,
        string name,
        string? parentId = null,
        string? mapLabel = null,
        string? mapPath = null,
        int mapLabelX = 0,
        int mapLabelY = 0,
        string? mapAssetId = null) =>
        (Id, Name, ParentId, MapLabel, MapPath, MapLabelX, MapLabelY, MapAssetId) =
        (id, name, parentId, mapLabel, mapPath, mapLabelX, mapLabelY, mapAssetId);

    /// <summary>Gets the stable geographic discovery identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the localized display label supplied to the component.</summary>
    public string Name { get; }

    /// <summary>Gets the parent identifier, or <see langword="null"/> for a world-map region.</summary>
    public string? ParentId { get; }

    /// <summary>Gets the concise label rendered on the world map.</summary>
    public string? MapLabel { get; }

    /// <summary>Gets the optional presentation-only SVG path for a world-map region.</summary>
    public string? MapPath { get; }

    /// <summary>Gets the horizontal coordinate for the world-map label.</summary>
    public int MapLabelX { get; }

    /// <summary>Gets the vertical coordinate for the world-map label.</summary>
    public int MapLabelY { get; }

    /// <summary>Gets the optional identifier for public-domain geography in the shared SVG map asset.</summary>
    public string? MapAssetId { get; }
}

/// <summary>Lets a Planner set multiple transient geographic branches for Journey discovery.</summary>
public partial class PlannerJourneyExplorationFocus : ComponentBase
{
    internal static readonly IReadOnlyList<PlannerExplorationArea> DefaultAreas =
    [
        new("north-america", "North America", mapLabel: "N. America", mapLabelX: 136, mapLabelY: 104, mapAssetId: "north-america"),
        new("latin-america", "Latin America", mapLabel: "Latin America", mapLabelX: 220, mapLabelY: 222, mapAssetId: "latin-america"),
        new("europe", "Europe", mapLabel: "Europe", mapLabelX: 374, mapLabelY: 82, mapAssetId: "europe"),
        new("africa-middle-east", "Africa & Middle East", mapLabel: "Africa + M.E.", mapLabelX: 403, mapLabelY: 170, mapAssetId: "africa-middle-east"),
        new("asia-pacific", "Asia-Pacific", mapLabel: "Asia-Pacific", mapLabelX: 535, mapLabelY: 112, mapAssetId: "asia-pacific"),
        new("polar-remote", "Polar & remote", mapLabel: "Polar", mapLabelX: 344, mapLabelY: 336, mapAssetId: "polar-remote"),
        new("mediterranean", "Mediterranean", mapLabel: "Mediterranean", mapLabelX: 385, mapLabelY: 113, mapAssetId: "mediterranean"),
        new("caribbean", "Caribbean", mapLabel: "Caribbean", mapLabelX: 252, mapLabelY: 142, mapAssetId: "caribbean"),
        new("united-states", "United States", "north-america"),
        new("canada", "Canada", "north-america"),
        new("mexico", "Mexico", "north-america"),
        new("greenland", "Greenland", "north-america"),
        new("us-southeast", "Southeast", "united-states"),
        new("us-northeast", "Northeast", "united-states"),
        new("us-midwest", "Midwest", "united-states"),
        new("us-southwest", "Southwest", "united-states"),
        new("us-mountain-west", "Mountain West", "united-states"),
        new("us-pacific", "Pacific", "united-states"),
        new("central-america", "Central America", "latin-america"),
        new("south-america", "South America", "latin-america"),
        new("southern-cone", "Southern Cone", "south-america"),
        new("andean-region", "Andean region", "south-america"),
        new("northern-europe", "Northern Europe", "europe"),
        new("western-europe", "Western Europe", "europe"),
        new("central-europe", "Central Europe", "europe"),
        new("eastern-europe", "Eastern Europe", "europe"),
        new("southern-europe", "Southern Europe", "europe"),
        new("eastern-caribbean", "Eastern Caribbean", "caribbean"),
        new("western-caribbean", "Western Caribbean", "caribbean"),
        new("southern-caribbean", "Southern Caribbean", "caribbean"),
        new("western-mediterranean", "Western Mediterranean", "mediterranean"),
        new("eastern-mediterranean", "Eastern Mediterranean", "mediterranean"),
        new("adriatic", "Adriatic", "mediterranean"),
        new("east-asia", "East Asia", "asia-pacific"),
        new("southeast-asia", "Southeast Asia", "asia-pacific"),
        new("south-asia", "South Asia", "asia-pacific"),
        new("oceania", "Oceania", "asia-pacific"),
        new("africa", "Africa", "africa-middle-east"),
        new("middle-east", "Middle East", "africa-middle-east")
    ];

    private HashSet<string> SelectedAreaIds { get; } = new(StringComparer.Ordinal);
    private string? CurrentParentId { get; set; }
    private bool IsExpanded { get; set; } = true;
    private bool AutoHideAfterSelection { get; set; }

    /// <summary>Gets or sets the geographic hierarchy used by the discovery control.</summary>
    [Parameter]
    public IReadOnlyList<PlannerExplorationArea> Areas { get; set; } = DefaultAreas;

    /// <summary>Gets or sets the callback raised when transient geographic selections change.</summary>
    [Parameter]
    public EventCallback<IReadOnlySet<string>> OnSelectionChanged { get; set; }

    /// <summary>Gets the currently selected transient geographic identifiers.</summary>
    public IReadOnlySet<string> SelectedRegions => SelectedAreaIds;

    private IReadOnlyList<PlannerExplorationArea> RootAreas => Areas.Where(area => area.ParentId is null).ToArray();
    private IReadOnlyList<PlannerExplorationArea> CurrentChildren => Areas
        .Where(area => string.Equals(area.ParentId, CurrentParentId, StringComparison.Ordinal))
        .ToArray();
    private IReadOnlyList<PlannerExplorationArea> SelectedAreas => Areas.Where(area => SelectedAreaIds.Contains(area.Id)).ToArray();
    private IReadOnlyList<PlannerExplorationArea> CurrentAncestors => CurrentParentId is null ? [] : AncestorsAndSelf(CurrentParentId);
    private string CurrentPrompt => CurrentParentId is null
        ? "Choose one or more world regions"
        : $"Refine {Find(CurrentParentId)?.Name ?? "this region"}";
    private string SelectionSummary => SelectedAreaIds.Count == 0
        ? "Exploring anywhere. Your Journey remains unchanged."
        : $"Exploring {string.Join(", ", SelectedAreas.Select(area => PathLabel(area.Id)))}. Your Journey remains unchanged.";
    private string CompactSelectionSummary => SelectedAreaIds.Count == 0
        ? "Exploring anywhere"
        : $"Exploring: {string.Join(" + ", SelectedAreas.Select(area => PathLabel(area.Id)))}";
    private string SectionClasses => $"planner-exploration{(IsExpanded ? string.Empty : " planner-exploration--collapsed")}";

    private PlannerExplorationArea? Find(string id) => Areas.FirstOrDefault(area => area.Id == id);
    private bool HasChildren(string id) => Areas.Any(area => area.ParentId == id);
    private bool BranchHasSelection(string id) => SelectedAreaIds.Any(selectedId => IsSameOrDescendant(selectedId, id));
    private string RegionClasses(PlannerExplorationArea region) =>
        $"planner-exploration__region{(BranchHasSelection(region.Id) ? " planner-exploration__region--selected" : string.Empty)}";

    private async Task SelectAndExplore(string areaId)
    {
        UpdateSelectionFor(areaId);
        if (HasChildren(areaId))
        {
            CurrentParentId = areaId;
        }

        if (AutoHideAfterSelection)
        {
            IsExpanded = false;
        }

        await NotifySelectionChangedAsync();
    }

    private void UpdateSelectionFor(string areaId)
    {
        if (SelectedAreaIds.Remove(areaId))
        {
            return;
        }

        if (SelectedAreaIds.Any(selectedId => selectedId != areaId && IsSameOrDescendant(selectedId, areaId)))
        {
            return;
        }

        var selectedAncestors = SelectedAreaIds.Where(selectedId => IsSameOrDescendant(areaId, selectedId)).ToArray();
        foreach (var selectedAncestor in selectedAncestors)
        {
            SelectedAreaIds.Remove(selectedAncestor);
        }

        SelectedAreaIds.Add(areaId);
    }

    private async Task RemoveSelection(string areaId)
    {
        SelectedAreaIds.Remove(areaId);
        await NotifySelectionChangedAsync();
    }

    private async Task ClearSelection()
    {
        SelectedAreaIds.Clear();
        CurrentParentId = null;
        IsExpanded = true;
        await NotifySelectionChangedAsync();
    }

    private void ToggleExpanded() => IsExpanded = !IsExpanded;
    private void Expand() => IsExpanded = true;
    private void ChangeAutoHide(ChangeEventArgs args)
    {
        AutoHideAfterSelection = args.Value is true;
        if (AutoHideAfterSelection && SelectedAreaIds.Count > 0)
        {
            IsExpanded = false;
        }
    }

    private void ExploreWorld() => CurrentParentId = null;
    private void Explore(string areaId) => CurrentParentId = areaId;
    private Task NotifySelectionChangedAsync() =>
        OnSelectionChanged.InvokeAsync(SelectedAreaIds.ToHashSet(StringComparer.Ordinal));

    private async Task HandleRegionKey(KeyboardEventArgs args, string regionId)
    {
        if (args.Key is "Enter" or " ")
        {
            await SelectAndExplore(regionId);
        }
    }

    internal static bool AreaIsSameOrDescendant(
        IReadOnlyList<PlannerExplorationArea> areas,
        string candidateId,
        string ancestorId)
    {
        PlannerExplorationArea? FindArea(string id) => areas.FirstOrDefault(area => area.Id == id);
        for (var current = FindArea(candidateId); current is not null; current = current.ParentId is null ? null : FindArea(current.ParentId))
        {
            if (current.Id == ancestorId)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSameOrDescendant(string candidateId, string ancestorId) =>
        AreaIsSameOrDescendant(Areas, candidateId, ancestorId);

    private IReadOnlyList<PlannerExplorationArea> AncestorsAndSelf(string areaId)
    {
        var path = new List<PlannerExplorationArea>();
        for (var current = Find(areaId); current is not null; current = current.ParentId is null ? null : Find(current.ParentId))
        {
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private string PathLabel(string areaId) => string.Join(" → ", AncestorsAndSelf(areaId).Select(area => area.Name));
}
