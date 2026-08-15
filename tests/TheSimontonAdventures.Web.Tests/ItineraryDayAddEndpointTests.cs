using System.Security.Claims;
using System.Text;
using AdventuresSuite.Identity;
using Microsoft.AspNetCore.Http;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies safe parsing and redirects at the itinerary-day endpoint.</summary>
public sealed class ItineraryDayAddEndpointTests
{
    private static readonly ActorIdentity Actor = new(
        ActorType.Human, "user_planner_01", new UserId("user_planner_01"));

    /// <summary>A valid form passes parsed route and allowlisted fields to the service.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_InvokesServiceAndRedirects()
    {
        var context = Context(
            "expectedVersion=5&destinationVisitId=visit_rome_01&date=2027-05-02&title=Rome+arrival");
        var service = new RecordingService(new(AddItineraryDayOutcome.Added, 6));

        await ItineraryDayAddEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_italy_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal(
            "/workspace/creators/creator_alpha_01/plans/plan_italy_01?day=added",
            context.Response.Headers.Location);
        Assert.Equal(new DestinationVisitId("visit_rome_01"), service.Command?.DestinationVisitId);
        Assert.Equal("Rome arrival", service.Command?.Title);
    }

    /// <summary>A malformed visit identity fails without invoking the service.</summary>
    [Fact]
    public async Task HandleAsync_MalformedVisit_DoesNotInvokeService()
    {
        var context = Context(
            "expectedVersion=5&destinationVisitId=INVALID+VISIT&date=2027-05-02&title=Private");
        var service = new RecordingService(new(AddItineraryDayOutcome.Added, 6));

        await ItineraryDayAddEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_italy_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Null(service.Command);
        Assert.Equal(
            "/workspace/creators/creator_alpha_01/plans/plan_italy_01?day=validation",
            context.Response.Headers.Location);
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

    private sealed class RecordingService(AddItineraryDayResult result) : IItineraryDayAddService
    {
        public AddItineraryDayCommand? Command { get; private set; }
        public Task<AddItineraryDayResult> AddAsync(
            AddItineraryDayCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
