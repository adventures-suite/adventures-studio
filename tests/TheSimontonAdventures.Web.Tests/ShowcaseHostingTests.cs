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
}
