using Microsoft.AspNetCore.Components;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders one honest, non-interactive workspace experience preview.</summary>
public partial class WorkspaceApplicationPlaceholder
{
    /// <summary>Gets or sets the allowlisted application definition.</summary>
    [Parameter, EditorRequired]
    public WorkspaceApplicationDefinition Application { get; set; } = null!;

    /// <summary>Gets or sets the authenticated Planner return path.</summary>
    [Parameter]
    public string PlannerPath { get; set; } = "/workspace";

    /// <summary>Gets or sets the validated public Simonton Adventures preview URL.</summary>
    [Parameter]
    public Uri? SimontonAdventuresUrl { get; set; }
}
