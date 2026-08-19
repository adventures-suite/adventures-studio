using Microsoft.AspNetCore.Components;

namespace TheSimontonAdventures.Web.Components.Pages;

/// <summary>Declares the canonical workspace route while host and resource authorization remain below routing.</summary>
public partial class WorkspaceRoute
{
    /// <summary>Gets or sets the unresolved workspace path consumed by the authorized root experience.</summary>
    [Parameter]
    public string? WorkspacePath { get; set; }
}
