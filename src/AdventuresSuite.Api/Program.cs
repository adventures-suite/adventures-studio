using System.Text.Json;
using AdventuresSuite.Api;
using AdventuresSuite.Companion.Application;
using AdventuresSuite.Companion.Contracts;
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
}

activationMode ??= CompanionApiConstants.DisabledActivationMode;
releaseSha ??= "0000000000000000000000000000000000000000";

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
    builder.Services.AddSingleton<ICompanionProjectionService, DeterministicCompanionProjectionService>();
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
app.MapGet("/health/ready", () => Results.Json(
    health,
    CompanionJsonSerializerContext.Default.CompanionHealthDto)).ExcludeFromDescription();
app.MapCompanionApi();
app.Run();

/// <summary>Provides the API entry point to integration tests.</summary>
public partial class Program;
