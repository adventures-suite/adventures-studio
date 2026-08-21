using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated reservation-summary mutation boundary.</summary>
public static class ReservationAddEndpoints
{
    /// <summary>Maps the antiforgery-protected reservation-summary POST.</summary>
    public static IEndpointConventionBuilder MapReservationAddEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
            "/workspace/creators/{creatorId}/plans/{planId}/reservations",
            HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one credential-free reservation form through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IReservationAddService addService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CreatorId creator;
        AdventurePlanId plan;
        try
        {
            creator = new(creatorId);
            plan = new(planId);
        }
        catch (ArgumentException)
        {
            Redirect(context, "/workspace?reservation=denied");
            return;
        }

        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{detailPath}?reservation=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{detailPath}?reservation=validation");
            return;
        }

        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var version))
        {
            Redirect(context, $"{detailPath}?reservation=validation");
            return;
        }

        if (!TryReadOptionalDestinationVisitId(form["destinationVisitId"].ToString(), out var destinationVisitId))
        {
            Redirect(context, $"{detailPath}?reservation=validation");
            return;
        }

        var command = new AddReservationCommand(
            actor,
            creator,
            plan,
            version,
            form["subject"].ToString().Trim(),
            destinationVisitId);
        var result = await addService.AddAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            AddReservationOutcome.Added => "added",
            AddReservationOutcome.Denied => "denied",
            AddReservationOutcome.Conflict => "conflict",
            AddReservationOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?reservation={state}");
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }

    private static bool TryReadOptionalDestinationVisitId(
        string? value,
        out DestinationVisitId? destinationVisitId)
    {
        destinationVisitId = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        try
        {
            destinationVisitId = new(value.Trim());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
