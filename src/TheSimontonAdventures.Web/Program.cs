using TheSimontonAdventures.Web.Components;
using TheSimontonAdventures.Web.Services;
using TheSimontonAdventures.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

builder.Services.AddSingleton<
    ITravelContentService,
    JsonTravelContentService>();

builder.Services.AddSingleton<IQrCodeService, QrCodeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();



app.MapStaticAssets();

app.MapGet(
    "/go/{qrSlug}",
    async (
        string qrSlug,
        ITravelContentService travelContentService,
        CancellationToken cancellationToken) =>
    {
        var destinationRoute =
            await travelContentService.GetDestinationRouteByQrSlugAsync(
                qrSlug,
                cancellationToken);

        if (destinationRoute is null)
        {
            return Results.NotFound(
                $"No destination was found for QR slug '{qrSlug}'.");
        }

        return Results.Redirect(
            destinationRoute.DestinationUrl,
            permanent: false);
    });

app.MapGet(
    "/qr/{qrSlug}.svg",
    async (
        string qrSlug,
        ITravelContentService travelContentService,
        IQrCodeService qrCodeService,
        CancellationToken cancellationToken) =>
    {
        var destination =
            await travelContentService.GetDestinationRouteByQrSlugAsync(
                qrSlug,
                cancellationToken);

        if (destination is null)
        {
            return Results.NotFound();
        }

        var svg = qrCodeService.GenerateSvg(qrSlug);

        return Results.Text(
            svg,
            contentType: "image/svg+xml");
    });

app.MapGet(
    "/qr/{qrSlug}.png",
    async (
        string qrSlug,
        ITravelContentService travelContentService,
        IQrCodeService qrCodeService,
        CancellationToken cancellationToken) =>
    {
        var destination =
            await travelContentService.GetDestinationRouteByQrSlugAsync(
                qrSlug,
                cancellationToken);

        if (destination is null)
        {
            return Results.NotFound();
        }

        var png = qrCodeService.GeneratePng(qrSlug);

        return Results.File(
            png,
            contentType: "image/png",
            fileDownloadName: $"{qrSlug}-qr.png");
    });

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
