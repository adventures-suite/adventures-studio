using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Maps the cookie-authenticated Adventure Template instantiation boundary.</summary>
public static class AdventureTemplateInstantiateEndpoints
{
    /// <summary>Maps the antiforgery-protected template-instantiation endpoint.</summary>
    public static IEndpointConventionBuilder MapAdventureTemplateInstantiateEndpoint(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                "/workspace/creators/{creatorId}/plans/create-from-template",
                HandleAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));

    /// <summary>Creates an independent private plan and redirects to a safe GET result.</summary>
    public static async Task HandleAsync(
        HttpContext context,
        string creatorId,
        [FromServices] IWorkspaceActorResolver actorResolver,
        [FromServices] IAdventureTemplateInstantiateService instantiateService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(instantiateService);

        CreatorId creator;
        try
        {
            creator = new CreatorId(creatorId);
        }
        catch (ArgumentException)
        {
            await RedirectAsync(context, "/workspace?template=denied");
            return;
        }

        var listPath = $"/workspace/creators/{creator.Value}/plans";
        var actor = actorResolver.Resolve(context.User);
        if (actor is null)
        {
            await RedirectAsync(context, $"{listPath}?template=denied");
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            await RedirectAsync(context, $"{listPath}?template=validation");
            return;
        }

        if (!TryReadCommand(form, actor, creator, out var command))
        {
            await RedirectAsync(context, $"{listPath}?template=validation");
            return;
        }

        var result = await instantiateService.InstantiateAsync(command, cancellationToken);
        var location = result.Outcome switch
        {
            AdventureTemplateInstantiateOutcome.Created
                or AdventureTemplateInstantiateOutcome.Replayed
                when result.AdventurePlanId.HasValue =>
                $"{listPath}/{result.AdventurePlanId.Value.Value}",
            AdventureTemplateInstantiateOutcome.Denied => $"{listPath}?template=denied",
            AdventureTemplateInstantiateOutcome.Conflict => $"{listPath}?template=conflict",
            AdventureTemplateInstantiateOutcome.ValidationFailed => $"{listPath}?template=validation",
            _ => $"{listPath}?template=failure"
        };
        await RedirectAsync(context, location);
    }

    private static bool TryReadCommand(
        IFormCollection form,
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId creatorId,
        out AdventureTemplateInstantiateCommand command)
    {
        command = null!;
        if (!DateOnly.TryParseExact(
                form["startDate"], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var startDate))
        {
            return false;
        }

        try
        {
            command = new(
                actor,
                creatorId,
                new PlanningIdempotencyKey(form["idempotencyKey"].ToString()),
                new AdventureTemplateVersionId(
                    form["templateId"].ToString(), form["templateVersion"].ToString()),
                startDate,
                form["locale"].ToString());
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
