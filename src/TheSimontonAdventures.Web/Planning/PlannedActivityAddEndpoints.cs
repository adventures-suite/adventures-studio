using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated proposed-activity mutation boundary.</summary>
public static class PlannedActivityAddEndpoints
{
    /// <summary>Maps the antiforgery-protected proposed-activity POST.</summary>
    public static IEndpointConventionBuilder MapPlannedActivityAddEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/activities",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one proposed-activity form submission through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IPlannedActivityAddService addService,
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
            Redirect(context, "/workspace?activity=denied");
            return;
        }

        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{detailPath}?activity=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{detailPath}?activity=validation");
            return;
        }

        if (!TryReadCommand(form, actor, creator, plan, out var command))
        {
            Redirect(context, $"{detailPath}?activity=validation");
            return;
        }

        var result = await addService.AddAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            AddPlannedActivityOutcome.Added => "added",
            AddPlannedActivityOutcome.Denied => "denied",
            AddPlannedActivityOutcome.Conflict => "conflict",
            AddPlannedActivityOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?activity={state}");
    }

    private static bool TryReadCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId planId,
        out AddPlannedActivityCommand command)
    {
        command = null!;
        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion)
            || !TryReadOptionalTime(form["startsAtLocal"].ToString(), out var start)
            || !TryReadOptionalTime(form["endsAtLocal"].ToString(), out var end))
        {
            return false;
        }

        try
        {
            command = new(
                actor, creatorId, planId,
                new ItineraryDayId(form["itineraryDayId"].ToString()),
                expectedVersion, form["title"].ToString().Trim(), start, end);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryReadOptionalTime(string value, out TimeOnly? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        time = parsed;
        return true;
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
