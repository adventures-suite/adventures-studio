using System.Security.Claims;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheSimontonAdventures.Web.Components;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the Creator-independent workspace landing surface.</summary>
public sealed class WorkspaceRootTests
{
    /// <summary>
    /// Ensures sign-in uses a full navigation so the browser can follow the
    /// cross-origin External ID challenge instead of an enhanced fetch.
    /// </summary>
    [Fact]
    public async Task AnonymousWorkspace_DisablesEnhancedSignInNavigation()
    {
        var html = await RenderAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Contains("href=\"/authentication/sign-in\"", html);
        Assert.Contains("data-enhance-nav=\"false\"", html);
    }

    /// <summary>
    /// Ensures an authenticated workspace request renders a protected sign-out
    /// mutation without requiring public Creator Context.
    /// </summary>
    [Fact]
    public async Task AuthenticatedWorkspace_RendersProtectedPostSignOut()
    {
        var html = await RenderAsync(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "opaque-user")],
            authenticationType: "test")));

        Assert.Contains("You are signed in", html);
        Assert.Contains("method=\"post\"", html);
        Assert.Contains("action=\"/authentication/sign-out\"", html);
        Assert.DoesNotContain("opaque-user", html);
    }

    private static async Task<string> RenderAsync(ClaimsPrincipal user)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntiforgery();
        services.AddHttpContextAccessor();
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            User = user
        };
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<WorkspaceRoot>();
            return output.ToHtmlString();
        });

        return html;
    }
}
