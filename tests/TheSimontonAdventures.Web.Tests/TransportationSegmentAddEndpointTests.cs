using System.Security.Claims;
using System.Text;
using AdventuresSuite.Identity;
using Microsoft.AspNetCore.Http;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies safe parsing and redirects at the transportation endpoint.</summary>
public sealed class TransportationSegmentAddEndpointTests
{
    private static readonly ActorIdentity Actor = new(
        ActorType.Human, "user_planner_01", new UserId("user_planner_01"));

    /// <summary>A valid form passes local values without provider-specific translation.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_InvokesServiceAndRedirects()
    {
        var context = Context("expectedVersion=7&mode=Flight&from=Phoenix&to=Rome&departureDate=2027-01-02&departureTimeLocal=10%3A00&departureTimeZoneId=America%2FPhoenix&arrivalDate=2027-01-03&arrivalTimeLocal=09%3A00&arrivalTimeZoneId=Europe%2FRome");
        var service = new RecordingService(new(AddTransportationSegmentOutcome.Added, 8));

        await TransportationSegmentAddEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_italy_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("Europe/Rome", service.Command?.ArrivalTimeZoneId);
        Assert.Equal(new TimeOnly(10, 0), service.Command?.DepartureTimeLocal);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans/plan_italy_01?transportation=added",
            context.Response.Headers.Location);
    }

    /// <summary>Malformed local time fails without invoking the service or reflecting form data.</summary>
    [Fact]
    public async Task HandleAsync_MalformedTime_DoesNotInvokeService()
    {
        var context = Context("expectedVersion=7&mode=SECRET&from=A&to=B&departureDate=2027-01-02&departureTimeLocal=bad&departureTimeZoneId=UTC&arrivalDate=2027-01-03&arrivalTimeZoneId=UTC");
        var service = new RecordingService(new(AddTransportationSegmentOutcome.Added, 8));

        await TransportationSegmentAddEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_italy_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Null(service.Command);
        Assert.DoesNotContain("SECRET", context.Response.Headers.Location.ToString());
        Assert.EndsWith("?transportation=validation", context.Response.Headers.Location.ToString());
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

    private sealed class RecordingService(AddTransportationSegmentResult result)
        : ITransportationSegmentAddService
    {
        public AddTransportationSegmentCommand? Command { get; private set; }
        public Task<AddTransportationSegmentResult> AddAsync(
            AddTransportationSegmentCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
