using System.Reflection;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies provider-independent identity and authorization invariants.</summary>
public sealed class AuthorizationContractTests
{
    private static readonly CreatorId Alpha = new("creator_alpha");
    private static readonly CreatorId Beta = new("creator_beta");
    private static readonly ActorIdentity Human = new(
        ActorType.Human,
        "actor_steve",
        new UserId("user_steve"));

    /// <summary>Ensures stable human and membership identities reject unsafe values.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("User_One")]
    [InlineData("user-one")]
    public void Identities_InvalidValue_Throws(string? value)
    {
        Assert.Throws<ArgumentException>(() => new UserId(value!));
        Assert.Throws<ArgumentException>(() => new CreatorMembershipId(value!));
    }

    /// <summary>Ensures actor types cannot confuse human and workload identity.</summary>
    [Fact]
    public void ActorIdentity_EnforcesHumanUserBoundary()
    {
        Assert.True(Human.IsHuman);
        var support = new ActorIdentity(
            ActorType.Support,
            "actor_support",
            new UserId("user_support"));
        Assert.True(support.RepresentsPerson);
        Assert.False(support.IsHuman);
        Assert.False(new ActorIdentity(ActorType.System, "system_platform").IsHuman);
        Assert.False(new ActorIdentity(ActorType.BackgroundJob, "job_planning").IsHuman);
        Assert.Throws<ArgumentException>(() =>
            new ActorIdentity(ActorType.Human, "actor_missing_user"));
        Assert.Throws<ArgumentException>(() =>
            new ActorIdentity(ActorType.System, "system_invalid", new UserId("user_steve")));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorIdentity((ActorType)999, "actor_unknown"));
    }

    /// <summary>Ensures collection and instance scopes cannot be confused.</summary>
    [Fact]
    public void ResourceScope_RepresentsCollectionAndInstanceExplicitly()
    {
        var collection = AuthorizationResourceScope.ForCollection(
            Alpha,
            AuthorizationResourceTypes.AdventurePlan);
        var instance = AuthorizationResourceScope.ForInstance(
            Alpha,
            AuthorizationResourceTypes.AdventurePlan,
            "plan_spain_2027");

        Assert.Equal(AuthorizationResourceScopeType.CreatorCollection, collection.ScopeType);
        Assert.Null(collection.ResourceId);
        Assert.Equal(AuthorizationResourceScopeType.ResourceInstance, instance.ScopeType);
        Assert.Equal("plan_spain_2027", instance.ResourceId);
        Assert.Throws<ArgumentException>(() => AuthorizationResourceScope.ForCollection(
            default,
            AuthorizationResourceTypes.AdventurePlan));
        Assert.Throws<ArgumentException>(() => AuthorizationResourceScope.ForInstance(
            Alpha,
            AuthorizationResourceTypes.AdventurePlan,
            "invalid-plan"));
    }

    /// <summary>Ensures permissions are canonical and proposal access remains weaker.</summary>
    [Fact]
    public void Permissions_UseApprovedDistinctVocabulary()
    {
        Assert.Equal("AdventurePlan.ViewArchived", Permissions.AdventurePlanViewArchived.Value);
        Assert.NotEqual(Permissions.PlanningProposalSubmit, Permissions.PlanningEngagementDirectEdit);
        Assert.Throws<ArgumentException>(() => new Permission("AdventurePlan"));
        Assert.Throws<ArgumentException>(() => new Permission(" AdventurePlan.View"));
        Assert.Throws<ArgumentException>(() => new Permission("AdventurePlan.View.Private"));
    }

    /// <summary>Exercises the initial authorization matrix with a deterministic fake.</summary>
    [Fact]
    public async Task AuthorizationMatrix_DeterministicEvaluator_DeniesUnsafeContexts()
    {
        var evaluator = new MatrixEvaluator(
            Alpha,
            [Permissions.AdventurePlanView, Permissions.AdventurePlanCreate,
             Permissions.AdventurePlanViewArchived, Permissions.PlanningProposalSubmit],
            membershipIsActive: true);
        var collection = AuthorizationResourceScope.ForCollection(
            Alpha,
            AuthorizationResourceTypes.AdventurePlan);

        var anonymous = await evaluator.AuthorizeAsync(new(
            null,
            Permissions.AdventurePlanView,
            collection));
        Assert.Equal(AuthorizationDenialReason.Unauthenticated, anonymous.DenialReason);

        Assert.True((await evaluator.AuthorizeAsync(new(
            Human,
            Permissions.AdventurePlanCreate,
            collection))).IsAllowed);
        Assert.True((await evaluator.AuthorizeAsync(new(
            Human,
            Permissions.AdventurePlanViewArchived,
            collection))).IsAllowed);

        var crossCreator = await evaluator.AuthorizeAsync(new(
            Human,
            Permissions.AdventurePlanView,
            AuthorizationResourceScope.ForInstance(
                Beta,
                AuthorizationResourceTypes.AdventurePlan,
                "plan_spain_2027")));
        Assert.Equal(AuthorizationDenialReason.ResourceScopeMismatch, crossCreator.DenialReason);

        var directEdit = await evaluator.AuthorizeAsync(new(
            Human,
            Permissions.PlanningEngagementDirectEdit,
            AuthorizationResourceScope.ForInstance(
                Alpha,
                AuthorizationResourceTypes.AdventurePlan,
                "plan_spain_2027")));
        Assert.Equal(AuthorizationDenialReason.PermissionRequired, directEdit.DenialReason);

        var systemApproval = await evaluator.AuthorizeAsync(new(
            new ActorIdentity(ActorType.BackgroundJob, "job_apply_proposal"),
            Permissions.PlanningProposalApplyApproved,
            AuthorizationResourceScope.ForInstance(
                Alpha,
                AuthorizationResourceTypes.PlanningProposal,
                "proposal_spain")));
        Assert.Equal(AuthorizationDenialReason.HumanActorRequired, systemApproval.DenialReason);

        var supportApproval = await evaluator.AuthorizeAsync(new(
            new ActorIdentity(ActorType.Support, "actor_support", new UserId("user_support")),
            Permissions.PlanningProposalApplyApproved,
            AuthorizationResourceScope.ForInstance(
                Alpha,
                AuthorizationResourceTypes.PlanningProposal,
                "proposal_spain")));
        Assert.Equal(AuthorizationDenialReason.HumanActorRequired, supportApproval.DenialReason);

        var revokedEvaluator = new MatrixEvaluator(
            Alpha,
            [Permissions.AdventurePlanView],
            membershipIsActive: false);
        var revoked = await revokedEvaluator.AuthorizeAsync(new(
            Human,
            Permissions.AdventurePlanView,
            collection));
        Assert.Equal(AuthorizationDenialReason.AccessRevoked, revoked.DenialReason);
    }

    /// <summary>Ensures audit intent is redacted, Creator-consistent, and versioned.</summary>
    [Fact]
    public void AuditIntent_ValidatesAtomicMutationMetadata()
    {
        var resource = AuthorizationResourceScope.ForInstance(
            Alpha,
            AuthorizationResourceTypes.AdventurePlan,
            "plan_spain_2027");
        var intent = new AuditEventIntent(
            new("audit_restore_01"),
            Human,
            Alpha,
            Permissions.AdventurePlanRestore,
            resource,
            AuditOutcome.Succeeded,
            AuditReasonCategory.Completed,
            new DateTimeOffset(2026, 8, 7, 22, 0, 0, TimeSpan.Zero),
            new("0f1234567890abcdef1234567890abcd"),
            previousVersion: 3,
            resultingVersion: 4);

        Assert.Equal(3, intent.PreviousVersion);
        Assert.Equal(4, intent.ResultingVersion);
        Assert.Throws<ArgumentException>(() => new AuditEventIntent(
            new("audit_restore_02"), Human, Beta, Permissions.AdventurePlanRestore,
            resource, AuditOutcome.Succeeded, AuditReasonCategory.Completed,
            new DateTimeOffset(2026, 8, 7, 22, 0, 0, TimeSpan.Zero),
            new("correlation_restore_02")));
        Assert.Throws<ArgumentException>(() => new AuditEventIntent(
            new("audit_restore_03"), Human, Alpha, Permissions.AdventurePlanRestore,
            resource, AuditOutcome.Succeeded, AuditReasonCategory.Completed,
            new DateTimeOffset(2026, 8, 7, 22, 0, 0, TimeSpan.FromHours(-7)),
            new("correlation_restore_03")));
    }

    /// <summary>Ensures audit intent preserves initiating-human attribution.</summary>
    [Fact]
    public void AuditIntent_BackgroundExecution_PreservesInitiatingHuman()
    {
        var background = new ActorIdentity(ActorType.BackgroundJob, "job_apply_proposal");
        var resource = AuthorizationResourceScope.ForInstance(
            Alpha,
            AuthorizationResourceTypes.PlanningProposal,
            "proposal_spain");
        var intent = new AuditEventIntent(
            new("audit_proposal_01"),
            background,
            Alpha,
            Permissions.PlanningProposalApplyApproved,
            resource,
            AuditOutcome.Succeeded,
            AuditReasonCategory.Completed,
            new DateTimeOffset(2026, 8, 7, 22, 0, 0, TimeSpan.Zero),
            new("1234567890abcdef1234567890abcdef"),
            initiatingActor: Human);

        Assert.Equal(Human, intent.InitiatingActor);
        Assert.Throws<ArgumentException>(() => new AuditEventIntent(
            new("audit_proposal_02"), background, Alpha,
            Permissions.PlanningProposalApplyApproved, resource,
            AuditOutcome.Succeeded, AuditReasonCategory.Completed,
            new DateTimeOffset(2026, 8, 7, 22, 0, 0, TimeSpan.Zero),
            new("correlation_proposal_02"),
            initiatingActor: new ActorIdentity(
                ActorType.Support,
                "actor_support",
                new UserId("user_support"))));
    }

    /// <summary>Ensures audit outcomes carry compatible safe reason categories.</summary>
    [Fact]
    public void AuditIntent_ReasonCategory_MustMatchOutcome()
    {
        var resource = AuthorizationResourceScope.ForInstance(
            Alpha,
            AuthorizationResourceTypes.AdventurePlan,
            "plan_spain_2027");

        var rejected = new AuditEventIntent(
            new("audit_rejected_01"), Human, Alpha, Permissions.AdventurePlanRestore,
            resource, AuditOutcome.Rejected, AuditReasonCategory.AccessRevoked,
            new DateTimeOffset(2026, 8, 7, 22, 0, 0, TimeSpan.Zero),
            new("correlation_rejected_01"));
        Assert.Equal(AuditReasonCategory.AccessRevoked, rejected.ReasonCategory);

        Assert.Throws<ArgumentException>(() => new AuditEventIntent(
            new("audit_rejected_02"), Human, Alpha, Permissions.AdventurePlanRestore,
            resource, AuditOutcome.Succeeded, AuditReasonCategory.PermissionRequired,
            new DateTimeOffset(2026, 8, 7, 22, 0, 0, TimeSpan.Zero),
            new("correlation_rejected_02")));
        Assert.Throws<ArgumentException>(() => new AuditEventIntent(
            new("audit_failed_01"), Human, Alpha, Permissions.AdventurePlanRestore,
            resource, AuditOutcome.Failed, AuditReasonCategory.Completed,
            new DateTimeOffset(2026, 8, 7, 22, 0, 0, TimeSpan.Zero),
            new("correlation_failed_01")));
    }

    /// <summary>Ensures initiating attribution is limited to background execution.</summary>
    [Fact]
    public void AuthorizationRequest_InitiatingActor_RequiresHumanAndWorkloadExecutor()
    {
        var resource = AuthorizationResourceScope.ForCollection(
            Alpha,
            AuthorizationResourceTypes.AdventurePlan);
        var background = new ActorIdentity(ActorType.BackgroundJob, "job_planning");

        var request = new AuthorizationRequest(
            background,
            Permissions.AdventurePlanCreate,
            resource,
            Human);
        Assert.Equal(Human, request.InitiatingActor);

        Assert.Throws<ArgumentException>(() => new AuthorizationRequest(
            background,
            Permissions.AdventurePlanCreate,
            resource,
            new ActorIdentity(
                ActorType.Support,
                "actor_support",
                new UserId("user_support"))));
        Assert.Throws<ArgumentException>(() => new AuthorizationRequest(
            Human,
            Permissions.AdventurePlanCreate,
            resource,
            Human));
    }

    /// <summary>Ensures core authorization contracts expose no framework or provider types.</summary>
    [Fact]
    public void AuthorizationContracts_DoNotExposeFrameworkOrProviderTypes()
    {
        Type[] contracts =
        [
            typeof(IAuthorizationPolicyEvaluator),
            typeof(IRequiredAuditIntentCollector),
            typeof(AuthorizationRequest),
            typeof(AuthorizationDecision),
            typeof(AuditEventIntent)
        ];

        var assemblyNames = contracts
            .SelectMany(type => type.GetMethods().SelectMany(SignatureTypes))
            .Select(type => type.Assembly.GetName().Name ?? string.Empty);

        Assert.DoesNotContain(assemblyNames, name =>
            name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Identity", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal)
            || name.StartsWith("Dapper", StringComparison.Ordinal));
    }

    private static IEnumerable<Type> SignatureTypes(MethodInfo method)
    {
        yield return Unwrap(method.ReturnType);
        foreach (var parameter in method.GetParameters())
        {
            yield return Unwrap(parameter.ParameterType);
        }
    }

    private static Type Unwrap(Type type) => type.IsGenericType
        ? type.GetGenericArguments().Last()
        : type;

    private sealed class MatrixEvaluator(
        CreatorId memberCreatorId,
        IReadOnlyCollection<Permission> permissions,
        bool membershipIsActive) : IAuthorizationPolicyEvaluator
    {
        private static readonly HashSet<Permission> HumanOnlyPermissions =
        [Permissions.PlanningProposalReview, Permissions.PlanningProposalApplyApproved,
         Permissions.CreatorManageMembers, Permissions.AdventurePlanRestore];

        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Actor is null)
            {
                return Task.FromResult(AuthorizationDecision.Deny(
                    AuthorizationDenialReason.Unauthenticated));
            }

            if (HumanOnlyPermissions.Contains(request.Permission)
                && request.Actor.Type != ActorType.Human)
            {
                return Task.FromResult(AuthorizationDecision.Deny(
                    AuthorizationDenialReason.HumanActorRequired));
            }

            if (!membershipIsActive)
            {
                return Task.FromResult(AuthorizationDecision.Deny(
                    AuthorizationDenialReason.AccessRevoked));
            }

            if (request.Resource.CreatorId != memberCreatorId)
            {
                return Task.FromResult(AuthorizationDecision.Deny(
                    AuthorizationDenialReason.ResourceScopeMismatch));
            }

            return Task.FromResult(permissions.Contains(request.Permission)
                ? AuthorizationDecision.Allow()
                : AuthorizationDecision.Deny(AuthorizationDenialReason.PermissionRequired));
        }
    }
}
