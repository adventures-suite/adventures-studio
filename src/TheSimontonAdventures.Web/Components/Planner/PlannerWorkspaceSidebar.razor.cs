using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Displays controlled navigation for the private Planner workspace.</summary>
public partial class PlannerWorkspaceSidebar : ComponentBase
{
    private bool IsPointerResizeActive { get; set; }

    /// <summary>Gets or sets whether the authoritative shell has collapsed the sidebar.</summary>
    [Parameter]
    public bool IsCollapsed { get; set; }

    /// <summary>Gets or sets whether the authoritative shell has opened mobile navigation.</summary>
    [Parameter]
    public bool IsMobileOpen { get; set; }

    /// <summary>Gets or sets the optional public site opened by the Web navigation item.</summary>
    [Parameter]
    public Uri? SimontonAdventuresUrl { get; set; }

    /// <summary>Gets or sets the current bounded width.</summary>
    [Parameter]
    public int WidthPixels { get; set; }

    /// <summary>Gets or sets the minimum permitted width.</summary>
    [Parameter]
    public int MinimumWidthPixels { get; set; }

    /// <summary>Gets or sets the maximum permitted width.</summary>
    [Parameter]
    public int MaximumWidthPixels { get; set; }

    /// <summary>Gets or sets the callback raised when collapse is requested.</summary>
    [Parameter]
    public EventCallback OnCollapseRequested { get; set; }

    /// <summary>Gets or sets the callback raised when hiding is requested.</summary>
    [Parameter]
    public EventCallback OnHideRequested { get; set; }

    /// <summary>Gets or sets the callback raised when mobile navigation should close.</summary>
    [Parameter]
    public EventCallback OnMobileCloseRequested { get; set; }

    /// <summary>Gets or sets the callback raised with a requested bounded width.</summary>
    [Parameter]
    public EventCallback<int> OnResizeRequested { get; set; }

    /// <summary>Gets the CSS classes representing parent-owned state.</summary>
    public string SidebarClasses => string.Join(' ',
        "planner-sidebar",
        IsCollapsed ? "planner-sidebar--collapsed" : string.Empty,
        IsMobileOpen ? "planner-sidebar--mobile-open" : string.Empty).Trim();

    /// <summary>Requests one keyboard resize step toward the minimum.</summary>
    public Task DecreaseWidthAsync() =>
        OnResizeRequested.InvokeAsync(WidthPixels - PlannerWorkspaceShell.SidebarResizeStepPixels);

    /// <summary>Requests one keyboard resize step toward the maximum.</summary>
    public Task IncreaseWidthAsync() =>
        OnResizeRequested.InvokeAsync(WidthPixels + PlannerWorkspaceShell.SidebarResizeStepPixels);

    /// <summary>Maps standard separator keys to bounded resize requests.</summary>
    /// <param name="args">The keyboard event.</param>
    public Task HandleResizeKeyAsync(KeyboardEventArgs args) =>
        args.Key switch
        {
            "ArrowLeft" => DecreaseWidthAsync(),
            "ArrowRight" => IncreaseWidthAsync(),
            "Home" => OnResizeRequested.InvokeAsync(MinimumWidthPixels),
            "End" => OnResizeRequested.InvokeAsync(MaximumWidthPixels),
            _ => Task.CompletedTask
        };

    /// <summary>Begins a primary-pointer resize gesture at the requested width.</summary>
    /// <param name="args">The pointer event containing the viewport position.</param>
    public Task BeginPointerResizeAsync(PointerEventArgs args)
    {
        if (args.Button != 0)
        {
            return Task.CompletedTask;
        }

        IsPointerResizeActive = true;
        return RequestPointerWidthAsync(args.ClientX);
    }

    /// <summary>Continues an active pointer resize gesture.</summary>
    /// <param name="args">The pointer event containing the viewport position.</param>
    public Task ContinuePointerResizeAsync(PointerEventArgs args) =>
        IsPointerResizeActive
            ? RequestPointerWidthAsync(args.ClientX)
            : Task.CompletedTask;

    /// <summary>Ends the current pointer resize gesture.</summary>
    public void EndPointerResize()
    {
        IsPointerResizeActive = false;
    }

    private Task RequestPointerWidthAsync(double clientX) =>
        OnResizeRequested.InvokeAsync((int)Math.Round(clientX, MidpointRounding.AwayFromZero));
}
