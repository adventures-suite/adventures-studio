using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders the explicit confirmation required before a destination FootStep mutation.</summary>
public partial class PlannerDestinationFootStepReview : ComponentBase
{
    private string? PreviousFootStepKey { get; set; }

    /// <summary>Gets or sets the authorized destination FootStep being reviewed.</summary>
    [Parameter, EditorRequired]
    public PlannerFootStepDefinition FootStep { get; set; } = null!;

    /// <summary>Gets or sets the protected destination FootStep application path.</summary>
    [Parameter, EditorRequired]
    public string ApplyPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the authoritative plan version shown to the user.</summary>
    [Parameter]
    public long ExpectedVersion { get; set; }

    /// <summary>Gets or sets the inclusive plan start date.</summary>
    [Parameter]
    public DateOnly PlanStartDate { get; set; }

    /// <summary>Gets or sets the inclusive plan end date.</summary>
    [Parameter]
    public DateOnly PlanEndDate { get; set; }

    /// <summary>Gets or sets the optional cancellation callback.</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    private PlannerFootStepDestinationDraft Destination =>
        FootStep.DestinationDraft ?? throw new InvalidOperationException("A destination review requires a destination draft.");

    private string IdempotencyKey { get; set; } = string.Empty;
    private string HeadingId => $"footstep-review-{FootStep.Id}";
    private DateOnly SuggestedEnd
    {
        get
        {
            var candidate = FootStep.DurationDays is > 1
                ? PlanStartDate.AddDays(FootStep.DurationDays.Value - 1)
                : PlanStartDate;
            return candidate > PlanEndDate ? PlanEndDate : candidate;
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var key = $"{FootStep.Id}:{FootStep.Version}:{ExpectedVersion}";
        if (!string.Equals(key, PreviousFootStepKey, StringComparison.Ordinal))
        {
            PreviousFootStepKey = key;
            IdempotencyKey = $"footstep_{Guid.NewGuid():N}";
        }
    }

    private static string DateValue(DateOnly date) => date.ToString("yyyy-MM-dd");
}
