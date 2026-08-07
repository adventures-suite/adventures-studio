using TheSimontonAdventures.Web.Components;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Resources;
using TheSimontonAdventures.Web.Services;
using TheSimontonAdventures.Web.Validation;

var builder = WebApplication.CreateBuilder(args);

// Register Razor Components and enable interactive server-side rendering.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Bind environment-specific Creator host aliases. The resolver ignores these
// mappings outside Development so production hosts always require an explicit
// approved-domain registration.
builder.Services.Configure<CreatorResolutionOptions>(
    builder.Configuration.GetSection(CreatorResolutionOptions.SectionName));

// Register the Creator Engine retrieval and host-resolution foundation. Request
// context establishment is introduced separately in the middleware phase.
builder.Services.AddSingleton<ICreatorService, JsonCreatorService>();
builder.Services.AddSingleton<ICreatorResolver, CreatorResolver>();
builder.Services.AddScoped<CreatorContextAccessor>();
builder.Services.AddScoped<ICreatorContextAccessor>(services =>
    services.GetRequiredService<CreatorContextAccessor>());

// Register the existing JSON-backed travel-content implementation.
//
// This service remains the current source for volumes, journeys,
// destinations, and related travel content.
builder.Services.AddSingleton<
    ITravelContentService,
    JsonTravelContentService>();

// Register the Address Engine abstraction.
//
// The Address Engine resolves stable public slugs without exposing the
// underlying JSON-backed travel-content implementation to consumers.
builder.Services.AddSingleton<
    IAddressableContentService,
    AddressableContentService>();

// Register the QR Engine implementation used to generate SVG and PNG
// representations of stable public addresses.
builder.Services.AddSingleton<
    IQrCodeService,
    QrCodeService>();

// Introduce storage-independent, Creator-owned resource records while keeping
// existing public assets in wwwroot as the initial storage provider.
builder.Services.AddSingleton<IResourceProvider, LocalPublicResourceProvider>();
builder.Services.AddSingleton<IResourceService, JsonResourceService>();
builder.Services.AddHostedService<ResourceValidationHostedService>();

// Warm the immutable Creator registry and validate Creator-scoped content
// before requests can observe duplicate addresses or broken public references.
builder.Services.AddSingleton<ICreatorContentValidator, CreatorContentValidator>();
builder.Services.AddSingleton<ApplicationReadinessState>();
builder.Services.AddHostedService<CreatorContentValidationHostedService>();

var app = builder.Build();

// Configure production-only error handling and HTTP Strict Transport Security.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    // The default HSTS duration is 30 days. This can be adjusted later when
    // production hosting and custom domains are finalized.
    app.UseHsts();
}

// Resolve the explicitly approved request host before status-page re-execution,
// static assets, endpoints, or shared UI can expose Creator-owned content.
app.UseMiddleware<CreatorResolutionMiddleware>();

// Redirect HTTP requests to HTTPS.
app.UseHttpsRedirection();

// Enable antiforgery protection for interactive server components.
app.UseAntiforgery();

// Expose static assets such as stylesheets, images, and JavaScript files.
app.MapStaticAssets();

/// <summary>
/// Reports whether required Creator and Resource validation completed for the
/// current application instance.
/// </summary>
app.MapGet(
    "/health",
    (ApplicationReadinessState readinessState) =>
    {
        var response = new
        {
            status = readinessState.IsReady ? "Healthy" : "Unhealthy",
            resourcesValidated = readinessState.ResourcesValidated,
            creatorContentValidated = readinessState.CreatorContentValidated
        };

        return readinessState.IsReady
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    });

/// <summary>
/// Resolves a stable public slug and redirects the request to its current
/// canonical target.
/// </summary>
/// <remarks>
/// This endpoint is intentionally content-type independent. The requested slug
/// may eventually represent a destination, experience, journey segment, quote,
/// video, resource, or another addressable platform object.
///
/// The public <c>/go/{slug}</c> address is intended to remain stable even when
/// the internal target route changes.
/// </remarks>
app.MapGet(
    "/go/{slug}",
    async (
        string slug,
        IAddressableContentService addressableContentService,
        ICreatorContextAccessor creatorContextAccessor,
        CancellationToken cancellationToken) =>
    {
        var route =
            await addressableContentService.ResolveAsync(
                creatorContextAccessor.Current.Id,
                slug,
                cancellationToken);

        if (route is null)
        {
            return Results.NotFound(
                $"No published content was found for public slug '{slug}'.");
        }

        return Results.Redirect(
            route.TargetUrl,
            permanent: false);
    });

/// <summary>
/// Generates a scalable SVG QR code for a valid public slug.
/// </summary>
/// <remarks>
/// The QR image encodes the stable <c>/go/{slug}</c> address rather than the
/// current internal content route.
///
/// SVG is the preferred format for printed books and other high-resolution
/// publishing workflows.
/// </remarks>
app.MapGet(
    "/qr/{slug}.svg",
    async (
        string slug,
        IAddressableContentService addressableContentService,
        ICreatorContextAccessor creatorContextAccessor,
        IQrCodeService qrCodeService,
        CancellationToken cancellationToken) =>
    {
        // Validate the address before generating a QR image. Unknown or
        // unpublished targets must not produce public QR assets.
        var route =
            await addressableContentService.ResolveAsync(
                creatorContextAccessor.Current.Id,
                slug,
                cancellationToken);

        if (route is null)
        {
            return Results.NotFound(
                $"No published content was found for public slug '{slug}'.");
        }

        var svg = qrCodeService.GenerateSvg(
            creatorContextAccessor.Current,
            route.Slug);

        return Results.Text(
            svg,
            contentType: "image/svg+xml");
    });

/// <summary>
/// Generates a downloadable PNG QR code for a valid public slug.
/// </summary>
/// <remarks>
/// The PNG endpoint is useful for previews, office documents, email, and
/// systems that do not support SVG assets.
/// </remarks>
app.MapGet(
    "/qr/{slug}.png",
    async (
        string slug,
        IAddressableContentService addressableContentService,
        ICreatorContextAccessor creatorContextAccessor,
        IQrCodeService qrCodeService,
        CancellationToken cancellationToken) =>
    {
        // Resolve the address first so the QR Engine cannot generate a code
        // for missing or unpublished content.
        var route =
            await addressableContentService.ResolveAsync(
                creatorContextAccessor.Current.Id,
                slug,
                cancellationToken);

        if (route is null)
        {
            return Results.NotFound(
                $"No published content was found for public slug '{slug}'.");
        }

        var png = qrCodeService.GeneratePng(
            creatorContextAccessor.Current,
            route.Slug);

        return Results.File(
            png,
            contentType: "image/png",
            fileDownloadName: $"{route.Slug}-qr.png");
    });

// Map the Blazor application and enable interactive server rendering.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>
/// Exposes the top-level application entry point to the integration-test host.
/// </summary>
public partial class Program;
