using System.Security.Claims;
using System.Text;
using AdventuresSuite.Identity;
using Microsoft.AspNetCore.Http;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies safe parsing and redirect behavior at the destination endpoint.</summary>
public sealed class DestinationVisitAddEndpointTests
{
    private static readonly ActorIdentity Actor = new(
        ActorType.Human, "user_planner_01", new UserId("user_planner_01"));

    /// <summary>A valid form passes only parsed route and allowlisted fields to the service.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_InvokesServiceAndRedirects()
    {
        var context = Context(
            "expectedVersion=4&name=Rome&startDate=2027-05-01&endDate=2027-05-03&timeZoneId=Europe%2FRome");
        var service = new RecordingService(new(AddDestinationVisitOutcome.Added, 5));

        await DestinationVisitAddEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_italy_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal(
            "/workspace/creators/creator_alpha_01/plans/plan_italy_01?destination=added",
            context.Response.Headers.Location);
        Assert.Equal("Rome", service.Command?.Name);
        Assert.Equal("Europe/Rome", service.Command?.TimeZoneId);
        Assert.Equal(4, service.Command?.ExpectedVersion);
    }

    /// <summary>Malformed route identities fail without invoking the mutation service.</summary>
    [Fact]
    public async Task HandleAsync_MalformedRoute_DoesNotInvokeService()
    {
        var context = Context(string.Empty);
        var service = new RecordingService(new(AddDestinationVisitOutcome.Added, 5));

        await DestinationVisitAddEndpoints.HandleAsync(
            context, "INVALID CREATOR", "INVALID PLAN",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Null(service.Command);
        Assert.Equal("/workspace?destination=denied", context.Response.Headers.Location);
    }

    private static DefaultHttpContext Context(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject")], "test"));
        return context;
    }

    private sealed class StubActorResolver(ActorIdentity? actor) : IWorkspaceActorResolver
    {
        public ActorIdentity? Resolve(ClaimsPrincipal principal) => actor;
    }

    private sealed class RecordingService(AddDestinationVisitResult result)
        : IDestinationVisitAddService
    {
        public AddDestinationVisitCommand? Command { get; private set; }
        public Task<AddDestinationVisitResult> AddAsync(
            AddDestinationVisitCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
