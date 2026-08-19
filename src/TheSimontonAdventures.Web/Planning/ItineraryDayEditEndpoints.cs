using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated itinerary-day title edit boundary.</summary>
public static class ItineraryDayEditEndpoints
{
    /// <summary>Maps the antiforgery-protected itinerary-day title edit POST.</summary>
    public static IEndpointConventionBuilder MapItineraryDayEditEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/days/{dayId}/edit",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one itinerary-day title edit through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        string dayId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IItineraryDayEditService editService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(editService);

        CreatorId creator;
        AdventurePlanId plan;
        ItineraryDayId day;
        try
        {
            creator = new(creatorId);
            plan = new(planId);
            day = new(dayId);
        }
        catch (ArgumentException)
        {
            Redirect(context, "/workspace?day-edit=denied");
            return;
        }

        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{detailPath}?day-edit=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{detailPath}?day-edit=validation");
            return;
        }

        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion))
        {
            Redirect(context, $"{detailPath}?day-edit=validation");
            return;
        }

        var command = new EditItineraryDayCommand(
            actor, creator, plan, day, expectedVersion, form["title"].ToString().Trim());
        var result = await editService.EditAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            EditItineraryDayOutcome.Updated => "updated",
            EditItineraryDayOutcome.Unchanged => "unchanged",
            EditItineraryDayOutcome.Denied => "denied",
            EditItineraryDayOutcome.Conflict => "conflict",
            EditItineraryDayOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?day-edit={state}");
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
