using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated transportation mutation boundary.</summary>
public static class TransportationSegmentAddEndpoints
{
    /// <summary>Maps the antiforgery-protected transportation POST.</summary>
    public static IEndpointConventionBuilder MapTransportationSegmentAddEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/transportation",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one transportation form submission through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] ITransportationSegmentAddService addService,
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
            Redirect(context, "/workspace?transportation=denied");
            return;
        }

        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{detailPath}?transportation=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{detailPath}?transportation=validation");
            return;
        }

        if (!TryReadCommand(form, actor, creator, plan, out var command))
        {
            Redirect(context, $"{detailPath}?transportation=validation");
            return;
        }

        var result = await addService.AddAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            AddTransportationSegmentOutcome.Added => "added",
            AddTransportationSegmentOutcome.Denied => "denied",
            AddTransportationSegmentOutcome.Conflict => "conflict",
            AddTransportationSegmentOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?transportation={state}");
    }

    private static bool TryReadCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId planId,
        out AddTransportationSegmentCommand command)
    {
        command = null!;
        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion)
            || !DateOnly.TryParseExact(form["departureDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var departureDate)
            || !DateOnly.TryParseExact(form["arrivalDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrivalDate)
            || !TryReadOptionalTime(form["departureTimeLocal"].ToString(), out var departureTime)
            || !TryReadOptionalTime(form["arrivalTimeLocal"].ToString(), out var arrivalTime)
            || !TryReadOptionalDestinationVisitId(form["departureDestinationVisitId"], out var departureVisitId)
            || !TryReadOptionalDestinationVisitId(form["arrivalDestinationVisitId"], out var arrivalVisitId))
        {
            return false;
        }

        command = new(
            actor, creatorId, planId, expectedVersion,
            form["mode"].ToString().Trim(), form["from"].ToString().Trim(),
            form["to"].ToString().Trim(), departureDate, departureTime,
            form["departureTimeZoneId"].ToString().Trim(), arrivalDate, arrivalTime,
            form["arrivalTimeZoneId"].ToString().Trim(), departureVisitId, arrivalVisitId);
        return true;
    }

    private static bool TryReadOptionalDestinationVisitId(string? value, out DestinationVisitId? visitId)
    {
        visitId = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        try { visitId = new(value.Trim()); return true; }
        catch (ArgumentException) { return false; }
    }

    private static bool TryReadOptionalTime(string value, out TimeOnly? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed)) return false;
        time = parsed;
        return true;
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
