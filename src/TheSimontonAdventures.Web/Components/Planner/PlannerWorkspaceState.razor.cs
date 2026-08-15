using Microsoft.AspNetCore.Components;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Defines reusable private Planner workspace presentation states.</summary>
public enum PlannerWorkspaceStateKind
{
    /// <summary>Content is loading.</summary>
    Loading,
    /// <summary>An authorized result contains no records.</summary>
    Empty,
    /// <summary>Authorization was denied without disclosing protected details.</summary>
    Denied,
    /// <summary>The workspace or addressed resource is unavailable.</summary>
    Unavailable,
    /// <summary>A submitted version is stale or conflicts with current state.</summary>
    Conflict,
    /// <summary>An unexpected safe failure occurred.</summary>
    Failure
}

/// <summary>Provides stable presentation metadata for Planner workspace states.</summary>
public static class PlannerWorkspaceStateKindExtensions
{
    /// <summary>Gets the stable CSS suffix for a state.</summary>
    /// <param name="kind">The presentation state.</param>
    /// <returns>The CSS suffix.</returns>
    public static string ToCssClass(this PlannerWorkspaceStateKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>Gets the concise accessible icon label for a state.</summary>
    /// <param name="kind">The presentation state.</param>
    /// <returns>The icon label.</returns>
    public static string ToAccessibleLabel(this PlannerWorkspaceStateKind kind) => kind switch
    {
        PlannerWorkspaceStateKind.Loading => "Loading",
        PlannerWorkspaceStateKind.Empty => "Empty",
        PlannerWorkspaceStateKind.Denied => "Access denied",
        PlannerWorkspaceStateKind.Unavailable => "Unavailable",
        PlannerWorkspaceStateKind.Conflict => "Conflict",
        _ => "Failure"
    };

    /// <summary>Gets a decorative line icon path for a state.</summary>
    /// <param name="kind">The presentation state.</param>
    /// <returns>The SVG path.</returns>
    public static string ToIconPath(this PlannerWorkspaceStateKind kind) => kind switch
    {
        PlannerWorkspaceStateKind.Loading => "M12 3a9 9 0 1 0 9 9",
        PlannerWorkspaceStateKind.Empty => "M4 7h16v12H4zM8 7V4h8v3",
        PlannerWorkspaceStateKind.Denied => "M6 10h12v10H6zM8 10V7a4 4 0 0 1 8 0v3",
        PlannerWorkspaceStateKind.Conflict => "M12 3 2 20h20zM12 9v5m0 3h.01",
        PlannerWorkspaceStateKind.Failure => "M12 3 2 20h20zM12 9v5m0 3h.01",
        _ => "M12 3a9 9 0 1 0 9 9M12 7v6m0 4h.01"
    };
}

/// <summary>Renders consistent, accessible loading, empty, denial, conflict, and failure states.</summary>
public partial class PlannerWorkspaceState : ComponentBase
{
    private readonly string id = $"planner-state-{Guid.NewGuid():N}";

    /// <summary>Gets or sets the state kind.</summary>
    [Parameter, EditorRequired]
    public PlannerWorkspaceStateKind Kind { get; set; }

    /// <summary>Gets or sets the state heading.</summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional explanatory message.</summary>
    [Parameter]
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets optional state-specific actions or details.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets the unique heading identifier.</summary>
    public string HeadingId => $"{id}-heading";

    /// <summary>Gets the unique icon-title identifier.</summary>
    public string IconTitleId => $"{id}-icon";

    /// <summary>Gets the live-region role appropriate to this state.</summary>
    public string LiveRole => Kind is PlannerWorkspaceStateKind.Conflict or PlannerWorkspaceStateKind.Failure ? "alert" : "status";

    /// <summary>Gets the live-region politeness appropriate to this state.</summary>
    public string LiveSetting => Kind is PlannerWorkspaceStateKind.Conflict or PlannerWorkspaceStateKind.Failure ? "assertive" : "polite";
}
