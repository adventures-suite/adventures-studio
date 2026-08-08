using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies composable, default-deny Creator authorization policies.</summary>
public sealed class AuthorizationPolicyEvaluatorTests
{
    private static readonly CreatorId CustomerCreator = new("creator_customer");
    private static readonly CreatorId AgencyCreator = new("creator_agency");
    private static readonly UserId CustomerUser = new("user_customer");
    private static readonly ActorIdentity CustomerActor = new(
        ActorType.Human,
        "actor_customer",
        CustomerUser);
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 20, 0, 0, TimeSpan.Zero);

    /// <summary>Ensures role bundles remain ordered, immutable permission conveniences.</summary>
    [Fact]
    public void RoleBundles_ExposeExpectedPermissionStrength()
    {
        Assert.Contains(Permissions.AdventurePlanView,
            CreatorRolePermissionBundles.GetPermissions(CreatorRole.Viewer));
        Assert.DoesNotContain(Permissions.AdventurePlanEdit,
            CreatorRolePermissionBundles.GetPermissions(CreatorRole.Viewer));
        Assert.Contains(Permissions.AdventurePlanEdit,
            CreatorRolePermissionBundles.GetPermissions(CreatorRole.Contributor));
        Assert.DoesNotContain(Permissions.AdventurePlanRestore,
            CreatorRolePermissionBundles.GetPermissions(CreatorRole.Contributor));
        Assert.Contains(Permissions.AdventurePlanRestore,
            CreatorRolePermissionBundles.GetPermissions(CreatorRole.Planner));
        Assert.Contains(Permissions.CreatorManageMembers,
            CreatorRolePermissionBundles.GetPermissions(CreatorRole.Administrator));
        Assert.DoesNotContain(Permissions.SupportImpersonate,
            CreatorRolePermissionBundles.GetPermissions(CreatorRole.Owner));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreatorRolePermissionBundles.GetPermissions((CreatorRole)999));
    }

    /// <summary>Ensures membership inputs are deeply copied and effective periods are enforced.</summary>
    [Fact]
    public void MembershipSnapshot_IsImmutableAndTimeBounded()
    {
        var roles = new List<CreatorRole> { CreatorRole.Viewer };
        var grants = new List<Permission> { Permissions.AdventurePlanEdit };
        var membership = Membership(roles, grants, expiresAtUtc: Now.AddHours(1));

        roles.Add(CreatorRole.Owner);
        grants.Add(Permissions.CreatorManageMembers);

        Assert.Single(membership.Roles);
        Assert.DoesNotContain(Permissions.CreatorManageMembers, membership.Permissions);
        Assert.True(membership.IsActiveAt(Now));
        Assert.False(membership.IsActiveAt(Now.AddHours(1)));
        Assert.Throws<ArgumentException>(() => membership.IsActiveAt(Now.ToOffset(TimeSpan.FromHours(-7))));
        Assert.Throws<ArgumentException>(() => Membership(
            [CreatorRole.Viewer], [], expiresAtUtc: Now.AddDays(-2)));
    }

    /// <summary>Exercises collection creation and archived-list policies.</summary>
    [Fact]
    public async Task CollectionPolicies_RequireExplicitPermissionAndScope()
    {
        var membership = Membership([CreatorRole.Planner], []);
        var evaluator = Evaluator(membership);
        var collection = AuthorizationResourceScope.ForCollection(
            CustomerCreator,
            AuthorizationResourceTypes.AdventurePlan);

        Assert.True((await evaluator.AuthorizeAsync(Request(
            Permissions.AdventurePlanCreate,
            collection))).IsAllowed);
        Assert.True((await evaluator.AuthorizeAsync(Request(
            Permissions.AdventurePlanViewArchived,
            collection))).IsAllowed);
        Assert.True((await evaluator.AuthorizeAsync(Request(
            Permissions.AdventurePlanView,
            collection))).IsAllowed);

        var viewerEvaluator = Evaluator(Membership([CreatorRole.Viewer], []));
        Assert.Equal(
            AuthorizationDenialReason.PermissionRequired,
            (await viewerEvaluator.AuthorizeAsync(Request(
                Permissions.AdventurePlanViewArchived,
                collection))).DenialReason);

        Assert.Equal(
            AuthorizationDenialReason.InvalidRequest,
            (await evaluator.AuthorizeAsync(Request(
                Permissions.AdventurePlanCreate,
                AuthorizationResourceScope.ForInstance(
                    CustomerCreator,
                    AuthorizationResourceTypes.AdventurePlan,
                    "plan_customer")))).DenialReason);
    }

    /// <summary>Ensures ownership is loaded authoritatively and cross-Creator access fails safely.</summary>
    [Fact]
    public async Task InstancePolicy_RequiresAuthoritativeCreatorOwnership()
    {
        var requestScope = PlanScope(CustomerCreator);
        var evaluator = Evaluator(
            Membership([CreatorRole.Viewer], []),
            new AuthorizationResourceFacts(
                AgencyCreator,
                AuthorizationResourceTypes.AdventurePlan,
                "plan_customer",
                false,
                1));

        var decision = await evaluator.AuthorizeAsync(Request(
            Permissions.AdventurePlanView,
            requestScope));

        Assert.False(decision.IsAllowed);
        Assert.Equal(AuthorizationDenialReason.ResourceScopeMismatch, decision.DenialReason);
    }

    /// <summary>Ensures archived plans can be viewed but have explicit archive transitions.</summary>
    [Fact]
    public async Task ArchivePolicies_EnforceLifecycleAndAuditIntent()
    {
        var membership = Membership([CreatorRole.Planner], []);
        var archivedFacts = PlanFacts(isArchived: true);
        var archivedEvaluator = Evaluator(membership, archivedFacts);

        Assert.True((await archivedEvaluator.AuthorizeAsync(Request(
            Permissions.AdventurePlanView,
            PlanScope(CustomerCreator)))).IsAllowed);
        Assert.Equal(
            AuthorizationAuditRequirement.RequiredMutation,
            (await archivedEvaluator.AuthorizeAsync(Request(
                Permissions.AdventurePlanRestore,
                PlanScope(CustomerCreator)))).AuditRequirement);
        Assert.Equal(
            AuthorizationDenialReason.InvalidRequest,
            (await archivedEvaluator.AuthorizeAsync(Request(
                Permissions.AdventurePlanArchive,
                PlanScope(CustomerCreator)))).DenialReason);

        var activeEvaluator = Evaluator(membership, PlanFacts(isArchived: false));
        Assert.Equal(
            AuthorizationAuditRequirement.RequiredMutation,
            (await activeEvaluator.AuthorizeAsync(Request(
                Permissions.AdventurePlanArchive,
                PlanScope(CustomerCreator)))).AuditRequirement);
        Assert.Equal(
            AuthorizationDenialReason.InvalidRequest,
            (await activeEvaluator.AuthorizeAsync(Request(
                Permissions.AdventurePlanRestore,
                PlanScope(CustomerCreator)))).DenialReason);
    }

    /// <summary>Ensures sensitive reads declare fail-closed durable audit behavior.</summary>
    [Fact]
    public async Task SensitiveReservationPolicy_RequiresReadAuditIntent()
    {
        var evaluator = Evaluator(Membership([CreatorRole.Planner], []), PlanFacts(false));

        var decision = await evaluator.AuthorizeAsync(Request(
            Permissions.AdventurePlanViewSensitiveReservations,
            PlanScope(CustomerCreator)));

        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationAuditRequirement.RequiredSensitiveRead, decision.AuditRequirement);
    }

    /// <summary>Ensures every currently supported authoritative mutation requires audit intent.</summary>
    [Theory]
    [MemberData(nameof(MutatingPolicyCases))]
    public async Task MutatingPolicies_RequireDurableAuditIntent(
        Permission permission,
        AuthorizationResourceScope scope,
        AuthorizationResourceFacts? facts)
    {
        var evaluator = Evaluator(Membership([CreatorRole.Owner], []), facts);

        var decision = await evaluator.AuthorizeAsync(Request(permission, scope));

        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationAuditRequirement.RequiredMutation, decision.AuditRequirement);
    }

    /// <summary>Supplies all currently supported authoritative mutation policies.</summary>
    public static TheoryData<Permission, AuthorizationResourceScope, AuthorizationResourceFacts?>
        MutatingPolicyCases => new()
        {
            {
                Permissions.CreatorManageMembers,
                AuthorizationResourceScope.ForCollection(
                    CustomerCreator,
                    AuthorizationResourceTypes.Creator),
                null
            },
            {
                Permissions.AdventurePlanCreate,
                AuthorizationResourceScope.ForCollection(
                    CustomerCreator,
                    AuthorizationResourceTypes.AdventurePlan),
                null
            },
            { Permissions.AdventurePlanEdit, PlanScope(CustomerCreator), PlanFacts(false) },
            { Permissions.AdventurePlanArchive, PlanScope(CustomerCreator), PlanFacts(false) },
            { Permissions.AdventurePlanRestore, PlanScope(CustomerCreator), PlanFacts(true) },
            {
                Permissions.PlanningProposalSubmit,
                ProposalScope(),
                ProposalFacts()
            },
            {
                Permissions.PlanningProposalReview,
                ProposalScope(),
                ProposalFacts()
            },
            {
                Permissions.PlanningProposalApplyApproved,
                ProposalScope(),
                ProposalFacts()
            }
        };

    /// <summary>Ensures inactive and stale memberships cannot retain access.</summary>
    [Theory]
    [InlineData(CreatorMembershipStatus.Pending)]
    [InlineData(CreatorMembershipStatus.Disabled)]
    [InlineData(CreatorMembershipStatus.Revoked)]
    public async Task MembershipPolicy_DeniesInactiveAndStaleState(CreatorMembershipStatus status)
    {
        var inactive = Membership([CreatorRole.Viewer], [], status: status);
        var evaluator = Evaluator(inactive, PlanFacts(false));

        Assert.Equal(
            AuthorizationDenialReason.AccessRevoked,
            (await evaluator.AuthorizeAsync(Request(
                Permissions.AdventurePlanView,
                PlanScope(CustomerCreator)))).DenialReason);

        var staleEvaluator = Evaluator(Membership([CreatorRole.Viewer], [], version: 2), PlanFacts(false));
        Assert.Equal(
            AuthorizationDenialReason.StaleAuthorizationContext,
            (await staleEvaluator.AuthorizeAsync(Request(
                Permissions.AdventurePlanView,
                PlanScope(CustomerCreator),
                membershipVersion: 1))).DenialReason);
    }

    /// <summary>Ensures agency membership alone grants nothing in a customer Creator.</summary>
    [Fact]
    public async Task AgencyMembership_CannotAuthorizeCustomerPlan()
    {
        var agencyMembership = new CreatorMembershipSnapshot(
            new("membership_agency"),
            CustomerUser,
            AgencyCreator,
            CreatorMembershipStatus.Active,
            [CreatorRole.Owner],
            [],
            1,
            Now.AddDays(-1));
        var evaluator = new AuthorizationPolicyEvaluator(
            new MembershipProvider(agencyMembership),
            new ResourceFactsProvider(PlanFacts(false)),
            new FixedTimeProvider(Now));

        var decision = await evaluator.AuthorizeAsync(Request(
            Permissions.AdventurePlanView,
            PlanScope(CustomerCreator)));

        Assert.Equal(AuthorizationDenialReason.MembershipRequired, decision.DenialReason);
    }

    /// <summary>Ensures anonymous, support, workload, missing-version, and unknown policies deny.</summary>
    [Fact]
    public async Task Evaluator_DefaultDeniesUnsupportedContexts()
    {
        var evaluator = Evaluator(Membership([CreatorRole.Owner], []), PlanFacts(false));
        var scope = PlanScope(CustomerCreator);

        Assert.Equal(
            AuthorizationDenialReason.Unauthenticated,
            (await evaluator.AuthorizeAsync(new AuthorizationRequest(
                null,
                Permissions.AdventurePlanView,
                scope))).DenialReason);
        Assert.Equal(
            AuthorizationDenialReason.ActorTypeUnsupported,
            (await evaluator.AuthorizeAsync(new AuthorizationRequest(
                new ActorIdentity(
                    ActorType.Support,
                    "actor_support",
                    new UserId("user_support")),
                Permissions.AdventurePlanView,
                scope,
                membershipVersion: 1))).DenialReason);
        Assert.Equal(
            AuthorizationDenialReason.ActorTypeUnsupported,
            (await evaluator.AuthorizeAsync(new AuthorizationRequest(
                new ActorIdentity(ActorType.BackgroundJob, "job_planning"),
                Permissions.AdventurePlanView,
                scope))).DenialReason);
        Assert.Equal(
            AuthorizationDenialReason.InvalidRequest,
            (await evaluator.AuthorizeAsync(new AuthorizationRequest(
                CustomerActor,
                Permissions.AdventurePlanView,
                scope))).DenialReason);
        Assert.Equal(
            AuthorizationDenialReason.PermissionRequired,
            (await evaluator.AuthorizeAsync(Request(
                Permissions.PlanningEngagementDirectEdit,
                AuthorizationResourceScope.ForInstance(
                    CustomerCreator,
                    AuthorizationResourceTypes.PlanningEngagement,
                    "engagement_customer")))).DenialReason);
    }

    private static AuthorizationPolicyEvaluator Evaluator(
        CreatorMembershipSnapshot membership,
        AuthorizationResourceFacts? facts = null) => new(
            new MembershipProvider(membership),
            new ResourceFactsProvider(facts),
            new FixedTimeProvider(Now));

    private static AuthorizationRequest Request(
        Permission permission,
        AuthorizationResourceScope scope,
        long membershipVersion = 1) => new(
            CustomerActor,
            permission,
            scope,
            membershipVersion: membershipVersion);

    private static AuthorizationResourceScope PlanScope(CreatorId creatorId) =>
        AuthorizationResourceScope.ForInstance(
            creatorId,
            AuthorizationResourceTypes.AdventurePlan,
            "plan_customer");

    private static AuthorizationResourceFacts PlanFacts(bool isArchived) => new(
        CustomerCreator,
        AuthorizationResourceTypes.AdventurePlan,
        "plan_customer",
        isArchived,
        1);

    private static AuthorizationResourceScope ProposalScope() =>
        AuthorizationResourceScope.ForInstance(
            CustomerCreator,
            AuthorizationResourceTypes.PlanningProposal,
            "proposal_customer");

    private static AuthorizationResourceFacts ProposalFacts() => new(
        CustomerCreator,
        AuthorizationResourceTypes.PlanningProposal,
        "proposal_customer",
        false,
        1);

    private static CreatorMembershipSnapshot Membership(
        IEnumerable<CreatorRole> roles,
        IEnumerable<Permission> grants,
        CreatorMembershipStatus status = CreatorMembershipStatus.Active,
        long version = 1,
        DateTimeOffset? expiresAtUtc = null) => new(
            new("membership_customer"),
            CustomerUser,
            CustomerCreator,
            status,
            roles,
            grants,
            version,
            Now.AddDays(-1),
            expiresAtUtc);

    private sealed class MembershipProvider(CreatorMembershipSnapshot membership)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId,
            CreatorId creatorId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CreatorMembershipSnapshot?>(
                membership.UserId == userId && membership.CreatorId == creatorId
                    ? membership
                    : null);
        }
    }

    private sealed class ResourceFactsProvider(AuthorizationResourceFacts? facts)
        : IAuthorizationResourceFactsProvider
    {
        public Task<AuthorizationResourceFacts?> GetResourceFactsAsync(
            AuthorizationResourceScope resource,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(facts);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
