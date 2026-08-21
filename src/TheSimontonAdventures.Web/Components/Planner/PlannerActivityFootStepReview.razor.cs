using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders a review-first form that seeds the existing authorized activity mutation.</summary>
public partial class PlannerActivityFootStepReview
{
    /// <summary>Gets or sets the authorized FootStep proposal.</summary>
    [Parameter, EditorRequired]
    public PlannerFootStepDefinition FootStep { get; set; } = null!;

    /// <summary>Gets or sets the authorized itinerary-day targets for the selected context.</summary>
    [Parameter]
    public IReadOnlyList<PlannerActivityTarget> Targets { get; set; } = [];

    /// <summary>Gets or sets whether the actor may edit the plan.</summary>
    [Parameter]
    public bool CanEdit { get; set; }

    /// <summary>Gets or sets the authoritative plan version rendered into the form.</summary>
    [Parameter]
    public long ExpectedVersion { get; set; }

    /// <summary>Gets or sets the antiforgery-protected manual activity endpoint.</summary>
    [Parameter]
    public string AddActivityPath { get; set; } = string.Empty;

    private static string? TimeValue(TimeOnly? value) => value?.ToString("HH:mm");
}

/// <summary>Identifies one authorized itinerary day available to an Activity FootStep review.</summary>
/// <param name="Id">The stable itinerary-day identity.</param>
/// <param name="Label">The authorized date and day label.</param>
public sealed record PlannerActivityTarget(string Id, string Label);
