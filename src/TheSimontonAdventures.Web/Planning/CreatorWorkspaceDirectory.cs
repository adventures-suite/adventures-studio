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
    IHostEnvironment hostEnvironment,
    IConfiguration? configuration = null) : ICreatorWorkspaceDirectoryService
{
    private static readonly CreatorId LocalAlphaCreatorId = new("creator_local_alpha");

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
        var candidates = creators
            .Where(creator => creator.Status == CreatorStatus.Active)
            .Where(creator => hostEnvironment.IsDevelopment() || !creator.DevelopmentOnly)
            .Select(creator => new CreatorWorkspaceChoice(creator.Id, creator.DisplayName))
            .ToList();
        if (IsLocalAlphaEnabled()
            && candidates.All(candidate => candidate.CreatorId != LocalAlphaCreatorId))
        {
            candidates.Add(new CreatorWorkspaceChoice(
                LocalAlphaCreatorId,
                "Local Alpha Adventures"));
        }

        var choices = new List<CreatorWorkspaceChoice>();
        foreach (var candidate in candidates
            .OrderBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var membership = await membershipProvider.GetMembershipAsync(
                actor.UserId.Value,
                candidate.CreatorId,
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
                        candidate.CreatorId,
                        AuthorizationResourceTypes.AdventurePlan),
                    membershipVersion: membership.Version),
                cancellationToken);
            if (decision.IsAllowed)
            {
                choices.Add(candidate);
            }
        }

        return choices;
    }

    private bool IsLocalAlphaEnabled() =>
        hostEnvironment.IsDevelopment()
        && string.Equals(
            configuration?["Authentication:Mode"],
            "Development",
            StringComparison.OrdinalIgnoreCase)
        && string.Equals(
            configuration?["ADVENTURESSUITE_LOCAL_ALPHA_ENABLED"],
            "true",
            StringComparison.Ordinal);
}
