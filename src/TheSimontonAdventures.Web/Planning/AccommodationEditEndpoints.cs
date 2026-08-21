using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated accommodation edit boundary.</summary>
public static class AccommodationEditEndpoints
{
    /// <summary>Maps the antiforgery-protected accommodation edit POST.</summary>
    public static IEndpointConventionBuilder MapAccommodationEditEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/accommodations/{accommodationId}/edit",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one accommodation edit through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        string accommodationId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IAccommodationEditService editService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(editService);

        CreatorId creator;
        AdventurePlanId plan;
        AccommodationId accommodation;
        try
        {
            creator = new(creatorId);
            plan = new(planId);
            accommodation = new(accommodationId);
        }
        catch (ArgumentException)
        {
            Redirect(context, "/workspace?accommodation-edit=denied");
            return;
        }

        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{detailPath}?accommodation-edit=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{detailPath}?accommodation-edit=validation");
            return;
        }

        if (!TryReadCommand(form, actor, creator, plan, accommodation, out var command))
        {
            Redirect(context, $"{detailPath}?accommodation-edit=validation");
            return;
        }

        var result = await editService.EditAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            EditAccommodationOutcome.Updated => "updated",
            EditAccommodationOutcome.Unchanged => "unchanged",
            EditAccommodationOutcome.Denied => "denied",
            EditAccommodationOutcome.Conflict => "conflict",
            EditAccommodationOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?accommodation-edit={state}");
    }

    private static bool TryReadCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId planId,
        AccommodationId accommodationId,
        out EditAccommodationCommand command)
    {
        command = null!;
        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion)
            || !DateOnly.TryParseExact(form["startDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate)
            || !DateOnly.TryParseExact(form["endDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate)
            || !TryReadOptionalDestinationVisitId(form["destinationVisitId"], out var destinationVisitId))
        {
            return false;
        }

        command = new(
            actor, creatorId, planId, accommodationId, expectedVersion,
            form["name"].ToString().Trim(), startDate, endDate,
            form["timeZoneId"].ToString().Trim(), destinationVisitId);
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
