using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Describes authoritative facts about one protected resource instance.</summary>
public sealed record AuthorizationResourceFacts
{
    /// <summary>Initializes authoritative resource ownership and lifecycle facts.</summary>
    public AuthorizationResourceFacts(
        CreatorId creatorId,
        AuthorizationResourceType resourceType,
        string resourceId,
        bool isArchived,
        long version)
    {
        if (creatorId == default || resourceType == default)
        {
            throw new ArgumentException("Resource facts require Creator and resource identities.");
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        CreatorId = creatorId;
        ResourceType = resourceType;
        ResourceId = AuthorizationIdentity.Require(resourceId, nameof(resourceId));
        IsArchived = isArchived;
        Version = version;
    }

    /// <summary>Gets the authoritative owning Creator.</summary>
    public CreatorId CreatorId { get; }
    /// <summary>Gets the authoritative protected resource type.</summary>
    public AuthorizationResourceType ResourceType { get; }
    /// <summary>Gets the stable resource identity.</summary>
    public string ResourceId { get; }
    /// <summary>Gets whether the resource is archived.</summary>
    public bool IsArchived { get; }
    /// <summary>Gets the authoritative positive resource version.</summary>
    public long Version { get; }
}

/// <summary>Loads authoritative resource facts before private data is accessed.</summary>
public interface IAuthorizationResourceFactsProvider
{
    /// <summary>Gets safe authorization facts for one requested resource instance.</summary>
    Task<AuthorizationResourceFacts?> GetResourceFactsAsync(
        AuthorizationResourceScope resource,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Evaluates initial Creator membership, permission, ownership, lifecycle, and
/// audit policies without depending on a UI, web framework, or identity provider.
/// </summary>
public sealed class AuthorizationPolicyEvaluator(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationResourceFactsProvider resourceFactsProvider,
    TimeProvider timeProvider) : IAuthorizationPolicyEvaluator
{
    /// <inheritdoc />
    public async Task<AuthorizationDecision> AuthorizeAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Actor is null)
        {
            return AuthorizationDecision.Deny(AuthorizationDenialReason.Unauthenticated);
        }

        if (request.Actor.Type != ActorType.Human || !request.Actor.UserId.HasValue)
        {
            return AuthorizationDecision.Deny(IsHumanOnly(request.Permission)
                ? AuthorizationDenialReason.HumanActorRequired
                : AuthorizationDenialReason.ActorTypeUnsupported);
        }

        if (!request.MembershipVersion.HasValue)
        {
            return AuthorizationDecision.Deny(AuthorizationDenialReason.InvalidRequest);
        }

        var membership = await membershipProvider.GetMembershipAsync(
            request.Actor.UserId.Value,
            request.Resource.CreatorId,
            cancellationToken);
        if (membership is null)
        {
            return AuthorizationDecision.Deny(AuthorizationDenialReason.MembershipRequired);
        }

        if (membership.UserId != request.Actor.UserId.Value
            || membership.CreatorId != request.Resource.CreatorId)
        {
            return AuthorizationDecision.Deny(AuthorizationDenialReason.ResourceScopeMismatch);
        }

        if (membership.Version != request.MembershipVersion.Value)
        {
            return AuthorizationDecision.Deny(AuthorizationDenialReason.StaleAuthorizationContext);
        }

        var utcNow = timeProvider.GetUtcNow();
        if (!membership.IsActiveAt(utcNow))
        {
            return AuthorizationDecision.Deny(AuthorizationDenialReason.AccessRevoked);
        }

        if (!membership.Permissions.Contains(request.Permission))
        {
            return AuthorizationDecision.Deny(AuthorizationDenialReason.PermissionRequired);
        }

        var policy = GetPolicy(request.Permission, request.Resource.ScopeType);
        if (policy is null
            || policy.Value.ResourceType != request.Resource.ResourceType
            || policy.Value.ScopeType != request.Resource.ScopeType)
        {
            return AuthorizationDecision.Deny(AuthorizationDenialReason.InvalidRequest);
        }

        if (request.Resource.ScopeType == AuthorizationResourceScopeType.ResourceInstance)
        {
            var facts = await resourceFactsProvider.GetResourceFactsAsync(
                request.Resource,
                cancellationToken);
            if (facts is null
                || facts.CreatorId != request.Resource.CreatorId
                || facts.ResourceType != request.Resource.ResourceType
                || facts.ResourceId != request.Resource.ResourceId)
            {
                return AuthorizationDecision.Deny(AuthorizationDenialReason.ResourceScopeMismatch);
            }

            if (policy.Value.RequiredArchiveState.HasValue
                && facts.IsArchived != policy.Value.RequiredArchiveState.Value)
            {
                return AuthorizationDecision.Deny(AuthorizationDenialReason.InvalidRequest);
            }
        }

        return AuthorizationDecision.Allow(policy.Value.AuditRequirement);
    }

    private static bool IsHumanOnly(Permission permission) => permission == Permissions.CreatorManageMembers
        || permission == Permissions.AdventurePlanRestore
        || permission == Permissions.PlanningProposalReview
        || permission == Permissions.PlanningProposalApplyApproved;

    private static PolicyDefinition? GetPolicy(
        Permission permission,
        AuthorizationResourceScopeType requestedScopeType)
    {
        if (permission == Permissions.CreatorView)
        {
            return Collection(AuthorizationResourceTypes.Creator);
        }

        if (permission == Permissions.CreatorManageMembers)
        {
            return Collection(
                AuthorizationResourceTypes.Creator,
                AuthorizationAuditRequirement.RequiredMutation);
        }

        if (permission == Permissions.AdventurePlanCreate
            || permission == Permissions.AdventurePlanViewArchived)
        {
            return Collection(
                AuthorizationResourceTypes.AdventurePlan,
                permission == Permissions.AdventurePlanCreate
                    ? AuthorizationAuditRequirement.RequiredMutation
                    : AuthorizationAuditRequirement.None);
        }

        if (permission == Permissions.AdventurePlanView)
        {
            return requestedScopeType == AuthorizationResourceScopeType.CreatorCollection
                ? Collection(AuthorizationResourceTypes.AdventurePlan)
                : Instance(AuthorizationResourceTypes.AdventurePlan);
        }

        if (permission == Permissions.AdventurePlanEdit)
        {
            return Instance(
                AuthorizationResourceTypes.AdventurePlan,
                isArchived: false,
                auditRequirement: AuthorizationAuditRequirement.RequiredMutation);
        }

        if (permission == Permissions.AdventurePlanArchive)
        {
            return Instance(
                AuthorizationResourceTypes.AdventurePlan,
                isArchived: false,
                auditRequirement: AuthorizationAuditRequirement.RequiredMutation);
        }

        if (permission == Permissions.AdventurePlanRestore)
        {
            return Instance(
                AuthorizationResourceTypes.AdventurePlan,
                isArchived: true,
                auditRequirement: AuthorizationAuditRequirement.RequiredMutation);
        }

        if (permission == Permissions.AdventurePlanViewSensitiveReservations)
        {
            return Instance(
                AuthorizationResourceTypes.AdventurePlan,
                auditRequirement: AuthorizationAuditRequirement.RequiredSensitiveRead);
        }

        if (permission == Permissions.PlanningProposalSubmit
            || permission == Permissions.PlanningProposalReview
            || permission == Permissions.PlanningProposalApplyApproved)
        {
            return Instance(
                AuthorizationResourceTypes.PlanningProposal,
                auditRequirement: AuthorizationAuditRequirement.RequiredMutation);
        }

        if (permission == Permissions.AuditView)
        {
            return Collection(
                AuthorizationResourceTypes.Audit,
                AuthorizationAuditRequirement.RequiredSensitiveRead);
        }

        return null;
    }

    private static PolicyDefinition Collection(
        AuthorizationResourceType resourceType,
        AuthorizationAuditRequirement auditRequirement = AuthorizationAuditRequirement.None) =>
        new(resourceType, AuthorizationResourceScopeType.CreatorCollection, null, auditRequirement);

    private static PolicyDefinition Instance(
        AuthorizationResourceType resourceType,
        bool? isArchived = null,
        AuthorizationAuditRequirement auditRequirement = AuthorizationAuditRequirement.None) =>
        new(resourceType, AuthorizationResourceScopeType.ResourceInstance, isArchived, auditRequirement);

    private readonly record struct PolicyDefinition(
        AuthorizationResourceType ResourceType,
        AuthorizationResourceScopeType ScopeType,
        bool? RequiredArchiveState,
        AuthorizationAuditRequirement AuditRequirement);
}
