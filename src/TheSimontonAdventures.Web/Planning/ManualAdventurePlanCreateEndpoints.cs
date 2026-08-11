using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated manual Adventure Plan creation boundary.</summary>
public static class ManualAdventurePlanCreateEndpoints
{
    /// <summary>Maps the antiforgery-protected POST endpoint.</summary>
    public static IEndpointConventionBuilder MapManualAdventurePlanCreateEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/create",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Handles one form post and redirects to a safe GET result.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IManualAdventurePlanCreateService createService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(createService);

        CreatorId creator;
        try
        {
            creator = new CreatorId(creatorId);
        }
        catch (ArgumentException)
        {
            await RedirectAsync(context, "/workspace?create=denied");
            return;
        }

        var listPath = $"/workspace/creators/{creator.Value}/plans";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            await RedirectAsync(context, $"{listPath}?create=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            await RedirectAsync(context, $"{listPath}?create=validation");
            return;
        }

        if (!TryReadCommand(form, actor, creator, out var command))
        {
            await RedirectAsync(context, $"{listPath}?create=validation");
            return;
        }

        var result = await createService.CreateAsync(command, cancellationToken);
        var location = result.Outcome switch
        {
            ManualAdventurePlanCreateOutcome.Created
                or ManualAdventurePlanCreateOutcome.Replayed
                when result.AdventurePlanId.HasValue =>
                $"{listPath}/{result.AdventurePlanId.Value.Value}",
            ManualAdventurePlanCreateOutcome.Denied => $"{listPath}?create=denied",
            ManualAdventurePlanCreateOutcome.Conflict => $"{listPath}?create=conflict",
            ManualAdventurePlanCreateOutcome.ValidationFailed => $"{listPath}?create=validation",
            _ => $"{listPath}?create=failure"
        };
        await RedirectAsync(context, location);
    }

    private static bool TryReadCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creatorId,
        out ManualAdventurePlanCreateCommand command)
    {
        command = null!;
        var title = form["title"].ToString().Trim();
        var descriptionValue = form["description"].ToString().Trim();
        if (!DateOnly.TryParseExact(
                form["startDate"],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var startDate)
            || !DateOnly.TryParseExact(
                form["endDate"],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var endDate))
        {
            return false;
        }

        try
        {
            command = new ManualAdventurePlanCreateCommand(
                actor,
                creatorId,
                new PlanningIdempotencyKey(form["idempotencyKey"].ToString()),
                title,
                string.IsNullOrEmpty(descriptionValue) ? null : descriptionValue,
                startDate,
                endDate);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static Task RedirectAsync(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
        return Task.CompletedTask;
    }
}
