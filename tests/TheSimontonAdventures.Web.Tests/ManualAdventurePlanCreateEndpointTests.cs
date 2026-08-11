using System.Security.Claims;
using AdventuresSuite.Identity;
using AdventuresSuite.Identity.ExternalId;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the cookie form boundary exposes only safe creation behavior.</summary>
public sealed class ManualAdventurePlanCreateEndpointTests
{
    /// <summary>The creation boundary is POST-only and explicitly requires antiforgery validation.</summary>
    [Fact]
    public void MapEndpoint_IsPostOnlyAndRequiresAntiforgery()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        using var routes = builder.Build();

        routes.MapManualAdventurePlanCreateEndpoint();

        var endpoint = ((IEndpointRouteBuilder)routes).DataSources
            .SelectMany(source => source.Endpoints).Single();
        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods);
        Assert.True(endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation);
    }

    /// <summary>An authenticated application actor is resolved and a success uses Post/Redirect/Get.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_RedirectsToExistingDetailRoute()
    {
        var service = new RecordingService(ManualAdventurePlanCreateResult.Success(
            ManualAdventurePlanCreateOutcome.Created, new("plan_created_01")));
        var context = Context(Form());

        await ManualAdventurePlanCreateEndpoints.HandleAsync(
            context, "creator_alpha_01", new WorkspaceActorResolver(), service, default);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans/plan_created_01",
            context.Response.Headers.Location);
        Assert.Equal(new UserId("user_planner_01"), service.Command!.Actor.UserId);
        Assert.Equal("Desert weekend", service.Command.Title);
        Assert.Equal(new DateOnly(2026, 11, 3), service.Command.EndDate);
    }

    /// <summary>Anonymous requests never call the creation service.</summary>
    [Fact]
    public async Task HandleAsync_AnonymousRequest_IsSafelyDenied()
    {
        var service = new RecordingService(ManualAdventurePlanCreateResult.Safe(
            ManualAdventurePlanCreateOutcome.Failed));
        var context = Context(Form());
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        await ManualAdventurePlanCreateEndpoints.HandleAsync(
            context, "creator_alpha_01", new WorkspaceActorResolver(), service, default);

        Assert.Null(service.Command);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans?create=denied",
            context.Response.Headers.Location);
    }

    /// <summary>Conflict and failure redirects never reflect submitted private content.</summary>
    [Theory]
    [InlineData(ManualAdventurePlanCreateOutcome.Conflict, "conflict")]
    [InlineData(ManualAdventurePlanCreateOutcome.Failed, "failure")]
    public async Task HandleAsync_NonSuccess_DoesNotLeakRequest(
        ManualAdventurePlanCreateOutcome outcome, string state)
    {
        const string secret = "PRIVATE-TITLE-DO-NOT-REFLECT";
        var service = new RecordingService(ManualAdventurePlanCreateResult.Safe(outcome));
        var context = Context(Form(secret));

        await ManualAdventurePlanCreateEndpoints.HandleAsync(
            context, "creator_alpha_01", new WorkspaceActorResolver(), service, default);

        Assert.Equal($"/workspace/creators/creator_alpha_01/plans?create={state}",
            context.Response.Headers.Location);
        Assert.DoesNotContain(secret, context.Response.Headers.Location.ToString());
    }

    private static DefaultHttpContext Context(IFormCollection form)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ApplicationUserClaims.UserId, "user_planner_01")], "cookie"))
        };
        context.Features.Set<IFormFeature>(new FormFeature(form));
        return context;
    }

    private static IFormCollection Form(string title = "Desert weekend") => new FormCollection(
        new Dictionary<string, StringValues>
        {
            ["idempotencyKey"] = "request_1234567890",
            ["title"] = title,
            ["description"] = "Private draft",
            ["startDate"] = "2026-11-01",
            ["endDate"] = "2026-11-03"
        });

    private sealed class RecordingService(ManualAdventurePlanCreateResult result)
        : IManualAdventurePlanCreateService
    {
        public ManualAdventurePlanCreateCommand? Command { get; private set; }
        public Task<ManualAdventurePlanCreateResult> CreateAsync(
            ManualAdventurePlanCreateCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
