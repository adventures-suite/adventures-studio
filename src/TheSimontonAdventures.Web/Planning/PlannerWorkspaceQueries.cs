using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Describes a safe outcome from the first read-only Planner workspace query.</summary>
public sealed record PlannerWorkspaceResult
{
    private PlannerWorkspaceResult(bool isAllowed, IReadOnlyList<AdventurePlanDashboardItem> plans)
    {
        IsAllowed = isAllowed;
        Plans = plans;
    }

    /// <summary>Gets whether the authenticated user may view this Creator's plans.</summary>
    public bool IsAllowed { get; }

    /// <summary>Gets authorized, non-archived plans when access is allowed.</summary>
    public IReadOnlyList<AdventurePlanDashboardItem> Plans { get; }

    /// <summary>Creates a non-disclosing denied result.</summary>
    public static PlannerWorkspaceResult Denied() => new(false, []);

    /// <summary>Creates an allowed result from an authorized private query.</summary>
    public static PlannerWorkspaceResult Allowed(IReadOnlyList<AdventurePlanDashboardItem> plans) =>
        new(true, plans ?? throw new ArgumentNullException(nameof(plans)));
}

/// <summary>Reads the private Planner dashboard through authorization and persistence boundaries.</summary>
public interface IPlannerWorkspaceQueryService
{
    /// <summary>Lists active plans only after Creator-scoped membership authorization succeeds.</summary>
    Task<PlannerWorkspaceResult> ListAsync(
        UserId userId,
        CreatorId creatorId,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements authorization-first, read-only Planner workspace access.</summary>
public sealed class PlannerWorkspaceQueryService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory) : IPlannerWorkspaceQueryService
{
    /// <inheritdoc />
    public async Task<PlannerWorkspaceResult> ListAsync(
        UserId userId,
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        if (userId == default || creatorId == default)
        {
            return PlannerWorkspaceResult.Denied();
        }

        var membership = await membershipProvider.GetMembershipAsync(
            userId, creatorId, cancellationToken);
        if (membership is null)
        {
            return PlannerWorkspaceResult.Denied();
        }

        var actor = new ActorIdentity(ActorType.Human, userId.Value, userId);
        var decision = await authorizationPolicyEvaluator.AuthorizeAsync(
            new AuthorizationRequest(
                actor,
                Permissions.AdventurePlanView,
                AuthorizationResourceScope.ForCollection(
                    creatorId,
                    AuthorizationResourceTypes.AdventurePlan),
                membershipVersion: membership.Version),
            cancellationToken);
        if (!decision.IsAllowed)
        {
            return PlannerWorkspaceResult.Denied();
        }

        await using var transaction = await transactionFactory.BeginAsync(
            creatorId, cancellationToken);
        var plans = await transaction.AdventurePlans.ListDashboardAsync(
            creatorId, cancellationToken);
        return PlannerWorkspaceResult.Allowed(plans);
    }
}
