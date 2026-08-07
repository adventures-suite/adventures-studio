using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Services;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies Creator resolution and publication enforcement through the complete
/// public HTTP pipeline.
/// </summary>
public sealed class PublicRouteIntegrationTests : IClassFixture<PublicRouteIntegrationTests.CreatorWebApplicationFactory>
{
    private readonly CreatorWebApplicationFactory _factory;

    /// <summary>Initializes public-route integration tests.</summary>
    /// <param name="factory">The development-hosted Creator application.</param>
    public PublicRouteIntegrationTests(CreatorWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Ensures a public flagship volume is served.</summary>
    [Fact]
    public async Task PublicVolume_ReturnsOk()
    {
        using var response = await SendAsync(
            "localhost",
            "/volumes/italy-greece-croatia");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Ensures deployment health reports successful completion of both
    /// required startup-validation stages.
    /// </summary>
    [Fact]
    public async Task HealthEndpoint_ReportsStartupValidationReadiness()
    {
        using var response = await SendAsync("localhost", "/health");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Healthy\"", payload);
        Assert.Contains("\"deploymentVersion\":", payload);
        Assert.Contains("\"resourcesValidated\":true", payload);
        Assert.Contains("\"creatorContentValidated\":true", payload);
    }

    /// <summary>Ensures a draft Creator-owned volume is not served publicly.</summary>
    [Fact]
    public async Task DraftVolume_ReturnsNotFound()
    {
        using var response = await SendAsync(
            "demo.localhost",
            "/volumes/demo-draft-notebook");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Ensures a published destination is served.</summary>
    [Fact]
    public async Task PublishedDestination_ReturnsOk()
    {
        using var response = await SendAsync(
            "localhost",
            "/volumes/italy-greece-croatia/italy/venice");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Ensures destination resources and authoritative alt text reach public HTML.</summary>
    [Fact]
    public async Task PublishedDestination_RendersResourceUrlAndAlternativeText()
    {
        using var response = await SendAsync(
            "localhost",
            "/volumes/italy-greece-croatia/italy/venice");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("/images/volumes/volume-1/italy/venice/canal-hero.jpeg", html);
        Assert.Contains("A canal in Venice", html);
        Assert.Contains("Photo: The Simonton Adventures", html);
        Assert.Contains("Copyright The Simonton Adventures", html);
        Assert.DoesNotContain("athens-wide.jpeg", html);
    }

    /// <summary>Ensures each Creator renders only its independently owned homepage resource.</summary>
    [Fact]
    public async Task HomepageResources_AreCreatorScoped()
    {
        using var flagship = await SendAsync("localhost", "/");
        using var demo = await SendAsync("demo.localhost", "/");
        var flagshipHtml = await flagship.Content.ReadAsStringAsync();
        var demoHtml = await demo.Content.ReadAsStringAsync();

        Assert.Contains("adventures-studio-hero.jpeg", flagshipHtml);
        Assert.DoesNotContain("athens-wide.jpeg", flagshipHtml);
        Assert.Contains("athens-wide.jpeg", demoHtml);
        Assert.DoesNotContain("adventures-studio-hero.jpeg", demoHtml);
    }

    /// <summary>Ensures an unpublished destination is not served publicly.</summary>
    [Fact]
    public async Task UnpublishedDestination_ReturnsNotFound()
    {
        using var response = await SendAsync(
            "demo.localhost",
            "/volumes/demo-aegean-notebook/greece/unpublished-preview");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Ensures an unapproved host cannot receive Creator content.</summary>
    [Fact]
    public async Task UnknownHost_ReturnsMisdirectedRequest()
    {
        using var response = await SendAsync("unknown.example", "/");

        Assert.Equal(HttpStatusCode.MisdirectedRequest, response.StatusCode);
    }

    /// <summary>
    /// Ensures shared homepage presentation renders only the demo Creator's
    /// configured copy and media.
    /// </summary>
    [Fact]
    public async Task DemoHomepage_ContainsNoFlagshipPresentation()
    {
        using var response = await SendAsync("demo.localhost", "/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Small field notes, independently owned.", html);
        Assert.Contains("Open the Notebook", html);
        Assert.Contains(
            "/images/volumes/volume-1/greece/athens/athens-wide.jpeg",
            html);
        Assert.DoesNotContain("The Simonton Adventures", html);
        Assert.DoesNotContain("adventures-studio-hero.jpeg", html);
    }

    /// <summary>
    /// Ensures each Creator controls both the selected homepage sections and
    /// their render order.
    /// </summary>
    [Fact]
    public async Task HomepageComposition_IsOrderedAndCreatorScoped()
    {
        using var flagship = await SendAsync("localhost", "/");
        using var demo = await SendAsync("demo.localhost", "/");
        var flagshipHtml = await flagship.Content.ReadAsStringAsync();
        var demoHtml = await demo.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, flagship.StatusCode);
        var currentIndex = flagshipHtml.IndexOf(
            "current-adventure",
            StringComparison.Ordinal);
        var plannedIndex = flagshipHtml.IndexOf(
            "home-planned-adventures",
            StringComparison.Ordinal);
        var featuredIndex = flagshipHtml.IndexOf(
            "home-destinations",
            StringComparison.Ordinal);
        Assert.True(currentIndex >= 0);
        Assert.True(plannedIndex > currentIndex);
        Assert.True(featuredIndex > plannedIndex);

        Assert.Equal(HttpStatusCode.OK, demo.StatusCode);
        Assert.Contains("current-adventure", demoHtml);
        Assert.DoesNotContain("home-planned-adventures", demoHtml);
        Assert.DoesNotContain("home-destinations", demoHtml);
    }

    /// <summary>
    /// Ensures the Adventures catalog contains only public volumes owned by
    /// the Creator resolved from the request host.
    /// </summary>
    [Fact]
    public async Task AdventuresCatalog_IsCreatorScopedAndExcludesDraftVolumes()
    {
        using var flagship = await SendAsync("localhost", "/adventures");
        using var demo = await SendAsync("demo.localhost", "/adventures");
        var flagshipHtml = await flagship.Content.ReadAsStringAsync();
        var demoHtml = await demo.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, flagship.StatusCode);
        Assert.Contains("Italy, Greece &amp; Croatia", flagshipHtml);
        Assert.DoesNotContain("Aegean Notebook", flagshipHtml);

        Assert.Equal(HttpStatusCode.OK, demo.StatusCode);
        Assert.Contains("Aegean Notebook", demoHtml);
        Assert.DoesNotContain("Demo Draft Notebook", demoHtml);
        Assert.DoesNotContain("Italy, Greece &amp; Croatia", demoHtml);
    }

    /// <summary>
    /// Ensures a planned Adventure exposes its proposed Journey Engine
    /// itinerary, planning state, and change-sensitive guidance.
    /// </summary>
    [Fact]
    public async Task PlannedAdventure_RendersPlanningExperience()
    {
        using var response = await SendAsync(
            "localhost",
            "/volumes/key-west-eastern-caribbean-cruise");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Planned Adventure", html);
        Assert.Contains("Proposed itinerary", html);
        Assert.Contains("Destinations in the plan", html);
        Assert.Contains("Perfect Day CocoCay", html);
        Assert.Contains("5/15/2027", html);
        Assert.Contains("5/19/2027", html);
        Assert.Contains("5/20/2027", html);
        Assert.Contains("5/22/2027", html);
        Assert.DoesNotContain("Gangway down", html);
        Assert.Contains("details may change", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Flight and arrival details remain to be confirmed", html);
    }

    /// <summary>
    /// Ensures a planning-stage destination resolves itinerary references
    /// without publishing a premature public destination page.
    /// </summary>
    [Fact]
    public async Task PlanningStageDestination_ReturnsNotFound()
    {
        using var response = await SendAsync(
            "localhost",
            "/volumes/spain-trans-atlantic-cruise/spain/barcelona");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Ensures identical public slugs redirect within the independently resolved
    /// Creator boundary.
    /// </summary>
    [Fact]
    public async Task SharedSlug_ResolvesIndependentlyByCreatorHost()
    {
        using var flagship = await SendAsync("localhost", "/go/athens");
        using var demo = await SendAsync("demo.localhost", "/go/athens");

        Assert.Equal(HttpStatusCode.Redirect, flagship.StatusCode);
        Assert.Equal(
            "/volumes/italy-greece-croatia/greece/athens",
            flagship.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, demo.StatusCode);
        Assert.Equal(
            "/volumes/demo-aegean-notebook/greece/athens",
            demo.Headers.Location?.OriginalString);
    }

    /// <summary>
    /// Ensures QR generation receives the primary domain owned by the resolved
    /// Creator rather than the incoming development alias.
    /// </summary>
    [Fact]
    public async Task QrEndpoint_UsesResolvedCreatorDomain()
    {
        using var flagship = await SendAsync("localhost", "/qr/athens.svg");
        using var demo = await SendAsync("demo.localhost", "/qr/athens.svg");
        var flagshipSvg = await flagship.Content.ReadAsStringAsync();
        var demoSvg = await demo.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, flagship.StatusCode);
        Assert.Contains("https://thesimontonadventures.com/go/athens", flagshipSvg);
        Assert.Equal(HttpStatusCode.OK, demo.StatusCode);
        Assert.Contains("https://demo.adventuressuite.test/go/athens", demoSvg);
    }

    private async Task<HttpResponseMessage> SendAsync(string host, string path)
    {
        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost")
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = host;

        return await client.SendAsync(request);
    }

    /// <summary>Hosts the real application with deterministic QR output.</summary>
    public sealed class CreatorWebApplicationFactory : WebApplicationFactory<Program>
    {
        /// <inheritdoc />
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IQrCodeService>();
                services.AddSingleton<IQrCodeService, InspectableQrCodeService>();
            });
        }
    }

    private sealed class InspectableQrCodeService : IQrCodeService
    {
        private readonly QrCodeService _inner = new();

        public string BuildPublicUrl(CreatorContext creatorContext, string qrSlug) =>
            _inner.BuildPublicUrl(creatorContext, qrSlug);

        public string GenerateSvg(CreatorContext creatorContext, string qrSlug) =>
            $"<svg>{BuildPublicUrl(creatorContext, qrSlug)}</svg>";

        public byte[] GeneratePng(CreatorContext creatorContext, string qrSlug) =>
            throw new NotSupportedException("PNG output is outside this integration test.");
    }
}
