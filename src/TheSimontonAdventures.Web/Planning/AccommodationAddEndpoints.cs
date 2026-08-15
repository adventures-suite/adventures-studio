using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated accommodation mutation boundary.</summary>
public static class AccommodationAddEndpoints
{
    /// <summary>Maps the antiforgery-protected accommodation POST.</summary>
    public static IEndpointConventionBuilder MapAccommodationAddEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/workspace/creators/{creatorId}/plans/{planId}/accommodations", HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one accommodation form submission through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(HttpContext context, string creatorId, string planId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IAccommodationAddService addService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CreatorId creator;
        AdventurePlanId plan;
        try { creator = new(creatorId); plan = new(planId); }
        catch (ArgumentException) { Redirect(context, "/workspace?accommodation=denied"); return; }
        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null) { Redirect(context, $"{detailPath}?accommodation=denied"); return; }
        IFormCollection form;
        try { form = await context.Request.ReadFormAsync(cancellationToken); }
        catch (InvalidDataException) { Redirect(context, $"{detailPath}?accommodation=validation"); return; }
        if (!TryReadCommand(form, actor, creator, plan, out var command))
        { Redirect(context, $"{detailPath}?accommodation=validation"); return; }
        var result = await addService.AddAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            AddAccommodationOutcome.Added => "added",
            AddAccommodationOutcome.Denied => "denied",
            AddAccommodationOutcome.Conflict => "conflict",
            AddAccommodationOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?accommodation={state}");
    }

    private static bool TryReadCommand(IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor, CreatorId creatorId,
        AdventurePlanId planId, out AddAccommodationCommand command)
    {
        command = null!;
        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var version)
            || !DateOnly.TryParseExact(form["startDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !DateOnly.TryParseExact(form["endDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)) return false;
        command = new(actor, creatorId, planId, version, form["name"].ToString().Trim(),
            start, end, form["timeZoneId"].ToString().Trim());
        return true;
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
