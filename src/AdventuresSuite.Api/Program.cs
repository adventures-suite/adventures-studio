using System.Text.Json;
using AdventuresSuite.Api;
using AdventuresSuite.Companion.Application;
using AdventuresSuite.Companion.Contracts;
using AdventuresSuite.Companion.SqlServer;
using TheSimontonAdventures.Web.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var deterministicMode = builder.Configuration.GetValue<bool>(CompanionApiConstants.DeterministicModeKey);
var activationMode = builder.Configuration[CompanionApiConstants.ActivationModeKey];
var projectionProvider = builder.Configuration[CompanionApiConstants.ProjectionProviderKey];
var releaseSha = builder.Configuration[CompanionApiConstants.ReleaseShaKey];
if (deterministicMode && !builder.Environment.IsEnvironment("Test"))
{
    throw new InvalidOperationException("The deterministic Companion adapter can activate only in Test.");
}
if (!builder.Environment.IsEnvironment("Test"))
{
    if (!string.Equals(activationMode, CompanionApiConstants.DisabledActivationMode, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Companion:ActivationMode must be explicitly set to Disabled until production activation gates pass.");
    }

    if (releaseSha is null
        || releaseSha.Length != 40
        || releaseSha.Any(value => value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
    {
        throw new InvalidOperationException("Deployment:CommitSha must contain the exact lowercase 40-character release SHA.");
    }

    if (projectionProvider is not (CompanionApiConstants.ClosedProjectionProvider
        or CompanionApiConstants.SqlProjectionProvider))
    {
        throw new InvalidOperationException("Companion:ProjectionProvider must be explicitly Closed or Sql.");
    }
}

activationMode ??= CompanionApiConstants.DisabledActivationMode;
releaseSha ??= "0000000000000000000000000000000000000000";
projectionProvider ??= CompanionApiConstants.ClosedProjectionProvider;

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.MaxDepth = 32;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, CompanionJsonSerializerContext.Default);
});
builder.Services.AddOpenApi(CompanionApiConstants.OpenApiDocumentName, options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
    options.AddDocumentTransformer<CompanionOpenApiDocumentTransformer>();
});
builder.Services.AddSingleton<ISupportIdProvider, SequentialSupportIdProvider>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, CompanionAuthorizationResultHandler>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = deterministicMode
        ? TestCompanionAuthenticationHandler.SchemeName
        : ClosedCompanionAuthenticationHandler.SchemeName;
    options.DefaultChallengeScheme = options.DefaultAuthenticateScheme;
})
    .AddScheme<AuthenticationSchemeOptions, ClosedCompanionAuthenticationHandler>(
        ClosedCompanionAuthenticationHandler.SchemeName, _ => { });

if (deterministicMode)
{
    builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestCompanionAuthenticationHandler>(
        TestCompanionAuthenticationHandler.SchemeName, _ => { });
    builder.Services.AddSingleton<TimeProvider>(
        new FixedTimeProvider(new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero)));
    builder.Services.AddSingleton<DeterministicCompanionAuthorizationFacts>();
    builder.Services.AddSingleton<ICreatorMembershipProvider>(provider =>
        provider.GetRequiredService<DeterministicCompanionAuthorizationFacts>());
    builder.Services.AddSingleton<IAuthorizationResourceFactsProvider>(provider =>
        provider.GetRequiredService<DeterministicCompanionAuthorizationFacts>());
    builder.Services.AddSingleton<IAuthorizationPolicyEvaluator, AuthorizationPolicyEvaluator>();
    builder.Services.AddSingleton<ICompanionTodayQuery, DeterministicCompanionTodayQuery>();
    builder.Services.AddSingleton<ICompanionProjectionService, DeterministicCompanionProjectionService>();
}
else if (projectionProvider == CompanionApiConstants.SqlProjectionProvider)
{
    var sqlConnectionString = CompanionSqlConfiguration.Validate(
        builder.Configuration["Companion:Sql:ConnectionString"],
        builder.Configuration["Companion:Sql:ApprovedServer"],
        builder.Configuration["Companion:Sql:ApprovedDatabase"],
        builder.Configuration["Companion:Sql:ManagedIdentityClientId"]);
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton(new SqlCompanionAdventureQueries(sqlConnectionString));
    builder.Services.AddSingleton<ICompanionAdventureSummaryQuery>(provider =>
        provider.GetRequiredService<SqlCompanionAdventureQueries>());
    builder.Services.AddSingleton<ICompanionAdventureDetailQuery>(provider =>
        provider.GetRequiredService<SqlCompanionAdventureQueries>());
    builder.Services.AddSingleton<ICompanionTodayQuery, ClosedCompanionTodayQuery>();
    builder.Services.AddSingleton<ICompanionProjectionService, AuthoritativeCompanionProjectionService>();
    builder.Services.AddSingleton<ICompanionSqlReadinessProbe>(
        new CompanionSqlReadinessProbe(sqlConnectionString));
}
else
{
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<ICompanionProjectionService, ClosedCompanionProjectionService>();
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(CompanionApiConstants.AuthorizationPolicy, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", DeterministicCompanionProjectionService.RequiredScope);
    });

var app = builder.Build();
app.UseExceptionHandler(exceptionApplication =>
{
    exceptionApplication.Run(async context =>
    {
        var supportId = context.RequestServices.GetRequiredService<ISupportIdProvider>().Create();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers["X-Support-Id"] = supportId;
        context.Response.Headers.CacheControl = "no-store";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            CompanionProblems.Create(StatusCodes.Status500InternalServerError, "temporarily_unavailable", supportId),
            CompanionJsonSerializerContext.Default.CompanionProblemDto,
            context.RequestAborted);
    });
});
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
{
    app.MapOpenApi("/openapi/{documentName}.json");
    app.MapScalarApiReference("/scalar/companion", options =>
    {
        options.WithTitle("AdventuresCompanion API v1");
        options.WithOpenApiRoutePattern($"/openapi/{CompanionApiConstants.OpenApiDocumentName}.json");
    });
}

var health = new CompanionHealthDto
{
    Status = "Healthy",
    Service = CompanionApiConstants.ServiceName,
    ReleaseSha = releaseSha,
    ActivationState = activationMode
};
app.MapGet("/health/live", () => Results.Json(
    health,
    CompanionJsonSerializerContext.Default.CompanionHealthDto)).ExcludeFromDescription();
app.MapGet("/health/ready", async (HttpContext context) =>
{
    var probe = context.RequestServices.GetService<ICompanionSqlReadinessProbe>();
    var ready = projectionProvider != CompanionApiConstants.SqlProjectionProvider;
    var failureCategory = "None";
    if (projectionProvider == CompanionApiConstants.SqlProjectionProvider)
    {
        if (probe is null)
        {
            failureCategory = "ProviderUnavailable";
        }
        else
        {
            try
            {
                ready = await probe.IsReadyAsync(context.RequestAborted);
                if (!ready) failureCategory = "ProbeRejected";
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (CompanionSqlReadinessException exception)
            {
                failureCategory = exception.Category.ToString();
            }
        }
    }
    if (!ready)
    {
        context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("CompanionReadiness")
            .LogWarning("Companion SQL readiness failed with category {FailureCategory}.", failureCategory);
    }
    var readiness = health with { Status = ready ? "Healthy" : "Unhealthy" };
    return Results.Json(
        readiness,
        CompanionJsonSerializerContext.Default.CompanionHealthDto,
        statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}).ExcludeFromDescription();
app.MapCompanionApi();
app.Run();

/// <summary>Provides the API entry point to integration tests.</summary>
public partial class Program;
