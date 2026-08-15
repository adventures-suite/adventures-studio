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

/// <summary>Verifies the safe cookie form boundary for overview edits.</summary>
public sealed class AdventurePlanOverviewEditEndpointTests
{
    /// <summary>The endpoint is POST-only and explicitly requires antiforgery validation.</summary>
    [Fact]
    public void MapEndpoint_IsPostOnlyAndRequiresAntiforgery()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        using var routes = builder.Build();

        routes.MapAdventurePlanOverviewEditEndpoint();

        var endpoint = ((IEndpointRouteBuilder)routes).DataSources
            .SelectMany(source => source.Endpoints).Single();
        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods);
        Assert.True(endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation);
    }

    /// <summary>A valid form resolves the human actor and redirects to authorized detail.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_UsesPostRedirectGet()
    {
        var service = new RecordingService(new(EditAdventurePlanOverviewOutcome.Updated, 8));
        var context = Context(Form());

        await AdventurePlanOverviewEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_overview_01",
            new WorkspaceActorResolver(), service, default);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal(
            "/workspace/creators/creator_alpha_01/plans/plan_overview_01?edit=updated",
            context.Response.Headers.Location);
        Assert.Equal(new UserId("user_planner_01"), service.Command!.Actor.UserId);
        Assert.Equal(7, service.Command.ExpectedVersion);
        Assert.Equal(new DateOnly(2027, 1, 5), service.Command.EndDate);
    }

    /// <summary>An anonymous request never invokes the application service.</summary>
    [Fact]
    public async Task HandleAsync_Anonymous_IsDeniedBeforeService()
    {
        var service = new RecordingService(new(EditAdventurePlanOverviewOutcome.Failed));
        var context = Context(Form());
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        await AdventurePlanOverviewEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_overview_01",
            new WorkspaceActorResolver(), service, default);

        Assert.Null(service.Command);
        Assert.EndsWith("?edit=denied", context.Response.Headers.Location.ToString());
    }

    /// <summary>Only allowlisted outcome labels appear in redirects; private input is never reflected.</summary>
    [Theory]
    [InlineData(EditAdventurePlanOverviewOutcome.Unchanged, "unchanged")]
    [InlineData(EditAdventurePlanOverviewOutcome.Denied, "denied")]
    [InlineData(EditAdventurePlanOverviewOutcome.Conflict, "conflict")]
    [InlineData(EditAdventurePlanOverviewOutcome.ValidationFailed, "validation")]
    [InlineData(EditAdventurePlanOverviewOutcome.DateChangeBlocked, "date-blocked")]
    [InlineData(EditAdventurePlanOverviewOutcome.Failed, "failure")]
    public async Task HandleAsync_SafeOutcome_DoesNotLeakSubmittedContent(
        EditAdventurePlanOverviewOutcome outcome,
        string state)
    {
        const string secret = "PRIVATE-TITLE-DO-NOT-REFLECT";
        var service = new RecordingService(new(outcome));
        var context = Context(Form(secret));

        await AdventurePlanOverviewEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_overview_01",
            new WorkspaceActorResolver(), service, default);

        Assert.EndsWith($"?edit={state}", context.Response.Headers.Location.ToString());
        Assert.DoesNotContain(secret, context.Response.Headers.Location.ToString());
        Assert.Equal(0, context.Response.ContentLength ?? 0);
    }

    /// <summary>Malformed expected versions and dates never invoke the service.</summary>
    [Theory]
    [InlineData("not-a-version", "2027-01-01", "2027-01-05")]
    [InlineData("7", "01/01/2027", "2027-01-05")]
    [InlineData("7", "2027-01-01", "January 5")]
    public async Task HandleAsync_MalformedForm_IsSafelyRejected(
        string version, string start, string end)
    {
        var service = new RecordingService(new(EditAdventurePlanOverviewOutcome.Updated));
        var context = Context(Form(version: version, start: start, end: end));

        await AdventurePlanOverviewEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_overview_01",
            new WorkspaceActorResolver(), service, default);

        Assert.Null(service.Command);
        Assert.EndsWith("?edit=validation", context.Response.Headers.Location.ToString());
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

    private static IFormCollection Form(
        string title = "Updated title",
        string version = "7",
        string start = "2027-01-01",
        string end = "2027-01-05") => new FormCollection(
        new Dictionary<string, StringValues>
        {
            ["expectedVersion"] = version,
            ["title"] = title,
            ["description"] = "Private description",
            ["startDate"] = start,
            ["endDate"] = end
        });

    private sealed class RecordingService(EditAdventurePlanOverviewResult result)
        : IAdventurePlanOverviewEditService
    {
        public EditAdventurePlanOverviewCommand? Command { get; private set; }
        public Task<EditAdventurePlanOverviewResult> EditAsync(
            EditAdventurePlanOverviewCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
