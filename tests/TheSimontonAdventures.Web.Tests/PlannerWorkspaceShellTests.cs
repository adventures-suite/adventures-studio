using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheSimontonAdventures.Web.Components;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the production Planner shell interaction and accessibility contract.</summary>
public sealed class PlannerWorkspaceShellTests
{
    /// <summary>The shell renders accessible display, navigation, skip-link, and responsive controls.</summary>
    [Fact]
    public async Task Shell_RendersAccessibleResponsiveContract()
    {
        var html = await RenderAsync<PlannerWorkspaceShell>(new()
        {
            [nameof(PlannerWorkspaceShell.ChildContent)] = (RenderFragment)(builder =>
                builder.AddContent(0, "Authorized plan content"))
        });

        Assert.Contains("data-theme=\"system\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#planner-workspace-content\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Planner navigation\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"planner-workspace-sidebar\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Planner color theme\"", html, StringComparison.Ordinal);
        Assert.Contains("Authorized plan content", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Destinations<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Settings<", html, StringComparison.Ordinal);
    }

    /// <summary>Collapse, hide, show, and mobile state remain authoritative in the shell.</summary>
    [Fact]
    public async Task Shell_OwnsNavigationState()
    {
        var shell = new PlannerWorkspaceShell();

        await shell.ToggleSidebarCollapseAsync();
        Assert.True(shell.IsSidebarCollapsed);
        Assert.Contains("planner-shell--sidebar-collapsed", shell.ShellClasses, StringComparison.Ordinal);

        await shell.HideSidebarAsync();
        Assert.True(shell.IsSidebarHidden);
        Assert.False(shell.IsMobileNavigationOpen);

        await shell.ShowSidebarAsync();
        await shell.ToggleMobileNavigationAsync();
        Assert.False(shell.IsSidebarHidden);
        Assert.True(shell.IsMobileNavigationOpen);
        Assert.Contains("planner-shell--mobile-open", shell.ShellClasses, StringComparison.Ordinal);

        await shell.CloseMobileNavigationAsync();
        Assert.False(shell.IsMobileNavigationOpen);
    }

    /// <summary>The visible selector maps only the semantic light, dark, and system themes.</summary>
    [Theory]
    [InlineData("light", PlannerWorkspaceTheme.Light)]
    [InlineData("dark", PlannerWorkspaceTheme.Dark)]
    [InlineData("system", PlannerWorkspaceTheme.System)]
    [InlineData("unexpected", PlannerWorkspaceTheme.System)]
    public async Task Shell_SelectsAllowlistedTheme(string value, PlannerWorkspaceTheme expected)
    {
        var shell = new PlannerWorkspaceShell();

        await shell.ChangeThemeAsync(new ChangeEventArgs { Value = value });

        Assert.Equal(expected, shell.Theme);
        Assert.Equal(value is "light" or "dark" ? value : "system", shell.Theme.ToDataAttribute());
    }

    /// <summary>Separator keys and direct resize requests cannot exceed the supported width range.</summary>
    [Fact]
    public async Task Shell_KeyboardResize_IsBounded()
    {
        var shell = new PlannerWorkspaceShell();

        await shell.ResizeSidebarAsync(int.MinValue);
        Assert.Equal(PlannerWorkspaceShell.MinimumSidebarWidthPixels, shell.SidebarWidthPixels);
        await shell.HandleResizeKeyAsync(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Equal(PlannerWorkspaceShell.MinimumSidebarWidthPixels, shell.SidebarWidthPixels);

        await shell.HandleResizeKeyAsync(new KeyboardEventArgs { Key = "End" });
        Assert.Equal(PlannerWorkspaceShell.MaximumSidebarWidthPixels, shell.SidebarWidthPixels);
        await shell.HandleResizeKeyAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(PlannerWorkspaceShell.MaximumSidebarWidthPixels, shell.SidebarWidthPixels);

        await shell.HandleResizeKeyAsync(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal(PlannerWorkspaceShell.MinimumSidebarWidthPixels, shell.SidebarWidthPixels);
    }

    /// <summary>The controlled sidebar raises requests without mutating parent-owned parameters.</summary>
    [Fact]
    public async Task Sidebar_EmitsStateRequestsWithoutMutatingParameters()
    {
        var collapseRequests = 0;
        var resizeRequest = 0;
        var sidebar = new PlannerWorkspaceSidebar();
        ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PlannerWorkspaceSidebar.IsCollapsed)] = false,
            [nameof(PlannerWorkspaceSidebar.WidthPixels)] = 280,
            [nameof(PlannerWorkspaceSidebar.MinimumWidthPixels)] = PlannerWorkspaceShell.MinimumSidebarWidthPixels,
            [nameof(PlannerWorkspaceSidebar.MaximumWidthPixels)] = PlannerWorkspaceShell.MaximumSidebarWidthPixels,
            [nameof(PlannerWorkspaceSidebar.OnCollapseRequested)] = EventCallback.Factory.Create(this, () => collapseRequests++),
            [nameof(PlannerWorkspaceSidebar.OnResizeRequested)] = EventCallback.Factory.Create<int>(this, value => resizeRequest = value)
        }).SetParameterProperties(sidebar);

        await sidebar.OnCollapseRequested.InvokeAsync();
        await sidebar.HandleResizeKeyAsync(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(1, collapseRequests);
        Assert.False(sidebar.IsCollapsed);
        Assert.Equal(296, resizeRequest);
    }

    /// <summary>The sidebar exposes labeled SVGs, keyboard resizing semantics, and bounded values.</summary>
    [Fact]
    public async Task Sidebar_RendersAccessibleControlState()
    {
        var html = await RenderAsync<PlannerWorkspaceSidebar>(new()
        {
            [nameof(PlannerWorkspaceSidebar.WidthPixels)] = 280,
            [nameof(PlannerWorkspaceSidebar.MinimumWidthPixels)] = PlannerWorkspaceShell.MinimumSidebarWidthPixels,
            [nameof(PlannerWorkspaceSidebar.MaximumWidthPixels)] = PlannerWorkspaceShell.MaximumSidebarWidthPixels
        });

        Assert.Contains("AdventuresSuite Planner</title>", html, StringComparison.Ordinal);
        Assert.Contains("Planner overview</title>", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"separator\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuemin=\"224\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuemax=\"384\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuenow=\"280\"", html, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"0\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Narrow Planner navigation\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Widen Planner navigation\"", html, StringComparison.Ordinal);
    }

    /// <summary>Every reusable state has a named region and appropriate live behavior.</summary>
    [Theory]
    [InlineData(PlannerWorkspaceStateKind.Loading, "status", "polite")]
    [InlineData(PlannerWorkspaceStateKind.Empty, "status", "polite")]
    [InlineData(PlannerWorkspaceStateKind.Denied, "status", "polite")]
    [InlineData(PlannerWorkspaceStateKind.Unavailable, "status", "polite")]
    [InlineData(PlannerWorkspaceStateKind.Conflict, "alert", "assertive")]
    [InlineData(PlannerWorkspaceStateKind.Failure, "alert", "assertive")]
    public async Task StatePresentation_RendersAccessibleRegion(
        PlannerWorkspaceStateKind kind,
        string role,
        string live)
    {
        var html = await RenderAsync<PlannerWorkspaceState>(new()
        {
            [nameof(PlannerWorkspaceState.Kind)] = kind,
            [nameof(PlannerWorkspaceState.Title)] = $"{kind} title",
            [nameof(PlannerWorkspaceState.Message)] = "Safe explanation"
        });

        Assert.Contains($"planner-state--{kind.ToCssClass()}", html, StringComparison.Ordinal);
        Assert.Contains($"role=\"{role}\"", html, StringComparison.Ordinal);
        Assert.Contains($"aria-live=\"{live}\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-labelledby=\"planner-state-", html, StringComparison.Ordinal);
        Assert.Contains($"{kind} title", html, StringComparison.Ordinal);
        Assert.Contains("Safe explanation", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
