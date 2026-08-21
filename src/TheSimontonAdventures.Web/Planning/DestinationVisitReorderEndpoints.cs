using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the authenticated destination-route reorder boundary.</summary>
public static class DestinationVisitReorderEndpoints
{
    /// <summary>Maps the antiforgery-protected destination reorder POST.</summary>
    public static IEndpointConventionBuilder MapDestinationVisitReorderEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/destinations/reorder",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one allowlisted destination reorder through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IDestinationVisitReorderService reorderService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CreatorId creator;
        AdventurePlanId plan;
        try
        {
            creator = new CreatorId(creatorId);
            plan = new AdventurePlanId(planId);
        }
        catch (ArgumentException)
        {
            Redirect(context, "/workspace?route=denied");
            return;
        }

        var path = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{path}?route=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{path}?route=validation");
            return;
        }

        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion)
            || !int.TryParse(form["targetSequence"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var targetSequence))
        {
            Redirect(context, $"{path}?route=validation");
            return;
        }

        DestinationVisitId destination;
        try
        {
            destination = new DestinationVisitId(form["destinationVisitId"].ToString());
        }
        catch (ArgumentException)
        {
            Redirect(context, $"{path}?route=validation");
            return;
        }

        var result = await reorderService.ReorderAsync(new(
            actor, creator, plan, destination, targetSequence, expectedVersion), cancellationToken);
        var state = result.Outcome switch
        {
            ReorderDestinationVisitOutcome.Updated => "reordered",
            ReorderDestinationVisitOutcome.Unchanged => "unchanged",
            ReorderDestinationVisitOutcome.Denied => "denied",
            ReorderDestinationVisitOutcome.Conflict => "conflict",
            ReorderDestinationVisitOutcome.ValidationFailed => "validation",
            ReorderDestinationVisitOutcome.BookingLocked => "booking-locked",
            ReorderDestinationVisitOutcome.ScheduleConflict => "schedule-conflict",
            _ => "failure"
        };
        Redirect(context, $"{path}?route={state}");
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
