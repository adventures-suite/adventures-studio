using System.Security.Claims;
using System.Text;
using AdventuresSuite.Identity;
using Microsoft.AspNetCore.Http;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies safe parsing and redirects at the itinerary-day edit endpoint.</summary>
public sealed class ItineraryDayEditEndpointTests
{
    private static readonly ActorIdentity Actor = new(
        ActorType.Human, "user_planner_01", new UserId("user_planner_01"));

    /// <summary>A valid form passes only allowlisted desired state to the edit service.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_InvokesServiceAndRedirects()
    {
        var context = Context("expectedVersion=6&title=Arrival%20in%20Rome&ignored=private");
        var service = new RecordingService(new(EditItineraryDayOutcome.Updated, 7));

        await ItineraryDayEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_italy_01", "day_rome_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans/plan_italy_01?day-edit=updated",
            context.Response.Headers.Location);
        Assert.Equal(new ItineraryDayId("day_rome_01"), service.Command?.ItineraryDayId);
        Assert.Equal("Arrival in Rome", service.Command?.Title);
    }

    /// <summary>Malformed input fails without invoking the service or reflecting submitted content.</summary>
    [Fact]
    public async Task HandleAsync_MalformedVersion_DoesNotInvokeServiceOrReflectContent()
    {
        var context = Context("expectedVersion=invalid&title=PRIVATE-CONTENT");
        var service = new RecordingService(new(EditItineraryDayOutcome.Updated, 7));

        await ItineraryDayEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_italy_01", "day_rome_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Null(service.Command);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans/plan_italy_01?day-edit=validation",
            context.Response.Headers.Location);
        Assert.DoesNotContain("PRIVATE-CONTENT", context.Response.Headers.Location.ToString());
    }

    /// <summary>Every service outcome maps to a fixed allowlisted PRG value.</summary>
    [Theory]
    [InlineData(EditItineraryDayOutcome.Unchanged, "unchanged")]
    [InlineData(EditItineraryDayOutcome.Denied, "denied")]
    [InlineData(EditItineraryDayOutcome.Conflict, "conflict")]
    [InlineData(EditItineraryDayOutcome.ValidationFailed, "validation")]
    [InlineData(EditItineraryDayOutcome.Failed, "failure")]
    public async Task HandleAsync_Result_UsesAllowlistedRedirect(
        EditItineraryDayOutcome outcome,
        string state)
    {
        var context = Context("expectedVersion=6&title=Rome");

        await ItineraryDayEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_italy_01", "day_rome_01",
            new StubActorResolver(Actor), new RecordingService(new(outcome)),
            CancellationToken.None);

        Assert.Equal(
            $"/workspace/creators/creator_alpha_01/plans/plan_italy_01?day-edit={state}",
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

    private sealed class RecordingService(EditItineraryDayResult result) : IItineraryDayEditService
    {
        public EditItineraryDayCommand? Command { get; private set; }
        public Task<EditItineraryDayResult> EditAsync(
            EditItineraryDayCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
