using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated planned-activity edit boundary.</summary>
public static class PlannedActivityEditEndpoints
{
    /// <summary>Maps the antiforgery-protected planned-activity edit POST.</summary>
    public static IEndpointConventionBuilder MapPlannedActivityEditEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/activities/{activityId}/edit",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one planned-activity edit through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        string activityId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IPlannedActivityEditService editService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(editService);

        CreatorId creator;
        AdventurePlanId plan;
        PlannedActivityId activity;
        try
        {
            creator = new(creatorId);
            plan = new(planId);
            activity = new(activityId);
        }
        catch (ArgumentException)
        {
            Redirect(context, "/workspace?activity-edit=denied");
            return;
        }

        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{detailPath}?activity-edit=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{detailPath}?activity-edit=validation");
            return;
        }

        if (!TryReadCommand(form, actor, creator, plan, activity, out var command))
        {
            Redirect(context, $"{detailPath}?activity-edit=validation");
            return;
        }

        var result = await editService.EditAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            EditPlannedActivityOutcome.Updated => "updated",
            EditPlannedActivityOutcome.Unchanged => "unchanged",
            EditPlannedActivityOutcome.Denied => "denied",
            EditPlannedActivityOutcome.Conflict => "conflict",
            EditPlannedActivityOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?activity-edit={state}");
    }

    private static bool TryReadCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId planId,
        PlannedActivityId activityId,
        out EditPlannedActivityCommand command)
    {
        command = null!;
        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion)
            || !TryTime(form["startsAtLocal"].ToString(), out var startsAtLocal)
            || !TryTime(form["endsAtLocal"].ToString(), out var endsAtLocal))
        {
            return false;
        }

        command = new(actor, creatorId, planId, activityId, expectedVersion,
            form["title"].ToString().Trim(), startsAtLocal, endsAtLocal);
        return true;
    }

    private static bool TryTime(string value, out TimeOnly? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
