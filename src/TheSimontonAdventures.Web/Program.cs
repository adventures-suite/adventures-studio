using TheSimontonAdventures.Web.Components;
using TheSimontonAdventures.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<
    ITravelContentService,
    JsonTravelContentService>();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
