using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheSimontonAdventures.Web.Components;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the shared paging contract for all Planner idea collections.</summary>
public sealed class PlannerIdeaPagerTests
{
    /// <summary>The pager announces position and exposes a user-defined card count.</summary>
    [Fact]
    public async Task Pager_RendersPageStatusSizeChoicesAndNavigation()
    {
        var html = await RenderAsync(new()
        {
            [nameof(PlannerIdeaPager.TotalItems)] = 5,
            [nameof(PlannerIdeaPager.PageSize)] = 2,
            [nameof(PlannerIdeaPager.CurrentPage)] = 2,
            [nameof(PlannerIdeaPager.PageSizeOptions)] = new[] { 1, 2, 4 }
        });

        Assert.Contains("Cards per page", html, StringComparison.Ordinal);
        Assert.Contains("Page 2 of 3 · 5 ideas", html, StringComparison.Ordinal);
        Assert.Contains(">1</option>", html, StringComparison.Ordinal);
        Assert.Contains(">2</option>", html, StringComparison.Ordinal);
        Assert.Contains(">4</option>", html, StringComparison.Ordinal);
        Assert.Contains(">Previous</button>", html, StringComparison.Ordinal);
        Assert.Contains(">Next</button>", html, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", html, StringComparison.Ordinal);
    }

    /// <summary>An empty collection omits irrelevant pagination controls.</summary>
    [Fact]
    public async Task Pager_EmptyCollectionRendersNoControls()
    {
        var html = await RenderAsync(new() { [nameof(PlannerIdeaPager.TotalItems)] = 0 });

        Assert.DoesNotContain("Cards per page", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Previous", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(Dictionary<string, object?> parameters)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<PlannerIdeaPager>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
