using Microsoft.AspNetCore.Components;

namespace TheSimontonAdventures.Web.Components;

/// <summary>
/// Presents the authenticated user's opaque platform identifier for bounded support operations.
/// </summary>
public partial class PlannerSupportIdentity
{
    /// <summary>
    /// Gets or sets the opaque identifier belonging to the currently authenticated user.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string UserId { get; set; } = string.Empty;
}
