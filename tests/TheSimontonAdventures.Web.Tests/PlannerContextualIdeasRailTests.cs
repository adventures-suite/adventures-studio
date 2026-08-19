using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheSimontonAdventures.Web.Components;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the presentation-only Planner Ideas rail contract.</summary>
public sealed class PlannerContextualIdeasRailTests
{
    /// <summary>The rail begins with an announced prompt and no fictional cards.</summary>
    [Fact]
    public async Task Rail_WithoutSelection_RendersPromptWithoutIdeas()
    {
        var html = await RenderAsync(new());

        Assert.Contains("Choose a destination or day", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Fictional local Alpha demo", html, StringComparison.Ordinal);
        Assert.Contains("role=\"complementary\"", html, StringComparison.Ordinal);
    }

    /// <summary>Fictional cards remain fail-closed unless the explicit Development gate is enabled.</summary>
    [Fact]
    public async Task Rail_SelectedContext_RequiresDevelopmentGateForFictionalIdeas()
    {
        var context = new PlannerIdeasContext(PlannerIdeasContextKind.Destination, "destination-1", "Example coast");
        var productionHtml = await RenderAsync(new() { [nameof(PlannerContextualIdeasRail.Context)] = context });
        var developmentHtml = await RenderAsync(new()
        {
            [nameof(PlannerContextualIdeasRail.Context)] = context,
            [nameof(PlannerContextualIdeasRail.EnableDevelopmentIdeas)] = true
        });

        Assert.Contains("No ideas for this context", productionHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Fictional local Alpha demo", productionHtml, StringComparison.Ordinal);
        Assert.Contains("Suggestions for", developmentHtml, StringComparison.Ordinal);
        Assert.Contains("Example coast", developmentHtml, StringComparison.Ordinal);
        Assert.Contains("Fictional local Alpha demo", developmentHtml, StringComparison.Ordinal);
        Assert.Contains("not booked, available, or added to your plan", developmentHtml, StringComparison.Ordinal);
    }

    /// <summary>Every non-populated state has explicit accessible status content.</summary>
    [Theory]
    [InlineData(PlannerIdeasState.Loading, "Finding ideas")]
    [InlineData(PlannerIdeasState.Empty, "No ideas for this context")]
    [InlineData(PlannerIdeasState.Unavailable, "Ideas temporarily unavailable")]
    [InlineData(PlannerIdeasState.Denied, "Ideas unavailable")]
    public async Task Rail_RendersExplicitState(PlannerIdeasState state, string title)
    {
        var html = await RenderAsync(new() { [nameof(PlannerContextualIdeasRail.StateOverride)] = state });

        Assert.Contains(title, html, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", html, StringComparison.Ordinal);
    }

    /// <summary>The rail exposes keyboard resize bounds and a narrow-screen dialog trigger.</summary>
    [Fact]
    public async Task Rail_RendersResponsiveInteractionContract()
    {
        var html = await RenderAsync(new() { [nameof(PlannerContextualIdeasRail.WidthPixels)] = 320 });

        Assert.Contains("aria-haspopup=\"dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"separator\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuemin=\"272\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuemax=\"400\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-valuenow=\"320\"", html, StringComparison.Ordinal);
        var markup = File.ReadAllText(Path.Combine(FindApplicationRoot(), "Components", "Planner", "PlannerContextualIdeasRail.razor"));
        var codeBehind = File.ReadAllText(Path.Combine(FindApplicationRoot(), "Components", "Planner", "PlannerContextualIdeasRail.razor.cs"));
        Assert.Contains("@onpointermove", markup, StringComparison.Ordinal);
        Assert.Contains("role=\"@(IsDrawerOpen ? \"dialog\" : \"complementary\")\"", markup, StringComparison.Ordinal);
        Assert.Contains("protected override async Task OnAfterRenderAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("FocusCloseAfterRender = true;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("FocusOpenAfterRender = true;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("await CloseButton.FocusAsync();", codeBehind, StringComparison.Ordinal);
        Assert.Contains("await OpenButton.FocusAsync();", codeBehind, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(Dictionary<string, object?> parameters)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<PlannerContextualIdeasRail>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }

    private static string FindApplicationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "TheSimontonAdventures.Web");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The web application root could not be found.");
    }
}
