using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders the Creator workspaces already authorized for Planner access.</summary>
public partial class PlannerCreatorWorkspaceChooser
{
    /// <summary>Gets or sets the authorized, least-data workspace choices.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<CreatorWorkspaceChoice> Workspaces { get; set; } = [];
}
