using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated destination-visit mutation boundary.</summary>
public static class DestinationVisitAddEndpoints
{
    /// <summary>Maps the antiforgery-protected destination-visit POST.</summary>
    public static IEndpointConventionBuilder MapDestinationVisitAddEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/destinations",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one destination-visit form submission through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IDestinationVisitAddService addService,
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
            Redirect(context, "/workspace?destination=denied");
            return;
        }

        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{detailPath}?destination=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{detailPath}?destination=validation");
            return;
        }

        if (!TryReadCommand(form, actor, creator, plan, out var command))
        {
            Redirect(context, $"{detailPath}?destination=validation");
            return;
        }

        var result = await addService.AddAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            AddDestinationVisitOutcome.Added => "added",
            AddDestinationVisitOutcome.Denied => "denied",
            AddDestinationVisitOutcome.Conflict => "conflict",
            AddDestinationVisitOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?destination={state}");
    }

    private static bool TryReadCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId planId,
        out AddDestinationVisitCommand command)
    {
        command = null!;
        if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion)
            || !DateOnly.TryParseExact(form["startDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate)
            || !DateOnly.TryParseExact(form["endDate"], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
        {
            return false;
        }

        command = new(
            actor,
            creatorId,
            planId,
            expectedVersion,
            form["name"].ToString().Trim(),
            startDate,
            endDate,
            form["timeZoneId"].ToString().Trim());
        return true;
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
