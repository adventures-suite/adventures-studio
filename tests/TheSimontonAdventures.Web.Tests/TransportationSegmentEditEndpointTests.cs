using System.Security.Claims;
using System.Text;
using AdventuresSuite.Identity;
using Microsoft.AspNetCore.Http;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies safe parsing and redirects at the transportation edit endpoint.</summary>
public sealed class TransportationSegmentEditEndpointTests
{
    private static readonly ActorIdentity Actor = new(
        ActorType.Human, "user_planner_01", new UserId("user_planner_01"));

    /// <summary>A valid form passes route-scoped identity and local schedule values to the service.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_InvokesServiceAndRedirects()
    {
        var context = Context(
            "expectedVersion=6&mode=Rail&from=Madrid&to=Barcelona" +
            "&departureDate=2027-10-28&departureTimeLocal=09%3A00" +
            "&departureTimeZoneId=Europe%2FMadrid&arrivalDate=2027-10-28" +
            "&arrivalTimeLocal=12%3A30&arrivalTimeZoneId=Europe%2FMadrid");
        var service = new RecordingService(new(EditTransportationSegmentOutcome.Updated, 7));

        await TransportationSegmentEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_spain_01", "transport_train_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal(
            "/workspace/creators/creator_alpha_01/plans/plan_spain_01?transportation-edit=updated",
            context.Response.Headers.Location);
        Assert.Equal(new TransportationSegmentId("transport_train_01"),
            service.Command?.TransportationSegmentId);
        Assert.Equal(new TimeOnly(12, 30), service.Command?.ArrivalTimeLocal);
    }

    /// <summary>Malformed local schedule data fails without invoking the service or reflecting input.</summary>
    [Fact]
    public async Task HandleAsync_MalformedTime_DoesNotInvokeServiceOrReflectInput()
    {
        const string privateValue = "PRIVATE-ROUTE-VALUE";
        var context = Context(
            $"expectedVersion=6&mode=Rail&from={privateValue}&to=Barcelona" +
            "&departureDate=2027-10-28&departureTimeLocal=invalid" +
            "&departureTimeZoneId=Europe%2FMadrid&arrivalDate=2027-10-28" +
            "&arrivalTimeZoneId=Europe%2FMadrid");
        var service = new RecordingService(new(EditTransportationSegmentOutcome.Updated, 7));

        await TransportationSegmentEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_spain_01", "transport_train_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Null(service.Command);
        Assert.Equal(
            "/workspace/creators/creator_alpha_01/plans/plan_spain_01?transportation-edit=validation",
            context.Response.Headers.Location);
        Assert.DoesNotContain(privateValue, context.Response.Headers.Location.ToString());
    }

    /// <summary>A malformed route identity uses a generic workspace denial.</summary>
    [Fact]
    public async Task HandleAsync_MalformedIdentity_FailsClosed()
    {
        var context = Context(string.Empty);
        var service = new RecordingService(new(EditTransportationSegmentOutcome.Updated, 7));

        await TransportationSegmentEditEndpoints.HandleAsync(
            context, "INVALID", "plan_spain_01", "transport_train_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Null(service.Command);
        Assert.Equal("/workspace?transportation-edit=denied", context.Response.Headers.Location);
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

    private sealed class RecordingService(EditTransportationSegmentResult result)
        : ITransportationSegmentEditService
    {
        public EditTransportationSegmentCommand? Command { get; private set; }
        public Task<EditTransportationSegmentResult> EditAsync(
            EditTransportationSegmentCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
