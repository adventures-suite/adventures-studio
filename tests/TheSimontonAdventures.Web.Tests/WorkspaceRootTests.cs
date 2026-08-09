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
    /// Ensures an authenticated workspace request renders a protected sign-out
    /// mutation without requiring public Creator Context.
    /// </summary>
    [Fact]
    public async Task AuthenticatedWorkspace_RendersProtectedPostSignOut()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntiforgery();
        services.AddHttpContextAccessor();
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "opaque-user")],
                authenticationType: "test"))
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

        Assert.Contains("You are signed in", html);
        Assert.Contains("method=\"post\"", html);
        Assert.Contains("action=\"/authentication/sign-out\"", html);
        Assert.DoesNotContain("opaque-user", html);
    }
}
