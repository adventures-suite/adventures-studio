using System.Security.Claims;
using System.Text;
using AdventuresSuite.Identity;
using Microsoft.AspNetCore.Http;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies safe parsing and redirects at the reservation endpoint.</summary>
public sealed class ReservationAddEndpointTests
{
    /// <summary>A valid form invokes the service without accepting confirmation credentials.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_InvokesServiceAndRedirects()
    {
        var actor = new ActorIdentity(ActorType.Human, "user_01", new("user_01"));
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            "expectedVersion=8&subject=Prado+Museum&confirmationReference=SECRET-123"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var service = new RecordingService(new(AddReservationOutcome.Added, 9));

        await ReservationAddEndpoints.HandleAsync(
            context,
            "creator_alpha_01",
            "plan_spain_01",
            new ActorResolver(actor),
            service,
            CancellationToken.None);

        Assert.Equal("Prado Museum", service.Command?.Subject);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans/plan_spain_01?reservation=added",
            context.Response.Headers.Location);
    }

    private sealed class ActorResolver(ActorIdentity actor) : IWorkspaceActorResolver
    {
        public ActorIdentity? Resolve(ClaimsPrincipal principal) => actor;
    }

    private sealed class RecordingService(AddReservationResult result) : IReservationAddService
    {
        public AddReservationCommand? Command { get; private set; }
        public Task<AddReservationResult> AddAsync(
            AddReservationCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
