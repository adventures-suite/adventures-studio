using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the product showcase cannot become a production route.</summary>
public sealed class ShowcaseHostingTests
{
    /// <summary>Ensures the compiled showcase route fails closed outside Development.</summary>
    [Fact]
    public async Task ProductionHost_ReturnsNotFoundForShowcase()
    {
        await using var factory = new ProductionWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://thesimontonadventures.com")
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/showcase");
        request.Headers.Host = "thesimontonadventures.com";

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Ensures the isolated Azure showcase serves no ordinary application route.</summary>
    [Fact]
    public async Task ShowcaseHost_ExposesOnlyShowcaseSurface()
    {
        await using var factory = new ShowcaseWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://showcase.example")
        });

        using var showcase = await client.GetAsync("/showcase");
        using var ordinaryRoute = await client.GetAsync("/destinations");
        using var root = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, showcase.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ordinaryRoute.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, root.StatusCode);
        Assert.Equal("/showcase", root.Headers.Location?.OriginalString);
    }

    /// <summary>Ensures the public story showcase cannot expose private workspace routes.</summary>
    [Fact]
    public async Task PublicShowcaseHost_ServesCreatorStoryAndDeniesPrivateRoutes()
    {
        await using var factory = new PublicShowcaseWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://simonton-showcase.example")
        });

        using var home = await client.GetAsync("/");
        var html = await home.Content.ReadAsStringAsync();
        using var adventure = await client.GetAsync(
            "/volumes/italy-greece-croatia");
        using var workspace = await client.GetAsync(
            "/workspace/creators/creator_tsa_01/plans");
        using var authentication = await client.GetAsync(
            "/authentication/sign-in");
        using var plannerShowcase = await client.GetAsync("/showcase");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("The Simonton Adventures", html);
        Assert.Contains(
            "Transforming inspiration into unforgettable experiences.",
            html);
        Assert.Equal(HttpStatusCode.OK, adventure.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, workspace.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, authentication.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, plannerShowcase.StatusCode);
    }

    /// <summary>Hosts the real application with production showcase configuration disabled.</summary>
    private sealed class ProductionWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Authentication:Mode", "Disabled");
            builder.UseSetting("Showcase:Enabled", "false");
        }
    }

    /// <summary>Hosts the application in its isolated public showcase environment.</summary>
    private sealed class ShowcaseWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Showcase");
            builder.UseSetting("WEBSITE_HOSTNAME", "showcase.example");
            builder.UseSetting(
                "CreatorResolution:AzureDefaultCreatorId",
                "creator_tsa_01");
        }
    }

    /// <summary>Hosts the approved public Creator story without private capabilities.</summary>
    private sealed class PublicShowcaseWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("PublicShowcase");
            builder.UseSetting(
                "WEBSITE_HOSTNAME",
                "simonton-showcase.example");
            builder.UseSetting(
                "CreatorResolution:AzureDefaultCreatorId",
                "creator_tsa_01");
        }
    }
}
