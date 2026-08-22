using System.Globalization;
using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders the pre-plan choice between manual creation and authorized Journey Templates.</summary>
public partial class PlannerJourneyStarter : ComponentBase
{
    private static readonly IReadOnlyList<OriginTimeZoneOption> OriginTimeZoneOptions =
    [
        new("America/Los_Angeles", "Pacific time"),
        new("America/Phoenix", "Arizona time"),
        new("America/Denver", "Mountain time"),
        new("America/Chicago", "Central time"),
        new("America/New_York", "Eastern time"),
        new("America/Anchorage", "Alaska time"),
        new("Pacific/Honolulu", "Hawaii time")
    ];
    private readonly Dictionary<AdventureTemplateVersionId, string> idempotencyKeys = [];

    /// <summary>Gets or sets the authorized immutable Journey Templates.</summary>
    [Parameter]
    public IReadOnlyList<AdventureTemplateBlueprint> Templates { get; set; } = [];

    /// <summary>Gets or sets the antiforgery-protected template creation path.</summary>
    [Parameter]
    public string CreateFromTemplatePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the requested BCP 47 catalog locale.</summary>
    [Parameter]
    public string Locale { get; set; } = "en-US";

    /// <summary>Gets or sets the callback indicating whether template discovery is active.</summary>
    [Parameter]
    public EventCallback<bool> OnTemplateModeChanged { get; set; }

    /// <summary>Gets or sets whether Journey discovery is initially expanded.</summary>
    [Parameter]
    public bool StartWithIdeasOpen { get; set; }

    /// <summary>Gets or sets the optional exact template identity initially shown in preview.</summary>
    [Parameter]
    public string? InitialPreviewTemplateId { get; set; }

    /// <summary>Gets whether the Journey discovery panel is open.</summary>
    public bool IsBrowsingIdeas { get; private set; }

    private AdventureTemplateBlueprint? SelectedTemplate { get; set; }
    private DateOnly? ConfiguredStartDate { get; set; }
    private string StartDateText { get; set; } = string.Empty;
    private string? StartDateError { get; set; }
    private string OriginName { get; set; } = string.Empty;
    private string OriginTimeZone { get; set; } = string.Empty;
    private int OneWayDistanceMiles { get; set; } = 1300;
    private int DailyDistanceMiles { get; set; } = 450;
    private List<string> OutboundTravelStops { get; } = [];
    private List<string> ReturnTravelStops { get; } = [];
    private bool TravelStopsWereSuggested { get; set; }
    private string? TravelStopSuggestionBasis { get; set; }
    private bool IsReviewReady { get; set; }
    private bool IsConfigurationComplete => ConfiguredStartDate.HasValue
        && (SelectedTemplate?.RequiresConfiguredOrigin != true
            || (!string.IsNullOrWhiteSpace(OriginName)
                && !string.IsNullOrWhiteSpace(OriginTimeZone)
                && OneWayDistanceMiles is >= 25 and <= 10000
                && DailyDistanceMiles is >= 100 and <= 1000
                && OutboundTravelStops.Count == TravelExpansionDays
                && ReturnTravelStops.Count == TravelExpansionDays
                && OutboundTravelStops.All(IsValidTravelStop)
                && ReturnTravelStops.All(IsValidTravelStop)));
    private int TravelDaysEachWay => Math.Max(1,
        (int)Math.Ceiling((double)OneWayDistanceMiles / Math.Max(1, DailyDistanceMiles)));
    private int TravelExpansionDays => SelectedTemplate?.RequiresConfiguredOrigin == true
        ? TravelDaysEachWay - 1
        : 0;
    private int AdaptedDurationDays => SelectedTemplate!.DurationDays + (2 * TravelExpansionDays);
    private int AdaptedItineraryDayCount => SelectedTemplate!.Days.Count + (2 * TravelExpansionDays);
    private int RidingDayCount => SelectedTemplate?.RequiresConfiguredOrigin == true
        ? 2 * TravelDaysEachWay
        : 0;
    private DateOnly JourneyEndDate => ConfiguredStartDate!.Value.AddDays(AdaptedDurationDays - 1);
    /// <inheritdoc />
    protected override void OnInitialized()
    {
        IsBrowsingIdeas = StartWithIdeasOpen;
        if (InitialPreviewTemplateId is not null)
        {
            SelectedTemplate = Templates.FirstOrDefault(candidate =>
                candidate.VersionId.TemplateId == InitialPreviewTemplateId);
            ResetConfiguration();
        }
    }

    private async Task BrowseIdeasAsync()
    {
        IsBrowsingIdeas = true;
        await OnTemplateModeChanged.InvokeAsync(true);
    }

    private async Task StartFromScratchAsync()
    {
        IsBrowsingIdeas = false;
        SelectedTemplate = null;
        await OnTemplateModeChanged.InvokeAsync(false);
    }

    private void PreviewTemplate(AdventureTemplateBlueprint template)
    {
        SelectedTemplate = template;
        ResetConfiguration();
    }

    private void StartDateChanged(ChangeEventArgs args)
    {
        StartDateText = args.Value?.ToString() ?? string.Empty;
        IsReviewReady = false;
        if (DateOnly.TryParseExact(
                StartDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var startDate))
        {
            ConfiguredStartDate = startDate;
            StartDateError = null;
            return;
        }

        ConfiguredStartDate = null;
        StartDateError = string.IsNullOrWhiteSpace(StartDateText)
            ? null
            : "Choose a valid Journey start date.";
    }

    private void ReviewConfiguration()
    {
        if (IsConfigurationComplete)
        {
            IsReviewReady = true;
        }
    }

    private void ConfigurationChanged()
    {
        EnsureTravelStopInputs();
        ApplyTravelStopSuggestion();
        IsReviewReady = false;
    }

    private void TravelStopChanged(
        AdventureTemplateTravelDirection direction,
        int index,
        ChangeEventArgs args)
    {
        var stops = direction == AdventureTemplateTravelDirection.Outbound
            ? OutboundTravelStops
            : ReturnTravelStops;
        stops[index] = args.Value?.ToString() ?? string.Empty;
        TravelStopsWereSuggested = false;
        TravelStopSuggestionBasis = null;
        IsReviewReady = false;
    }

    private void ChangeConfiguration() => IsReviewReady = false;

    private void ResetConfiguration()
    {
        ConfiguredStartDate = null;
        StartDateText = string.Empty;
        StartDateError = null;
        OriginName = string.Empty;
        OriginTimeZone = string.Empty;
        OneWayDistanceMiles = 1300;
        DailyDistanceMiles = 450;
        OutboundTravelStops.Clear();
        ReturnTravelStops.Clear();
        TravelStopsWereSuggested = false;
        TravelStopSuggestionBasis = null;
        EnsureTravelStopInputs();
        IsReviewReady = false;
    }

    private void ApplyTravelStopSuggestion()
    {
        if (SelectedTemplate?.RequiresConfiguredOrigin != true || TravelExpansionDays == 0)
        {
            TravelStopsWereSuggested = false;
            TravelStopSuggestionBasis = null;
            return;
        }

        var suggestion = AdventureTemplateTravelStopSuggestionSelector.Find(
            SelectedTemplate.TravelStopSuggestions,
            OriginName,
            OneWayDistanceMiles,
            DailyDistanceMiles,
            TravelExpansionDays);

        if (suggestion is null)
        {
            if (TravelStopsWereSuggested)
            {
                for (var index = 0; index < TravelExpansionDays; index++)
                {
                    OutboundTravelStops[index] = string.Empty;
                    ReturnTravelStops[index] = string.Empty;
                }
            }

            TravelStopsWereSuggested = false;
            TravelStopSuggestionBasis = null;
            return;
        }

        if (!TravelStopsWereSuggested
            && (OutboundTravelStops.Any(value => !string.IsNullOrWhiteSpace(value))
                || ReturnTravelStops.Any(value => !string.IsNullOrWhiteSpace(value))))
        {
            return;
        }

        for (var index = 0; index < TravelExpansionDays; index++)
        {
            OutboundTravelStops[index] = suggestion.OutboundStops[index];
            ReturnTravelStops[index] = suggestion.ReturnStops[index];
        }

        TravelStopsWereSuggested = true;
        TravelStopSuggestionBasis = suggestion.Basis;
    }

    private void EnsureTravelStopInputs()
    {
        Resize(OutboundTravelStops, TravelExpansionDays);
        Resize(ReturnTravelStops, TravelExpansionDays);

        static void Resize(List<string> values, int count)
        {
            while (values.Count < count)
            {
                values.Add(string.Empty);
            }

            if (values.Count > count)
            {
                values.RemoveRange(count, values.Count - count);
            }
        }
    }

    private static bool IsValidTravelStop(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 200;

    private static string FormatDate(DateOnly date) =>
        date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    private static string TemplateDestinationName(AdventureTemplateDestination destination) =>
        destination.UsesConfiguredOrigin ? "Your starting place" : destination.Name;

    private string ConfiguredDestinationName(AdventureTemplateDestination destination) =>
        destination.UsesConfiguredOrigin ? OriginName : destination.Name;

    private int AdaptedOffset(int offset)
    {
        if (SelectedTemplate?.RequiresConfiguredOrigin != true)
        {
            return offset;
        }

        var lastOriginOffset = SelectedTemplate.Destinations
            .Where(destination => destination.UsesConfiguredOrigin)
            .Max(destination => destination.StartDayOffset);
        return offset >= lastOriginOffset
            ? offset + (2 * TravelExpansionDays)
            : offset > 0
                ? offset + TravelExpansionDays
                : offset;
    }

    private string IdempotencyKey(AdventureTemplateVersionId versionId)
    {
        if (!idempotencyKeys.TryGetValue(versionId, out var key))
        {
            key = $"request_{Guid.NewGuid():N}";
            idempotencyKeys.Add(versionId, key);
        }

        return key;
    }

    private sealed record OriginTimeZoneOption(string Value, string Label);
}
