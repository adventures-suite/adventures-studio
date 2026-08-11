using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Reads memberships through a short Creator-scoped transaction.</summary>
public sealed class TransactionalCreatorMembershipProvider(
    ICreatorMembershipTransactionFactory transactionFactory) : ICreatorMembershipProvider
{
    /// <inheritdoc />
    public async Task<CreatorMembershipSnapshot?> GetMembershipAsync(
        UserId userId,
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await transactionFactory.BeginAsync(creatorId, cancellationToken);
        return await transaction.Memberships.GetMembershipAsync(userId, creatorId, cancellationToken);
    }
}

/// <summary>Loads minimum authoritative Planning facts for instance authorization.</summary>
public sealed class PlanningAuthorizationResourceFactsProvider(
    IPlanningTransactionFactory transactionFactory) : IAuthorizationResourceFactsProvider
{
    /// <inheritdoc />
    public async Task<AuthorizationResourceFacts?> GetResourceFactsAsync(
        AuthorizationResourceScope resource,
        CancellationToken cancellationToken = default)
    {
        if (resource.ResourceType != AuthorizationResourceTypes.AdventurePlan
            || resource.ScopeType != AuthorizationResourceScopeType.ResourceInstance
            || string.IsNullOrEmpty(resource.ResourceId))
        {
            return null;
        }

        AdventurePlanId planId;
        try
        {
            planId = new AdventurePlanId(resource.ResourceId);
        }
        catch (ArgumentException)
        {
            return null;
        }

        await using var transaction = await transactionFactory.BeginAsync(
            resource.CreatorId, cancellationToken);
        var plan = await transaction.AdventurePlans.GetAsync(
            resource.CreatorId, planId, cancellationToken);
        return plan is null
            ? null
            : new AuthorizationResourceFacts(
                plan.CreatorId,
                AuthorizationResourceTypes.AdventurePlan,
                plan.Id.Value,
                plan.Status == PlanningStatus.Archived,
                plan.Audit.Version);
    }
}
