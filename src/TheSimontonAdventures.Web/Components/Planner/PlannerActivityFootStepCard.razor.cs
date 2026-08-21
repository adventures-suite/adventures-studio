using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders one authorized Activity FootStep as a review-first draggable card.</summary>
public partial class PlannerActivityFootStepCard
{
    /// <summary>Gets or sets the authorized Activity FootStep projection.</summary>
    [Parameter, EditorRequired]
    public PlannerFootStepDefinition FootStep { get; set; } = null!;

    /// <summary>Gets or sets the authorized itinerary-day targets for this card.</summary>
    [Parameter]
    public IReadOnlyList<PlannerActivityTarget> Targets { get; set; } = [];

    /// <summary>Gets or sets whether the actor may edit the current plan.</summary>
    [Parameter]
    public bool CanEdit { get; set; }

    /// <summary>Gets or sets whether pointer dragging is available as an enhancement.</summary>
    [Parameter]
    public bool IsDraggable { get; set; }

    /// <summary>Gets or sets the authoritative plan version rendered into the review form.</summary>
    [Parameter]
    public long ExpectedVersion { get; set; }

    /// <summary>Gets or sets the antiforgery-protected activity endpoint.</summary>
    [Parameter]
    public string AddActivityPath { get; set; } = string.Empty;

    private string DraggableValue => IsDraggable ? "true" : "false";
    private string? PrimaryPlace => FootStep.Places.Select(DisplayValue).FirstOrDefault();
    private string? PrimaryCategory => FootStep.Categories.Select(DisplayValue).FirstOrDefault();
    private string TargetSummary => Targets.Count switch
    {
        0 => "Select an itinerary day to use this FootStep",
        1 => $"Suggested for {Targets[0].Label}",
        _ => $"Fits {Targets.Count} itinerary days in the selected destination"
    };
    private string? SuggestedTime => FootStep.ActivityDraft switch
    {
        { SuggestedStartTime: { } start, SuggestedEndTime: { } end } =>
            $"{FormatTime(start)}–{FormatTime(end)}",
        { SuggestedStartTime: { } start } => $"From {FormatTime(start)}",
        { SuggestedEndTime: { } end } => $"Until {FormatTime(end)}",
        _ => null
    };

    private static string FormatTime(TimeOnly value) => value.ToString("h:mm tt");

    private static string DisplayValue(string value) =>
        string.Join(' ', value.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
