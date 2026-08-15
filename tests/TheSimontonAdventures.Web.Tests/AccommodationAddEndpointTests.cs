using System.Security.Claims;
using System.Text;
using AdventuresSuite.Identity;
using Microsoft.AspNetCore.Http;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies safe parsing and redirects at the accommodation endpoint.</summary>
public sealed class AccommodationAddEndpointTests
{
    /// <summary>A valid form invokes the service and returns an allowlisted redirect.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_InvokesServiceAndRedirects()
    {
        var actor = new ActorIdentity(ActorType.Human, "user_01", new("user_01"));
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            "expectedVersion=8&name=Rome+Hotel&startDate=2027-01-02&endDate=2027-01-05&timeZoneId=Europe%2FRome"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var service = new RecordingService(new(AddAccommodationOutcome.Added, 9));
        await AccommodationAddEndpoints.HandleAsync(context, "creator_alpha_01", "plan_italy_01",
            new ActorResolver(actor), service, CancellationToken.None);
        Assert.Equal("Rome Hotel", service.Command?.Name);
        Assert.Equal("Europe/Rome", service.Command?.TimeZoneId);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans/plan_italy_01?accommodation=added",
            context.Response.Headers.Location);
    }

    private sealed class ActorResolver(ActorIdentity actor) : IWorkspaceActorResolver
    { public ActorIdentity? Resolve(ClaimsPrincipal principal) => actor; }
    private sealed class RecordingService(AddAccommodationResult result) : IAccommodationAddService
    {
        public AddAccommodationCommand? Command { get; private set; }
        public Task<AddAccommodationResult> AddAsync(AddAccommodationCommand command, CancellationToken cancellationToken = default)
        { Command = command; return Task.FromResult(result); }
    }
}
