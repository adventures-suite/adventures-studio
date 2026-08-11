using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the Planner read slice cannot reach persistence before authorization.</summary>
public sealed class PlannerWorkspaceQueryServiceTests
{
    private static readonly UserId User = new("user_planner_01");
    private static readonly CreatorId Creator = new("creator_alpha_01");

    /// <summary>Denied collection access returns no plans and never opens Planning persistence.</summary>
    [Fact]
    public async Task ListAsync_DeniedAuthorization_DoesNotBeginPlanningTransaction()
    {
        var transactions = new StubPlanningTransactionFactory();
        var service = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(Membership()),
            new StubAuthorizationEvaluator(AuthorizationDecision.Deny(
                AuthorizationDenialReason.PermissionRequired)),
            transactions);

        var result = await service.ListAsync(User, Creator);

        Assert.False(result.IsAllowed);
        Assert.Empty(result.Plans);
        Assert.Equal(0, transactions.BeginCount);
    }

    /// <summary>Allowed collection access lists plans through the explicit Creator-bound transaction.</summary>
    [Fact]
    public async Task ListAsync_AllowedAuthorization_UsesCreatorScopedRead()
    {
        var transactions = new StubPlanningTransactionFactory();
        var service = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(Membership()),
            new StubAuthorizationEvaluator(AuthorizationDecision.Allow()),
            transactions);

        var result = await service.ListAsync(User, Creator);

        Assert.True(result.IsAllowed);
        Assert.Empty(result.Plans);
        Assert.Equal(1, transactions.BeginCount);
        Assert.Equal(Creator, transactions.LastCreatorId);
    }

    private static CreatorMembershipSnapshot Membership() => new(
        new CreatorMembershipId("membership_planner_01"), User, Creator,
        CreatorMembershipStatus.Active, [CreatorRole.Viewer], [], 3,
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private sealed class StubMembershipProvider(CreatorMembershipSnapshot? membership)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId, CreatorId creatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(membership);
    }

    private sealed class StubAuthorizationEvaluator(AuthorizationDecision decision)
        : IAuthorizationPolicyEvaluator
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(decision);
    }

    private sealed class StubPlanningTransactionFactory : IPlanningTransactionFactory
    {
        public int BeginCount { get; private set; }
        public CreatorId LastCreatorId { get; private set; }

        public Task<IPlanningTransaction> BeginAsync(
            CreatorId creatorId, CancellationToken cancellationToken = default)
        {
            BeginCount++;
            LastCreatorId = creatorId;
            return Task.FromResult<IPlanningTransaction>(new StubPlanningTransaction(creatorId));
        }
    }

    private sealed class StubPlanningTransaction(CreatorId creatorId) : IPlanningTransaction
    {
        public CreatorId CreatorId { get; } = creatorId;
        public IAdventurePlanRepository AdventurePlans { get; } = new EmptyAdventurePlanRepository();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EmptyAdventurePlanRepository : IAdventurePlanRepository
    {
        public Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdventurePlanDashboardItem>>([]);
        public Task<AdventurePlan?> GetAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => Task.FromResult<AdventurePlan?>(null);
        public Task<IReadOnlyList<AdventurePlan>> ListAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdventurePlan>>([]);
        public Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdventurePlan>>([]);
        public Task AddAsync(CreatorId creatorId, AdventurePlan plan, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(CreatorId creatorId, AdventurePlan plan, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
