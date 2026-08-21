using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated transportation edit boundary.</summary>
public static class TransportationSegmentEditEndpoints
{
    /// <summary>Maps the antiforgery-protected transportation edit POST.</summary>
    public static IEndpointConventionBuilder MapTransportationSegmentEditEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/transportation/{segmentId}/edit",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one transportation edit through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        string segmentId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] ITransportationSegmentEditService editService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(editService);

        CreatorId creator;
        AdventurePlanId plan;
        TransportationSegmentId segment;
        try
        {
            creator = new(creatorId);
            plan = new(planId);
            segment = new(segmentId);
        }
        catch (ArgumentException)
        {
            Redirect(context, "/workspace?transportation-edit=denied");
            return;
        }

        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{detailPath}?transportation-edit=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{detailPath}?transportation-edit=validation");
            return;
        }

        if (!TryReadCommand(form, actor, creator, plan, segment, out var command))
        {
            Redirect(context, $"{detailPath}?transportation-edit=validation");
            return;
        }

        var result = await editService.EditAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            EditTransportationSegmentOutcome.Updated => "updated",
            EditTransportationSegmentOutcome.Unchanged => "unchanged",
            EditTransportationSegmentOutcome.Denied => "denied",
            EditTransportationSegmentOutcome.Conflict => "conflict",
            EditTransportationSegmentOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?transportation-edit={state}");
    }

    private static bool TryReadCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId planId,
        TransportationSegmentId segmentId,
        out EditTransportationSegmentCommand command)
    {
        command = null!;
        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion)
            || !DateOnly.TryParseExact(form["departureDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var departureDate)
            || !DateOnly.TryParseExact(form["arrivalDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrivalDate)
            || !TryReadOptionalTime(
                form["departureTimeLocal"].ToString(), out var departureTime)
            || !TryReadOptionalTime(
                form["arrivalTimeLocal"].ToString(), out var arrivalTime)
            || !TryReadOptionalDestinationVisitId(form["departureDestinationVisitId"], out var departureVisitId)
            || !TryReadOptionalDestinationVisitId(form["arrivalDestinationVisitId"], out var arrivalVisitId))
        {
            return false;
        }

        command = new(
            actor, creatorId, planId, segmentId, expectedVersion,
            form["mode"].ToString().Trim(), form["from"].ToString().Trim(),
            form["to"].ToString().Trim(), departureDate, departureTime,
            form["departureTimeZoneId"].ToString().Trim(), arrivalDate, arrivalTime,
            form["arrivalTimeZoneId"].ToString().Trim(), departureVisitId, arrivalVisitId);
        return true;
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

    private static bool TryReadOptionalDestinationVisitId(string? value, out DestinationVisitId? visitId)
    {
        visitId = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        try { visitId = new(value.Trim()); return true; }
        catch (ArgumentException) { return false; }
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
