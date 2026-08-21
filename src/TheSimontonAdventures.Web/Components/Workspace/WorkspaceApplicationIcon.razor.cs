using Microsoft.AspNetCore.Components;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders the established line icon for one AdventuresSuite workspace.</summary>
public partial class WorkspaceApplicationIcon
{
    /// <summary>Gets or sets the workspace whose icon should be rendered.</summary>
    [Parameter, EditorRequired]
    public WorkspaceApplicationKind Kind { get; set; }

    /// <summary>Gets or sets the CSS class applied to the SVG element.</summary>
    [Parameter]
    public string CssClass { get; set; } = "workspace-application-icon";
}
