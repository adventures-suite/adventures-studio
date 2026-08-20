using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated Destination FootStep application boundary.</summary>
public static class DestinationFootStepApplyEndpoints
{
    /// <summary>Maps the antiforgery-protected reviewed FootStep POST.</summary>
    public static IEndpointConventionBuilder MapDestinationFootStepApplyEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/footsteps/destination",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one reviewed Destination FootStep through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IDestinationFootStepApplyService service,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CreatorId creator;
        AdventurePlanId plan;
        try { creator = new(creatorId); plan = new(planId); }
        catch (ArgumentException) { Redirect(context, "/workspace?footstep=denied"); return; }
        var path = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null) { Redirect(context, $"{path}?footstep=denied"); return; }
        IFormCollection form;
        try { form = await context.Request.ReadFormAsync(cancellationToken); }
        catch (InvalidDataException) { Redirect(context, $"{path}?footstep=validation"); return; }
        if (!TryCommand(form, actor, creator, plan, out var command))
        {
            Redirect(context, $"{path}?footstep=validation");
            return;
        }
        var result = await service.ApplyAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            ApplyDestinationFootStepOutcome.Added => "added",
            ApplyDestinationFootStepOutcome.Replayed => "added",
            ApplyDestinationFootStepOutcome.Denied => "denied",
            ApplyDestinationFootStepOutcome.Conflict => "conflict",
            ApplyDestinationFootStepOutcome.ValidationFailed => "validation",
            _ => "failure"
        };
        Redirect(context, $"{path}?footstep={state}");
    }

    private static bool TryCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creator,
        AdventurePlanId plan,
        out ApplyDestinationFootStepCommand command)
    {
        command = null!;
        try
        {
            if (!long.TryParse(form["expectedVersion"], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var expectedVersion)
                || !DateOnly.TryParseExact(form["startDate"], "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate)
                || !DateOnly.TryParseExact(form["endDate"], "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate)) return false;
            command = new(actor, creator, plan, expectedVersion,
                new PlanningIdempotencyKey(form["idempotencyKey"].ToString()),
                form["footStepId"].ToString(), form["footStepVersion"].ToString(),
                startDate, endDate, form["timeZoneId"].ToString());
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
