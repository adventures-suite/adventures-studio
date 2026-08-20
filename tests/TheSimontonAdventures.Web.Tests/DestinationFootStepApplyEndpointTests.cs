using System.Security.Claims;
using System.Text;
using AdventuresSuite.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies safe parsing and redirects at the Destination FootStep endpoint.</summary>
public sealed class DestinationFootStepApplyEndpointTests
{
    private static readonly ActorIdentity Actor = new(
        ActorType.Human, "user_planner_01", new UserId("user_planner_01"));

    /// <summary>The application boundary is POST-only and requires antiforgery validation.</summary>
    [Fact]
    public void MapEndpoint_IsPostOnlyAndRequiresAntiforgery()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        using var routes = builder.Build();

        routes.MapDestinationFootStepApplyEndpoint();

        var endpoint = ((IEndpointRouteBuilder)routes).DataSources
            .SelectMany(source => source.Endpoints).Single();
        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods);
        Assert.True(endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation);
    }

    /// <summary>A valid reviewed form passes only allowlisted values and redirects safely.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_InvokesServiceAndRedirects()
    {
        var context = Context("expectedVersion=4&idempotencyKey=footstep-apply-key-0001"
            + "&footStepId=footstep_destination_lisbon_gateway&footStepVersion=1.0"
            + "&startDate=2027-05-01&endDate=2027-05-03&timeZoneId=Europe%2FLisbon");
        var service = new RecordingService(new(ApplyDestinationFootStepOutcome.Added, 5));

        await DestinationFootStepApplyEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_portugal_01",
            new ActorResolver(), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans/plan_portugal_01?footstep=added",
            context.Response.Headers.Location);
        Assert.Equal("footstep_destination_lisbon_gateway", service.Command?.FootStepId);
        Assert.Equal("1.0", service.Command?.FootStepVersion);
        Assert.Equal("Europe/Lisbon", service.Command?.TimeZoneId);
        Assert.Equal(4, service.Command?.ExpectedVersion);
    }

    /// <summary>Malformed identities and forms never invoke the mutation service.</summary>
    [Theory]
    [InlineData("INVALID CREATOR", "plan_portugal_01", "")]
    [InlineData("creator_alpha_01", "plan_portugal_01", "expectedVersion=bad")]
    public async Task HandleAsync_InvalidInput_DoesNotInvokeService(
        string creatorId, string planId, string body)
    {
        var context = Context(body);
        var service = new RecordingService(new(ApplyDestinationFootStepOutcome.Added, 5));

        await DestinationFootStepApplyEndpoints.HandleAsync(
            context, creatorId, planId, new ActorResolver(), service, CancellationToken.None);

        Assert.Null(service.Command);
        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Contains("footstep=", context.Response.Headers.Location.ToString(), StringComparison.Ordinal);
    }

    private static DefaultHttpContext Context(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject")], "test"));
        return context;
    }

    private sealed class ActorResolver : IWorkspaceActorResolver
    {
        public ActorIdentity? Resolve(ClaimsPrincipal principal) => Actor;
    }

    private sealed class RecordingService(ApplyDestinationFootStepResult result)
        : IDestinationFootStepApplyService
    {
        public ApplyDestinationFootStepCommand? Command { get; private set; }
        public Task<ApplyDestinationFootStepResult> ApplyAsync(
            ApplyDestinationFootStepCommand command, CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
