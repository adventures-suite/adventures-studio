using Microsoft.AspNetCore.Components;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders a consistent, accessible Planner panel disclosure header.</summary>
public partial class PlannerPanelHeader : ComponentBase
{
    /// <summary>Gets or sets the panel represented by the header.</summary>
    [Parameter] public PlannerWorkspacePanel Panel { get; set; }
    /// <summary>Gets or sets the visible panel title.</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    /// <summary>Gets or sets the concise panel-content summary.</summary>
    [Parameter, EditorRequired] public string Summary { get; set; } = string.Empty;
    /// <summary>Gets or sets the heading element identifier.</summary>
    [Parameter, EditorRequired] public string HeadingId { get; set; } = string.Empty;
    /// <summary>Gets or sets the controlled content-region identifier.</summary>
    [Parameter, EditorRequired] public string ContentId { get; set; } = string.Empty;
    /// <summary>Gets or sets whether the panel content is expanded.</summary>
    [Parameter] public bool IsExpanded { get; set; }
    /// <summary>Gets or sets whether the toolbar currently focuses this panel.</summary>
    [Parameter] public bool IsFocused { get; set; }
    /// <summary>Gets or sets the callback raised when manual expansion is toggled.</summary>
    [Parameter] public EventCallback<PlannerWorkspacePanel> OnToggle { get; set; }

    private string HeaderClasses => $"planner-panel-header{(IsFocused ? " planner-panel-header--focused" : IsExpanded ? " planner-panel-header--open" : string.Empty)}";
    private Task ToggleAsync() => OnToggle.InvokeAsync(Panel);
}
