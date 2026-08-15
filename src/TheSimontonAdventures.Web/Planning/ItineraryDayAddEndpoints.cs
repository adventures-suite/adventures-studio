using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated itinerary-day mutation boundary.</summary>
public static class ItineraryDayAddEndpoints
{
    /// <summary>Maps the antiforgery-protected itinerary-day POST.</summary>
    public static IEndpointConventionBuilder MapItineraryDayAddEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/days",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one itinerary-day form submission through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IItineraryDayAddService addService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(addService);

        CreatorId creator;
        AdventurePlanId plan;
        try
        {
            creator = new(creatorId);
            plan = new(planId);
        }
        catch (ArgumentException)
        {
            Redirect(context, "/workspace?day=denied");
            return;
        }

        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{detailPath}?day=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{detailPath}?day=validation");
            return;
        }

        if (!TryReadCommand(form, actor, creator, plan, out var command))
        {
            Redirect(context, $"{detailPath}?day=validation");
            return;
        }

        var result = await addService.AddAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            AddItineraryDayOutcome.Added => "added",
            AddItineraryDayOutcome.Denied => "denied",
            AddItineraryDayOutcome.Conflict => "conflict",
            AddItineraryDayOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?day={state}");
    }

    private static bool TryReadCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId planId,
        out AddItineraryDayCommand command)
    {
        command = null!;
        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion)
            || !DateOnly.TryParseExact(form["date"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return false;
        }

        try
        {
            command = new(
                actor, creatorId, planId,
                new DestinationVisitId(form["destinationVisitId"].ToString()),
                expectedVersion, date, form["title"].ToString().Trim());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
