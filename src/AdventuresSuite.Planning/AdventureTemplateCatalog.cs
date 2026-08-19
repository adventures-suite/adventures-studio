using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Loads template catalog content without exposing its storage provider.</summary>
public interface IAdventureTemplateCatalogSource
{
    /// <summary>Lists templates visible to an already authorized customer context.</summary>
    Task<IReadOnlyList<AdventureTemplateBlueprint>> ListAsync(
        CreatorId customerCreatorId,
        string requestedLocale,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves an exact immutable template and its approved-use evidence.</summary>
    Task<AuthorizedAdventureTemplateUse?> ResolveUseAsync(
        ActorIdentity actor,
        CreatorId customerCreatorId,
        AdventureTemplateVersionId templateVersion,
        string requestedLocale,
        CancellationToken cancellationToken = default);
}

/// <summary>Returns an authorized, disclosure-safe template catalog result.</summary>
/// <param name="IsAllowed">Whether the customer catalog may be shown.</param>
/// <param name="Templates">The authorized immutable templates.</param>
public sealed record AdventureTemplateCatalogResult(
    bool IsAllowed,
    IReadOnlyList<AdventureTemplateBlueprint> Templates);

/// <summary>Queries Adventure Templates through customer Creator authorization.</summary>
public interface IAdventureTemplateCatalogQueryService
{
    /// <summary>Lists visible templates after membership and create authorization.</summary>
    Task<AdventureTemplateCatalogResult> ListAsync(
        ActorIdentity actor,
        CreatorId creatorId,
        string requestedLocale,
        CancellationToken cancellationToken = default);
}

/// <summary>Applies customer authorization before querying a template catalog source.</summary>
public sealed class AdventureTemplateCatalogQueryService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IAdventureTemplateCatalogSource source) : IAdventureTemplateCatalogQueryService
{
    /// <inheritdoc />
    public async Task<AdventureTemplateCatalogResult> ListAsync(
        ActorIdentity actor,
        CreatorId creatorId,
        string requestedLocale,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.IsHuman || !actor.UserId.HasValue
            || creatorId == default || string.IsNullOrWhiteSpace(requestedLocale))
        {
            return Denied();
        }

        var membership = await membershipProvider.GetMembershipAsync(
            actor.UserId.Value, creatorId, cancellationToken);
        if (membership is null)
        {
            return Denied();
        }

        var authorization = await authorizationPolicyEvaluator.AuthorizeAsync(
            new AuthorizationRequest(
                actor,
                Permissions.AdventurePlanCreate,
                AuthorizationResourceScope.ForCollection(
                    creatorId, AuthorizationResourceTypes.AdventurePlan),
                membershipVersion: membership.Version),
            cancellationToken);
        if (!authorization.IsAllowed)
        {
            return Denied();
        }

        var templates = await source.ListAsync(creatorId, requestedLocale, cancellationToken);
        return new(true, templates);
    }

    private static AdventureTemplateCatalogResult Denied() => new(false, []);
}

/// <summary>Delegates exact template-use resolution to the configured catalog source.</summary>
public sealed class AdventureTemplateUseResolver(IAdventureTemplateCatalogSource source)
    : IAdventureTemplateUseResolver
{
    /// <inheritdoc />
    public Task<AuthorizedAdventureTemplateUse?> ResolveAsync(
        ActorIdentity actor,
        CreatorId customerCreatorId,
        AdventureTemplateVersionId templateVersion,
        string requestedLocale,
        CancellationToken cancellationToken = default) =>
        source.ResolveUseAsync(
            actor, customerCreatorId, templateVersion, requestedLocale, cancellationToken);
}

/// <summary>Fails closed when no reviewed production template catalog is configured.</summary>
public sealed class UnavailableAdventureTemplateCatalogSource : IAdventureTemplateCatalogSource
{
    /// <inheritdoc />
    public Task<IReadOnlyList<AdventureTemplateBlueprint>> ListAsync(
        CreatorId customerCreatorId,
        string requestedLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AdventureTemplateBlueprint>>([]);

    /// <inheritdoc />
    public Task<AuthorizedAdventureTemplateUse?> ResolveUseAsync(
        ActorIdentity actor,
        CreatorId customerCreatorId,
        AdventureTemplateVersionId templateVersion,
        string requestedLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<AuthorizedAdventureTemplateUse?>(null);
}
