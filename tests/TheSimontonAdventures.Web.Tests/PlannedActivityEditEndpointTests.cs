using System.Security.Claims;
using System.Text;
using AdventuresSuite.Identity;
using Microsoft.AspNetCore.Http;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies safe parsing and redirects at the planned-activity edit endpoint.</summary>
public sealed class PlannedActivityEditEndpointTests
{
    private static readonly ActorIdentity Actor = new(
        ActorType.Human, "user_planner_01", new UserId("user_planner_01"));

    /// <summary>A valid form passes allowlisted desired state to the edit service.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_InvokesServiceAndRedirects()
    {
        var context = Context("expectedVersion=6&title=Museum&startsAtLocal=10%3A00&endsAtLocal=12%3A30");
        var service = new RecordingService(new(EditPlannedActivityOutcome.Updated, 7));

        await PlannedActivityEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_italy_01", "activity_museum_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans/plan_italy_01?activity-edit=updated",
            context.Response.Headers.Location);
        Assert.Equal(new PlannedActivityId("activity_museum_01"), service.Command?.PlannedActivityId);
        Assert.Equal(new TimeOnly(12, 30), service.Command?.EndsAtLocal);
    }

    /// <summary>Malformed local time fails without invoking the service.</summary>
    [Fact]
    public async Task HandleAsync_MalformedTime_DoesNotInvokeService()
    {
        var context = Context("expectedVersion=6&title=Private&startsAtLocal=invalid");
        var service = new RecordingService(new(EditPlannedActivityOutcome.Updated, 7));

        await PlannedActivityEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_italy_01", "activity_museum_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Null(service.Command);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans/plan_italy_01?activity-edit=validation",
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

    private sealed class RecordingService(EditPlannedActivityResult result) : IPlannedActivityEditService
    {
        public EditPlannedActivityCommand? Command { get; private set; }
        public Task<EditPlannedActivityResult> EditAsync(
            EditPlannedActivityCommand command, CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
