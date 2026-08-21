using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders authorized Adventure entry points for one Creator.</summary>
public partial class PlannerAdventureLauncher
{
    /// <summary>Gets or sets the authorized Creator identity used to construct Planner routes.</summary>
    [Parameter, EditorRequired]
    public CreatorId CreatorId { get; set; }

    /// <summary>Gets or sets the authorized, least-data active-plan projections.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<AdventurePlanDashboardItem> Plans { get; set; } = [];

    private string PlanListPath => $"/workspace/creators/{CreatorId.Value}/plans";

    private string PlanPath(AdventurePlanId planId) => $"{PlanListPath}/{planId.Value}";

    private static string FormatDates(AdventurePlanDashboardItem plan) =>
        $"{plan.Dates.Start:MMM d, yyyy} – {plan.Dates.End:MMM d, yyyy}";

    private static string FormatStatus(PlanningStatus status) => status switch
    {
        PlanningStatus.Idea => "Idea",
        PlanningStatus.Draft => "Draft",
        PlanningStatus.Planned => "Planned",
        PlanningStatus.Upcoming => "Upcoming",
        PlanningStatus.InProgress => "In progress",
        PlanningStatus.Completed => "Completed",
        PlanningStatus.Archived => "Archived",
        _ => "Unavailable"
    };
}
