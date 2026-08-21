using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Identifies a user-arrangeable section of the Planner Journey workspace.</summary>
public enum PlannerWorkspacePanel
{
    /// <summary>The plan title, description, and inclusive dates.</summary>
    Overview,
    /// <summary>The ordered destinations and route.</summary>
    Route,
    /// <summary>The daily itinerary and planned activities.</summary>
    Itinerary,
    /// <summary>The planned activities grouped under their authoritative itinerary days.</summary>
    Activities,
    /// <summary>The Journey transportation segments.</summary>
    Transportation,
    /// <summary>The Journey accommodations.</summary>
    Accommodations,
    /// <summary>The credential-free reservation summaries.</summary>
    Reservations
}

/// <summary>Provides exclusive, momentary focus navigation across Planner Journey panels.</summary>
public partial class PlannerJourneyFocusToolbar : ComponentBase
{
    private static readonly PlannerWorkspacePanel[] Panels = Enum.GetValues<PlannerWorkspacePanel>();

    /// <summary>Gets or sets the authorized plan projection used only for section counts.</summary>
    [Parameter, EditorRequired]
    public AdventurePlanDetail Plan { get; set; } = null!;

    /// <summary>Gets or sets the panel currently selected for exclusive toolbar focus.</summary>
    [Parameter]
    public PlannerWorkspacePanel FocusedPanel { get; set; }

    /// <summary>Gets or sets the callback raised when a toolbar panel is selected.</summary>
    [Parameter]
    public EventCallback<PlannerWorkspacePanel> OnFocusedPanelChanged { get; set; }

    private Task SelectAsync(PlannerWorkspacePanel panel) => OnFocusedPanelChanged.InvokeAsync(panel);
    private static string Label(PlannerWorkspacePanel panel) => panel switch
    {
        PlannerWorkspacePanel.Overview => "Overview",
        PlannerWorkspacePanel.Route => "Route",
        PlannerWorkspacePanel.Itinerary => "Itinerary",
        PlannerWorkspacePanel.Activities => "Activities",
        PlannerWorkspacePanel.Transportation => "Transport",
        PlannerWorkspacePanel.Accommodations => "Stays",
        PlannerWorkspacePanel.Reservations => "Reservations",
        _ => panel.ToString()
    };
    private string Count(PlannerWorkspacePanel panel) => panel switch
    {
        PlannerWorkspacePanel.Overview => "Plan",
        PlannerWorkspacePanel.Route => $"{Plan.Destinations.Count}",
        PlannerWorkspacePanel.Itinerary => $"{Plan.Days.Count}",
        PlannerWorkspacePanel.Activities => $"{Plan.Days.Sum(day => day.Activities.Count)}",
        PlannerWorkspacePanel.Transportation => $"{Plan.Transportation.Count}",
        PlannerWorkspacePanel.Accommodations => $"{Plan.Accommodations.Count}",
        PlannerWorkspacePanel.Reservations => $"{Plan.Reservations.Count}",
        _ => "0"
    };
    private static RenderFragment Icon(PlannerWorkspacePanel panel) => builder =>
    {
        builder.OpenElement(0, "svg");
        builder.AddAttribute(1, "viewBox", "0 0 24 24");
        builder.AddAttribute(2, "aria-hidden", "true");
        builder.AddAttribute(3, "focusable", "false");
        builder.AddAttribute(4, "width", "18");
        builder.AddAttribute(5, "height", "18");
        builder.AddAttribute(6, "fill", "none");
        builder.AddAttribute(7, "stroke", "currentColor");
        builder.AddAttribute(8, "stroke-linecap", "round");
        builder.AddAttribute(9, "stroke-linejoin", "round");
        builder.AddAttribute(10, "stroke-width", "1.7");
        builder.OpenElement(11, "path");
        builder.AddAttribute(12, "d", panel switch
        {
            PlannerWorkspacePanel.Overview => "M5 4h14v16H5zM8 8h8M8 12h8M8 16h5",
            PlannerWorkspacePanel.Route => "M5 19V7l5-3 4 3 5-3v12l-5 3-4-3-5 3Zm5-15v12m4-9v12",
            PlannerWorkspacePanel.Itinerary => "M6 3v3m12-3v3M4 8h16v12H4zM8 12h3m2 0h3M8 16h3m2 0h3",
            PlannerWorkspacePanel.Activities => "M12 3l1.7 4.3L18 9l-4.3 1.7L12 15l-1.7-4.3L6 9l4.3-1.7L12 3Zm6 11 .9 2.1L21 17l-2.1.9L18 20l-.9-2.1L15 17l2.1-.9L18 14Z",
            PlannerWorkspacePanel.Transportation => "M3 14h18M6 14l2-8h8l2 8M7 18h.01M17 18h.01",
            PlannerWorkspacePanel.Accommodations => "M4 19V9l8-6 8 6v10M8 19v-6h8v6",
            PlannerWorkspacePanel.Reservations => "M5 4h14v16H5zM8 8h8M8 12h5M8 16h7",
            _ => string.Empty
        });
        builder.CloseElement();
        builder.CloseElement();
    };
}
