using System.Reflection;
using AdventuresSuite.Identity.ExternalId;
using AdventuresSuite.Authorization.SqlServer;
using AdventuresSuite.Planning.SqlServer;
using TheSimontonAdventures.Web.Components;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Resources;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;
using TheSimontonAdventures.Web.Services;
using TheSimontonAdventures.Web.Validation;

var builder = WebApplication.CreateBuilder(args);

// Register Razor Components and enable interactive server-side rendering.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();

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
builder.Services.AddScoped<TrustedRequestHostContextAccessor>();
builder.Services.AddScoped<ITrustedRequestHostContextAccessor>(services =>
    services.GetRequiredService<TrustedRequestHostContextAccessor>());

// Activate private identity only when the complete environment-backed Slice 5F
// configuration is present. Missing or partial external-provider state fails
// startup instead of falling back to public-only or development identity.
builder.AddAdventuresSuiteAuthentication();
var authenticationMode = builder.Configuration["Authentication:Mode"];
if (string.Equals(authenticationMode, nameof(AuthenticationMode.ExternalProvider), StringComparison.OrdinalIgnoreCase))
{
    var planningConnectionString = builder.Configuration["Authentication:SqlConnectionString"];
    if (string.IsNullOrWhiteSpace(planningConnectionString))
    {
        throw new InvalidOperationException(
            "The Planner workspace requires the approved authentication SQL connection string.");
    }

    builder.Services.AddSingleton<IPlanningTransactionFactory>(
        new SqlPlanningTransactionFactory(planningConnectionString));
    builder.Services.AddSingleton<ICreatorMembershipTransactionFactory>(
        new SqlCreatorMembershipTransactionFactory(planningConnectionString));
    builder.Services.AddSingleton<ICreatorMembershipProvider, TransactionalCreatorMembershipProvider>();
    builder.Services.AddSingleton<IAuthorizationResourceFactsProvider, PlanningAuthorizationResourceFactsProvider>();
    builder.Services.AddSingleton<IAuthorizationPolicyEvaluator, AuthorizationPolicyEvaluator>();
    builder.Services.AddSingleton<IWorkspaceActorResolver, WorkspaceActorResolver>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddScoped<IPlannerWorkspaceQueryService, PlannerWorkspaceQueryService>();
    builder.Services.AddSingleton<IPlanningCreationIdentityGenerator, GuidPlanningCreationIdentityGenerator>();
    builder.Services.AddScoped<IManualAdventurePlanCreateService, ManualAdventurePlanCreateService>();
}

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

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "AdventuresSuite.Antiforgery.Development"
        : "__Host-AdventuresSuite.Antiforgery";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.HeaderName = "X-AdventuresSuite-Antiforgery";
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

var app = builder.Build();
var authenticationConfiguration =
    app.Services.GetRequiredService<AuthenticationConfiguration>();

// Emit one structured startup event after hosted validation has completed and
// the server is ready to accept traffic. Deployment identifiers are supplied
// by CI and remain "local" for developer-started instances.
app.Lifetime.ApplicationStarted.Register(() =>
{
    var readinessState =
        app.Services.GetRequiredService<ApplicationReadinessState>();

    app.Logger.LogInformation(
        "Application started for deployment {CommitSha} (workflow run " +
        "{RunId}, attempt {RunAttempt}) in {Environment}. Resource validation: " +
        "{ResourcesValidated}; Creator content validation: " +
        "{CreatorContentValidated}.",
        app.Configuration["Deployment:CommitSha"] ?? "local",
        app.Configuration["Deployment:RunId"] ?? "local",
        app.Configuration["Deployment:RunAttempt"] ?? "local",
        app.Environment.EnvironmentName,
        readinessState.ResourcesValidated,
        readinessState.CreatorContentValidated);
});

// Configure production-only error handling and HTTP Strict Transport Security.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    // HSTS is deliberately omitted in Development so local HTTP remains usable.
    app.UseHsts();
}

// Apply only explicitly configured proxy addresses before request scheme, host,
// authentication, redirects, or workspace-origin decisions are evaluated.
if (authenticationConfiguration.Mode != AuthenticationMode.Disabled)
{
    app.UseForwardedHeaders();
}

// Apply browser defenses to dynamic pages, framework endpoints, and static
// assets, including host denials and fallback responses. HSTS remains
// production-only through UseHsts above.
app.UseMiddleware<BrowserSecurityHeadersMiddleware>();

// Resolve the explicitly approved request host before status-page re-execution,
// static assets, endpoints, or shared UI can expose Creator-owned content.
app.UseMiddleware<CreatorResolutionMiddleware>();

// Redirect HTTP requests to HTTPS.
app.UseHttpsRedirection();

// Slice 5F supplies the external authentication services and active
// configuration. Keeping this branch here fixes authentication ahead of every
// antiforgery decision without activating private identity in Slice 5E.
if (authenticationConfiguration.Mode != AuthenticationMode.Disabled)
{
    app.UseAuthentication();
}

// Reject cookie-bearing Blazor negotiate, reconnect, WebSocket, SSE, and long-
// polling requests unless they carry the exact configured workspace Origin.
app.UseMiddleware<WorkspaceSignalROriginMiddleware>();

// Validate antiforgery endpoint metadata used by Razor Components and forms.
app.UseAntiforgery();

// Require antiforgery proof by default for future cookie-authenticated HTTP
// mutations, even if an endpoint author forgets to add antiforgery metadata.
app.UseMiddleware<CookieAuthenticatedAntiforgeryMiddleware>();

// Expose static assets such as stylesheets, images, and JavaScript files.
app.MapStaticAssets();

/// <summary>
/// Reports whether required Creator and Resource validation completed for the
/// current application instance.
/// </summary>
app.MapGet(
    "/health",
    (ApplicationReadinessState readinessState,
        IServiceProvider services) =>
    {
        var authenticationReady = authenticationConfiguration.Mode == AuthenticationMode.Disabled
            || services.GetRequiredService<AuthenticationReadinessState>().IsReady;
        var response = new
        {
            status = readinessState.IsReady && authenticationReady ? "Healthy" : "Unhealthy",
            deploymentVersion =
                typeof(Program).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unknown",
            resourcesValidated = readinessState.ResourcesValidated,
            creatorContentValidated = readinessState.CreatorContentValidated,
            authenticationReady
        };

        return readinessState.IsReady && authenticationReady
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    });

if (authenticationConfiguration.Mode == AuthenticationMode.ExternalProvider)
{
    app.MapAdventuresSuiteExternalIdEndpoints(authenticationConfiguration);
    app.MapManualAdventurePlanCreateEndpoint();
}

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
