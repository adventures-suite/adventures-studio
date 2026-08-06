using TheSimontonAdventures.Web.Components;
using TheSimontonAdventures.Web.Configuration;
using TheSimontonAdventures.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Register Razor Components and enable interactive server-side rendering.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Bind and validate the platform-wide configuration settings.
//
// Environment variables may override these values at deployment time.
// For example, Azure App Service uses:
// Platform__PublicBaseUrl
builder.Services
    .AddOptions<PlatformOptions>()
    .Bind(builder.Configuration.GetSection(PlatformOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => Uri.TryCreate(
            options.PublicBaseUrl,
            UriKind.Absolute,
            out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps),
        $"{PlatformOptions.SectionName}:PublicBaseUrl must be a valid HTTP or HTTPS URL.")
    .ValidateOnStart();

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

// Re-execute unsuccessful requests through the shared not-found page.
app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

// Redirect HTTP requests to HTTPS.
app.UseHttpsRedirection();

// Enable antiforgery protection for interactive server components.
app.UseAntiforgery();

// Expose static assets such as stylesheets, images, and JavaScript files.
app.MapStaticAssets();

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
        CancellationToken cancellationToken) =>
    {
        var route =
            await addressableContentService.ResolveAsync(
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
        IQrCodeService qrCodeService,
        CancellationToken cancellationToken) =>
    {
        // Validate the address before generating a QR image. Unknown or
        // unpublished targets must not produce public QR assets.
        var route =
            await addressableContentService.ResolveAsync(
                slug,
                cancellationToken);

        if (route is null)
        {
            return Results.NotFound(
                $"No published content was found for public slug '{slug}'.");
        }

        var svg = qrCodeService.GenerateSvg(route.Slug);

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
        IQrCodeService qrCodeService,
        CancellationToken cancellationToken) =>
    {
        // Resolve the address first so the QR Engine cannot generate a code
        // for missing or unpublished content.
        var route =
            await addressableContentService.ResolveAsync(
                slug,
                cancellationToken);

        if (route is null)
        {
            return Results.NotFound(
                $"No published content was found for public slug '{slug}'.");
        }

        var png = qrCodeService.GeneratePng(route.Slug);

        return Results.File(
            png,
            contentType: "image/png",
            fileDownloadName: $"{route.Slug}-qr.png");
    });

// Map the Blazor application and enable interactive server rendering.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();