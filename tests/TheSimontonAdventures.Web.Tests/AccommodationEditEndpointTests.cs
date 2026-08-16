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

/// <summary>Verifies safe parsing, antiforgery metadata, and redirects for accommodation edits.</summary>
public sealed class AccommodationEditEndpointTests
{
    private static readonly ActorIdentity Actor = new(
        ActorType.Human, "user_planner_01", new UserId("user_planner_01"));

    /// <summary>The mapped endpoint explicitly requires antiforgery validation.</summary>
    [Fact]
    public void MapAccommodationEditEndpoint_RequiresAntiforgery()
    {
        var services = new ServiceCollection().AddRouting().BuildServiceProvider();
        var routeBuilder = new TestEndpointRouteBuilder(services);

        routeBuilder.MapAccommodationEditEndpoint();

        var endpoint = Assert.Single(routeBuilder.DataSources.SelectMany(source => source.Endpoints));
        Assert.True(endpoint.Metadata.GetMetadata<RequireAntiforgeryTokenAttribute>()?.RequiresValidation);
    }

    /// <summary>A valid form passes route-scoped identity and inclusive dates to the service.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_InvokesServiceAndRedirects()
    {
        var context = Context(
            "expectedVersion=6&name=Hotel%20Central&startDate=2027-10-25" +
            "&endDate=2027-10-29&timeZoneId=Europe%2FMadrid");
        var service = new RecordingService(new(EditAccommodationOutcome.Updated, 7));

        await AccommodationEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_spain_01", "accommodation_madrid_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal(
            "/workspace/creators/creator_alpha_01/plans/plan_spain_01?accommodation-edit=updated",
            context.Response.Headers.Location);
        Assert.Equal(new AccommodationId("accommodation_madrid_01"),
            service.Command?.AccommodationId);
        Assert.Equal(new DateOnly(2027, 10, 29), service.Command?.EndDate);
    }

    /// <summary>Malformed dates fail without invoking the service or reflecting submitted values.</summary>
    [Fact]
    public async Task HandleAsync_MalformedDate_DoesNotInvokeServiceOrReflectInput()
    {
        const string privateValue = "PRIVATE-PROPERTY-VALUE";
        var context = Context(
            $"expectedVersion=6&name={privateValue}&startDate=invalid" +
            "&endDate=2027-10-29&timeZoneId=Europe%2FMadrid");
        var service = new RecordingService(new(EditAccommodationOutcome.Updated, 7));

        await AccommodationEditEndpoints.HandleAsync(
            context, "creator_alpha_01", "plan_spain_01", "accommodation_madrid_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Null(service.Command);
        Assert.Equal(
            "/workspace/creators/creator_alpha_01/plans/plan_spain_01?accommodation-edit=validation",
            context.Response.Headers.Location);
        Assert.DoesNotContain(privateValue, context.Response.Headers.Location.ToString());
    }

    /// <summary>A malformed route identity uses a generic workspace denial.</summary>
    [Fact]
    public async Task HandleAsync_MalformedIdentity_FailsClosed()
    {
        var context = Context(string.Empty);
        var service = new RecordingService(new(EditAccommodationOutcome.Updated, 7));

        await AccommodationEditEndpoints.HandleAsync(
            context, "INVALID", "plan_spain_01", "accommodation_madrid_01",
            new StubActorResolver(Actor), service, CancellationToken.None);

        Assert.Null(service.Command);
        Assert.Equal("/workspace?accommodation-edit=denied", context.Response.Headers.Location);
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

    private sealed class TestEndpointRouteBuilder(IServiceProvider serviceProvider)
        : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() =>
            new ApplicationBuilder(ServiceProvider);
    }

    private sealed class RecordingService(EditAccommodationResult result) : IAccommodationEditService
    {
        public EditAccommodationCommand? Command { get; private set; }
        public Task<EditAccommodationResult> EditAsync(
            EditAccommodationCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
