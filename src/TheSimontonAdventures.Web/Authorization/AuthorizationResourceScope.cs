using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Distinguishes collection authorization from instance authorization.</summary>
public enum AuthorizationResourceScopeType
{
    /// <summary>A Creator-owned collection, used for create and list operations.</summary>
    CreatorCollection,
    /// <summary>One existing Creator-owned resource instance.</summary>
    ResourceInstance
}

/// <summary>Names a provider-independent protected resource category.</summary>
public readonly record struct AuthorizationResourceType
{
    /// <summary>Initializes a protected resource category.</summary>
    public AuthorizationResourceType(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value != value.Trim()
            || value.Length > 100
            || !char.IsAsciiLetter(value[0])
            || value.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException("A normalized resource type is required.", nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the canonical resource category.</summary>
    public string Value { get; }
}

/// <summary>Defines the initial protected resource categories.</summary>
public static class AuthorizationResourceTypes
{
    /// <summary>The Creator workspace and membership collection.</summary>
    public static readonly AuthorizationResourceType Creator = new("Creator");
    /// <summary>The private Adventure Plan collection and its instances.</summary>
    public static readonly AuthorizationResourceType AdventurePlan = new("AdventurePlan");
    /// <summary>The future Planning proposal collection and its instances.</summary>
    public static readonly AuthorizationResourceType PlanningProposal = new("PlanningProposal");
    /// <summary>The future Planning Engagement collection and its instances.</summary>
    public static readonly AuthorizationResourceType PlanningEngagement = new("PlanningEngagement");
    /// <summary>Creator-scoped audit history.</summary>
    public static readonly AuthorizationResourceType Audit = new("Audit");
}

/// <summary>Scopes an authorization request to a Creator collection or resource instance.</summary>
public sealed record AuthorizationResourceScope
{
    private AuthorizationResourceScope(
        AuthorizationResourceScopeType scopeType,
        CreatorId creatorId,
        AuthorizationResourceType resourceType,
        string? resourceId)
    {
        if (creatorId == default)
        {
            throw new ArgumentException("A valid Creator identity is required.", nameof(creatorId));
        }

        if (resourceType == default)
        {
            throw new ArgumentException("A valid resource type is required.", nameof(resourceType));
        }

        ScopeType = scopeType;
        CreatorId = creatorId;
        ResourceType = resourceType;
        ResourceId = resourceId;
    }

    /// <summary>Gets whether this scope targets a collection or an instance.</summary>
    public AuthorizationResourceScopeType ScopeType { get; }
    /// <summary>Gets the owning Creator identity.</summary>
    public CreatorId CreatorId { get; }
    /// <summary>Gets the protected resource category.</summary>
    public AuthorizationResourceType ResourceType { get; }
    /// <summary>Gets the stable resource identity for an instance scope.</summary>
    public string? ResourceId { get; }

    /// <summary>Creates a Creator collection scope for create and list operations.</summary>
    public static AuthorizationResourceScope ForCollection(
        CreatorId creatorId,
        AuthorizationResourceType resourceType) =>
        new(AuthorizationResourceScopeType.CreatorCollection, creatorId, resourceType, null);

    /// <summary>Creates a Creator-owned resource instance scope.</summary>
    public static AuthorizationResourceScope ForInstance(
        CreatorId creatorId,
        AuthorizationResourceType resourceType,
        string resourceId) =>
        new(AuthorizationResourceScopeType.ResourceInstance, creatorId, resourceType,
            AuthorizationIdentity.Require(resourceId, nameof(resourceId)));
}
