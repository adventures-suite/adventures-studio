using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Defines the available theme modes for the private Planner workspace shell.</summary>
public enum PlannerWorkspaceTheme
{
    /// <summary>Uses the light workspace palette.</summary>
    Light,

    /// <summary>Uses the dark workspace palette.</summary>
    Dark,

    /// <summary>Uses the operating-system color preference.</summary>
    System
}

/// <summary>Provides serialization helpers for Planner workspace theme values.</summary>
public static class PlannerWorkspaceThemeExtensions
{
    /// <summary>Gets the stable HTML attribute value for a theme.</summary>
    /// <param name="theme">The theme to serialize.</param>
    /// <returns>The lowercase theme value.</returns>
    public static string ToDataAttribute(this PlannerWorkspaceTheme theme) =>
        theme switch
        {
            PlannerWorkspaceTheme.Light => "light",
            PlannerWorkspaceTheme.Dark => "dark",
            _ => "system"
        };
}

/// <summary>Wraps the authorized Planner workspace with responsive navigation and display controls.</summary>
public partial class PlannerWorkspaceShell : ComponentBase, IAsyncDisposable
{
    private ElementReference MainContent { get; set; }
    private ElementReference ShellElement { get; set; }
    private ElementReference PinnedHeader { get; set; }
    private IJSObjectReference? FocusModule { get; set; }
    private bool IsAccountMenuOpen { get; set; }

    [Inject]
    private IJSRuntime JavaScript { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private string SkipLinkHref
    {
        get
        {
            var relativePath = Navigation.ToBaseRelativePath(Navigation.Uri);
            var fragmentIndex = relativePath.IndexOf('#', StringComparison.Ordinal);
            if (fragmentIndex >= 0)
            {
                relativePath = relativePath[..fragmentIndex];
            }

            return $"/{relativePath}#planner-workspace-content";
        }
    }

    /// <summary>Gets the minimum supported sidebar width in pixels.</summary>
    public const int MinimumSidebarWidthPixels = 224;

    /// <summary>Gets the maximum supported sidebar width in pixels.</summary>
    public const int MaximumSidebarWidthPixels = 384;

    /// <summary>Gets the keyboard resize increment in pixels.</summary>
    public const int SidebarResizeStepPixels = 16;

    /// <summary>Gets or sets the visible shell title used by the content region.</summary>
    [Parameter]
    public string Title { get; set; } = "Planner";

    /// <summary>Gets or sets the descriptive text shown in the workspace toolbar.</summary>
    [Parameter]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional public site opened by the Web navigation item.</summary>
    [Parameter]
    public Uri? SimontonAdventuresUrl { get; set; }

    /// <summary>Gets or sets the authorized Creator-addressed workspace route prefix.</summary>
    [Parameter]
    public string? WorkspaceBasePath { get; set; }

    /// <summary>Gets or sets the active AdventuresSuite application slug.</summary>
    [Parameter]
    public string ActiveApplicationSlug { get; set; } = "planner";

    /// <summary>Gets or sets optional toolbar content such as an authenticated sign-out action.</summary>
    [Parameter]
    public RenderFragment? ToolbarContent { get; set; }

    /// <summary>Gets or sets the contextual workspace toolbar pinned beneath the shell header.</summary>
    [Parameter]
    public RenderFragment? WorkspaceToolbarContent { get; set; }

    /// <summary>Gets or sets the shell content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets whether the content uses the full available workspace width.</summary>
    [Parameter]
    public bool IsContentEdgeAligned { get; set; }

    /// <summary>Gets the selected semantic theme.</summary>
    public PlannerWorkspaceTheme Theme { get; private set; } = PlannerWorkspaceTheme.System;

    /// <summary>Gets whether the left navigation is collapsed.</summary>
    public bool IsSidebarCollapsed { get; private set; }

    /// <summary>Gets whether the left navigation is hidden.</summary>
    public bool IsSidebarHidden { get; private set; }

    /// <summary>Gets whether mobile navigation is open.</summary>
    public bool IsMobileNavigationOpen { get; private set; }

    /// <summary>Gets the bounded sidebar width in pixels.</summary>
    public int SidebarWidthPixels { get; private set; } = 280;

    /// <summary>Gets the shell class list derived from authoritative state.</summary>
    public string ShellClasses => string.Join(' ',
        "planner-shell",
        IsSidebarHidden ? "planner-shell--sidebar-hidden" : string.Empty,
        IsSidebarCollapsed ? "planner-shell--sidebar-collapsed" : string.Empty,
        IsMobileNavigationOpen ? "planner-shell--mobile-open" : string.Empty).Trim();

    /// <summary>Gets the content class list derived from the requested workspace presentation.</summary>
    public string ContentClasses => string.Join(' ',
        "planner-shell__content",
        IsContentEdgeAligned ? "planner-shell__content--edge-aligned" : string.Empty).Trim();

    /// <summary>Toggles the collapsed sidebar state.</summary>
    public Task ToggleSidebarCollapseAsync()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        return Task.CompletedTask;
    }

    /// <summary>Hides the sidebar and closes mobile navigation.</summary>
    public Task HideSidebarAsync()
    {
        IsSidebarHidden = true;
        IsMobileNavigationOpen = false;
        return Task.CompletedTask;
    }

    /// <summary>Shows the sidebar.</summary>
    public Task ShowSidebarAsync()
    {
        IsSidebarHidden = false;
        return Task.CompletedTask;
    }

    /// <summary>Toggles mobile navigation without changing authorization or route state.</summary>
    public Task ToggleMobileNavigationAsync()
    {
        IsSidebarHidden = false;
        IsMobileNavigationOpen = !IsMobileNavigationOpen;
        return Task.CompletedTask;
    }

    /// <summary>Closes mobile navigation.</summary>
    public Task CloseMobileNavigationAsync()
    {
        IsMobileNavigationOpen = false;
        return Task.CompletedTask;
    }

    /// <summary>Applies a requested sidebar width after enforcing shell bounds.</summary>
    /// <param name="requestedWidthPixels">The requested width in pixels.</param>
    public Task ResizeSidebarAsync(int requestedWidthPixels)
    {
        SidebarWidthPixels = Math.Clamp(
            requestedWidthPixels,
            MinimumSidebarWidthPixels,
            MaximumSidebarWidthPixels);
        return Task.CompletedTask;
    }

    /// <summary>Handles keyboard resizing on the sidebar separator.</summary>
    /// <param name="args">The keyboard event.</param>
    public Task HandleResizeKeyAsync(KeyboardEventArgs args) =>
        args.Key switch
        {
            "ArrowLeft" => ResizeSidebarAsync(SidebarWidthPixels - SidebarResizeStepPixels),
            "ArrowRight" => ResizeSidebarAsync(SidebarWidthPixels + SidebarResizeStepPixels),
            "Home" => ResizeSidebarAsync(MinimumSidebarWidthPixels),
            "End" => ResizeSidebarAsync(MaximumSidebarWidthPixels),
            _ => Task.CompletedTask
        };

    /// <summary>Changes the semantic workspace theme from an accessible select control.</summary>
    /// <param name="args">The select change event.</param>
    public Task ChangeThemeAsync(ChangeEventArgs args)
    {
        return SetThemeAsync(args.Value?.ToString() switch
        {
            "light" => PlannerWorkspaceTheme.Light,
            "dark" => PlannerWorkspaceTheme.Dark,
            _ => PlannerWorkspaceTheme.System
        });
    }

    private Task SetThemeAsync(PlannerWorkspaceTheme theme)
    {
        Theme = Enum.IsDefined(theme) ? theme : PlannerWorkspaceTheme.System;
        IsAccountMenuOpen = false;
        return Task.CompletedTask;
    }

    private Task ToggleAccountMenuAsync()
    {
        IsAccountMenuOpen = !IsAccountMenuOpen;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            FocusModule = await JavaScript.InvokeAsync<IJSObjectReference>(
                "import",
                "./Components/Planner/PlannerWorkspaceShell.razor.js");
            if (FocusModule is not null)
            {
                await FocusModule.InvokeVoidAsync(
                    "observePinnedHeader",
                    ShellElement,
                    PinnedHeader);
            }
        }
    }

    private async Task FocusMainContentAsync()
    {
        if (FocusModule is not null)
        {
            await FocusModule.InvokeVoidAsync("focusElement", MainContent);
        }
        else
        {
            await MainContent.FocusAsync();
        }
    }

    /// <summary>Releases the JavaScript module owned by this interactive shell.</summary>
    public async ValueTask DisposeAsync()
    {
        if (FocusModule is not null)
        {
            try
            {
                await FocusModule.InvokeVoidAsync("disconnectPinnedHeader", ShellElement);
                await FocusModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit already owns teardown when the browser disconnects.
            }
        }

        GC.SuppressFinalize(this);
    }
}
