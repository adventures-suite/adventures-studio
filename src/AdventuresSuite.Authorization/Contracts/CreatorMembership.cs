using System.Collections.Frozen;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Classifies the current authorization state of a Creator membership.</summary>
public enum CreatorMembershipStatus
{
    /// <summary>The invitation has not yet become an active membership.</summary>
    Pending,
    /// <summary>The membership may authorize operations within its effective period.</summary>
    Active,
    /// <summary>The membership is administratively disabled.</summary>
    Disabled,
    /// <summary>The membership was permanently revoked.</summary>
    Revoked
}

/// <summary>Names an initial Creator-scoped administrative permission bundle.</summary>
public enum CreatorRole
{
    /// <summary>Full Creator administration and Planning control.</summary>
    Owner,
    /// <summary>Creator administration and Planning control without ownership semantics.</summary>
    Administrator,
    /// <summary>Creates and manages Adventure Plans.</summary>
    Planner,
    /// <summary>Contributes to existing Adventure Plans and proposals.</summary>
    Contributor,
    /// <summary>Views non-sensitive Creator and Planning information.</summary>
    Viewer
}

/// <summary>Defines the initial permissions represented by each Creator role.</summary>
public static class CreatorRolePermissionBundles
{
    private static readonly FrozenSet<Permission> ViewerPermissions =
        new[] { Permissions.CreatorView, Permissions.AdventurePlanView }.ToFrozenSet();

    private static readonly FrozenSet<Permission> ContributorPermissions = ViewerPermissions
        .Concat([Permissions.AdventurePlanEdit, Permissions.PlanningProposalSubmit])
        .ToFrozenSet();

    private static readonly FrozenSet<Permission> PlannerPermissions = ContributorPermissions
        .Concat([
            Permissions.AdventurePlanCreate,
            Permissions.AdventurePlanViewArchived,
            Permissions.AdventurePlanArchive,
            Permissions.AdventurePlanRestore,
            Permissions.AdventurePlanViewSensitiveReservations,
            Permissions.PlanningProposalReview,
            Permissions.PlanningProposalApplyApproved
        ])
        .ToFrozenSet();

    private static readonly FrozenSet<Permission> AdministratorPermissions = PlannerPermissions
        .Concat([
            Permissions.CreatorManageMembers,
            Permissions.AdventurePlanManageCompanionPolicy,
            Permissions.PlanningEngagementInvite,
            Permissions.PlanningEngagementManage,
            Permissions.AuditView
        ])
        .ToFrozenSet();

    /// <summary>Gets the immutable permission bundle for one initial role.</summary>
    public static IReadOnlySet<Permission> GetPermissions(CreatorRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        return role switch
        {
            CreatorRole.Viewer => ViewerPermissions,
            CreatorRole.Contributor => ContributorPermissions,
            CreatorRole.Planner => PlannerPermissions,
            CreatorRole.Administrator or CreatorRole.Owner => AdministratorPermissions,
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
    }
}

/// <summary>Represents current provider-independent Creator membership facts.</summary>
public sealed record CreatorMembershipSnapshot
{
    /// <summary>Initializes a validated immutable membership snapshot.</summary>
    public CreatorMembershipSnapshot(
        CreatorMembershipId id,
        UserId userId,
        CreatorId creatorId,
        CreatorMembershipStatus status,
        IEnumerable<CreatorRole> roles,
        IEnumerable<Permission>? permissionGrants,
        long version,
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset? expiresAtUtc = null)
    {
        if (id == default || userId == default || creatorId == default)
        {
            throw new ArgumentException("Membership, user, and Creator identities are required.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (effectiveFromUtc.Offset != TimeSpan.Zero
            || (expiresAtUtc.HasValue && expiresAtUtc.Value.Offset != TimeSpan.Zero)
            || (expiresAtUtc.HasValue && expiresAtUtc.Value <= effectiveFromUtc))
        {
            throw new ArgumentException("Membership effective timestamps must be ordered UTC values.");
        }

        var roleSet = (roles ?? throw new ArgumentNullException(nameof(roles))).ToFrozenSet();
        if (roleSet.Count == 0 || roleSet.Any(role => !Enum.IsDefined(role)))
        {
            throw new ArgumentException("At least one valid Creator role is required.", nameof(roles));
        }

        var grants = (permissionGrants ?? []).ToFrozenSet();
        if (grants.Any(permission => permission == default))
        {
            throw new ArgumentException("Permission grants cannot contain default values.", nameof(permissionGrants));
        }

        Id = id;
        UserId = userId;
        CreatorId = creatorId;
        Status = status;
        Roles = roleSet;
        PermissionGrants = grants;
        Permissions = roleSet
            .SelectMany(CreatorRolePermissionBundles.GetPermissions)
            .Concat(grants)
            .ToFrozenSet();
        Version = version;
        EffectiveFromUtc = effectiveFromUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Gets the stable membership identity.</summary>
    public CreatorMembershipId Id { get; }
    /// <summary>Gets the member's stable human identity.</summary>
    public UserId UserId { get; }
    /// <summary>Gets the Creator in which this membership grants permissions.</summary>
    public CreatorId CreatorId { get; }
    /// <summary>Gets the current membership status.</summary>
    public CreatorMembershipStatus Status { get; }
    /// <summary>Gets the immutable assigned role set.</summary>
    public IReadOnlySet<CreatorRole> Roles { get; }
    /// <summary>Gets immutable explicit permission grants in addition to role bundles.</summary>
    public IReadOnlySet<Permission> PermissionGrants { get; }
    /// <summary>Gets the immutable effective permission set.</summary>
    public IReadOnlySet<Permission> Permissions { get; }
    /// <summary>Gets the positive concurrency and revocation version.</summary>
    public long Version { get; }
    /// <summary>Gets when the membership becomes effective in UTC.</summary>
    public DateTimeOffset EffectiveFromUtc { get; }
    /// <summary>Gets when the membership expires in UTC, when bounded.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; }

    /// <summary>Determines whether the membership is active at a UTC instant.</summary>
    public bool IsActiveAt(DateTimeOffset utcNow)
    {
        if (utcNow.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Authorization evaluation time must use UTC.", nameof(utcNow));
        }

        return Status == CreatorMembershipStatus.Active
            && utcNow >= EffectiveFromUtc
            && (!ExpiresAtUtc.HasValue || utcNow < ExpiresAtUtc.Value);
    }
}

/// <summary>Loads current membership facts without coupling policy code to persistence.</summary>
public interface ICreatorMembershipProvider
{
    /// <summary>Gets the current membership for one user and Creator, if one exists.</summary>
    Task<CreatorMembershipSnapshot?> GetMembershipAsync(
        UserId userId,
        CreatorId creatorId,
        CancellationToken cancellationToken = default);
}
