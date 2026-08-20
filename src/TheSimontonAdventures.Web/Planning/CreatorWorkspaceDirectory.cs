using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Identifies one Creator workspace the current user may open in Planner.</summary>
public sealed record CreatorWorkspaceChoice
{
    /// <summary>Initializes a validated, least-data workspace choice.</summary>
    public CreatorWorkspaceChoice(CreatorId creatorId, string displayName)
    {
        if (creatorId == default || string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A Creator identity and display name are required.");
        }

        CreatorId = creatorId;
        DisplayName = displayName.Trim();
    }

    /// <summary>Gets the authorized Creator identity used in the Planner route.</summary>
    public CreatorId CreatorId { get; }

    /// <summary>Gets the public Creator display name.</summary>
    public string DisplayName { get; }
}

/// <summary>Lists only Creator workspaces the current human may view in Planner.</summary>
public interface ICreatorWorkspaceDirectoryService
{
    /// <summary>Returns authorized Planner workspace choices for the current actor.</summary>
    Task<IReadOnlyList<CreatorWorkspaceChoice>> ListAsync(
        ActorIdentity actor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the workspace chooser from validated Creator records and independent
/// Creator-scoped membership authorization decisions.
/// </summary>
public sealed class CreatorWorkspaceDirectoryService(
    ICreatorService creatorService,
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IHostEnvironment hostEnvironment) : ICreatorWorkspaceDirectoryService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CreatorWorkspaceChoice>> ListAsync(
        ActorIdentity actor,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.IsHuman || !actor.UserId.HasValue)
        {
            return [];
        }

        var creators = await creatorService.GetAllAsync(cancellationToken);
        var choices = new List<CreatorWorkspaceChoice>();
        foreach (var creator in creators
            .Where(creator => creator.Status == CreatorStatus.Active)
            .Where(creator => hostEnvironment.IsDevelopment() || !creator.DevelopmentOnly)
            .OrderBy(creator => creator.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var membership = await membershipProvider.GetMembershipAsync(
                actor.UserId.Value,
                creator.Id,
                cancellationToken);
            if (membership is null)
            {
                continue;
            }

            var decision = await authorizationPolicyEvaluator.AuthorizeAsync(
                new AuthorizationRequest(
                    actor,
                    Permissions.AdventurePlanView,
                    AuthorizationResourceScope.ForCollection(
                        creator.Id,
                        AuthorizationResourceTypes.AdventurePlan),
                    membershipVersion: membership.Version),
                cancellationToken);
            if (decision.IsAllowed)
            {
                choices.Add(new CreatorWorkspaceChoice(creator.Id, creator.DisplayName));
            }
        }

        return choices;
    }
}
