using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated Adventure Plan overview-edit boundary.</summary>
public static class AdventurePlanOverviewEditEndpoints
{
    /// <summary>Maps the antiforgery-protected overview POST.</summary>
    public static IEndpointConventionBuilder MapAdventurePlanOverviewEditEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/{planId}/overview",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one overview form submission through Post/Redirect/Get.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        string planId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IAdventurePlanOverviewEditService editService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(editService);

        CreatorId creator;
        AdventurePlanId plan;
        try
        {
            creator = new CreatorId(creatorId);
            plan = new AdventurePlanId(planId);
        }
        catch (ArgumentException)
        {
            Redirect(context, "/workspace?edit=denied");
            return;
        }

        var detailPath = $"/workspace/creators/{creator.Value}/plans/{plan.Value}";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            Redirect(context, $"{detailPath}?edit=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            Redirect(context, $"{detailPath}?edit=validation");
            return;
        }

        if (!TryReadCommand(form, actor, creator, plan, out var command))
        {
            Redirect(context, $"{detailPath}?edit=validation");
            return;
        }

        var result = await editService.EditAsync(command, cancellationToken);
        var state = result.Outcome switch
        {
            EditAdventurePlanOverviewOutcome.Updated => "updated",
            EditAdventurePlanOverviewOutcome.Unchanged => "unchanged",
            EditAdventurePlanOverviewOutcome.Denied => "denied",
            EditAdventurePlanOverviewOutcome.Conflict => "conflict",
            EditAdventurePlanOverviewOutcome.ValidationFailed => "validation",
            EditAdventurePlanOverviewOutcome.DateChangeBlocked => "date-blocked",
            _ => "failure"
        };
        Redirect(context, $"{detailPath}?edit={state}");
    }

    private static bool TryReadCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId planId,
        out EditAdventurePlanOverviewCommand command)
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

        var title = form["title"].ToString().Trim();
        var description = form["description"].ToString().Trim();
        command = new EditAdventurePlanOverviewCommand(
            actor,
            creatorId,
            planId,
            expectedVersion,
            title,
            string.IsNullOrEmpty(description) ? null : description,
            startDate,
            endDate);
        return true;
    }

    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }
}
