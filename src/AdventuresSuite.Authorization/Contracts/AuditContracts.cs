using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Authorization;

internal static class CorrelationIdentity
{
    public static string Require(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length is < 3 or > 128
            || value.Any(character => character is not (>= 'a' and <= 'z')
                and not (>= 'A' and <= 'Z')
                and not (>= '0' and <= '9')
                and not '_' and not '-' and not '.'))
        {
            throw new ArgumentException(
                "Correlation identities must contain 3-128 ASCII letters, digits, periods, hyphens, or underscores.",
                parameterName);
        }

        return value;
    }
}

/// <summary>Identifies one required audit event independently of its storage provider.</summary>
public readonly record struct AuditEventId
{
    /// <summary>Initializes a stable audit-event identity.</summary>
    public AuditEventId(string value) => Value = AuthorizationIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity.</summary>
    public string Value { get; }
}

/// <summary>Identifies one correlated operation across platform boundaries.</summary>
public readonly record struct CorrelationId
{
    /// <summary>Initializes a stable correlation identity.</summary>
    public CorrelationId(string value) => Value = CorrelationIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity.</summary>
    public string Value { get; }
}

/// <summary>Describes the outcome recorded for a required audited operation.</summary>
public enum AuditOutcome
{
    /// <summary>The operation succeeded.</summary>
    Succeeded,
    /// <summary>The operation was rejected before mutation.</summary>
    Rejected,
    /// <summary>The operation failed without committing a mutation.</summary>
    Failed
}

/// <summary>Classifies a safe, non-sensitive reason for an audited outcome.</summary>
public enum AuditReasonCategory
{
    /// <summary>The authorized operation completed successfully.</summary>
    Completed,
    /// <summary>No usable authenticated actor was available.</summary>
    Unauthenticated,
    /// <summary>An applicable active membership was not available.</summary>
    MembershipRequired,
    /// <summary>The actor lacked the required permission.</summary>
    PermissionRequired,
    /// <summary>The requested resource did not match the authorized Creator scope.</summary>
    ResourceScopeMismatch,
    /// <summary>The applicable membership or delegated access was revoked or stale.</summary>
    AccessRevoked,
    /// <summary>The operation required an ordinary human customer decision.</summary>
    HumanActorRequired,
    /// <summary>Safe validation rejected the operation.</summary>
    ValidationFailed,
    /// <summary>Optimistic concurrency prevented the operation.</summary>
    ConcurrencyConflict,
    /// <summary>An operational dependency prevented completion.</summary>
    DependencyFailure,
    /// <summary>An unexpected failure prevented completion.</summary>
    UnexpectedFailure
}

/// <summary>Captures redacted audit intent for a protected operation.</summary>
public sealed record AuditEventIntent
{
    /// <summary>Initializes and validates an audit event intent.</summary>
    public AuditEventIntent(
        AuditEventId id,
        ActorIdentity actor,
        CreatorId creatorId,
        Permission permission,
        AuthorizationResourceScope resource,
        AuditOutcome outcome,
        AuditReasonCategory reasonCategory,
        DateTimeOffset occurredAtUtc,
        CorrelationId correlationId,
        ActorIdentity? initiatingActor = null,
        long? previousVersion = null,
        long? resultingVersion = null)
    {
        if (id == default || correlationId == default)
        {
            throw new ArgumentException("Valid audit and correlation identities are required.");
        }

        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        if (initiatingActor is not null && initiatingActor.Type != ActorType.Human)
        {
            throw new ArgumentException(
                "An initiating audit actor must be an ordinary authenticated human.",
                nameof(initiatingActor));
        }

        if (initiatingActor is not null
            && actor.Type is not (ActorType.System or ActorType.BackgroundJob))
        {
            throw new ArgumentException(
                "Initiating-human attribution is valid only for system or background execution.",
                nameof(initiatingActor));
        }

        if (creatorId == default || resource is null || resource.CreatorId != creatorId)
        {
            throw new ArgumentException("Audit intent must use one matching Creator scope.", nameof(creatorId));
        }

        if (permission == default || !Enum.IsDefined(outcome) || !Enum.IsDefined(reasonCategory))
        {
            throw new ArgumentException("Audit intent requires a valid permission, outcome, and reason category.");
        }

        var reasonMatchesOutcome = outcome switch
        {
            AuditOutcome.Succeeded => reasonCategory == AuditReasonCategory.Completed,
            AuditOutcome.Rejected => reasonCategory is not AuditReasonCategory.Completed
                and not AuditReasonCategory.DependencyFailure
                and not AuditReasonCategory.UnexpectedFailure,
            AuditOutcome.Failed => reasonCategory is AuditReasonCategory.ConcurrencyConflict
                or AuditReasonCategory.DependencyFailure
                or AuditReasonCategory.UnexpectedFailure,
            _ => false
        };
        if (!reasonMatchesOutcome)
        {
            throw new ArgumentException(
                "The audit reason category is not valid for the selected outcome.",
                nameof(reasonCategory));
        }

        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Audit timestamps must use UTC.", nameof(occurredAtUtc));
        }

        if (previousVersion is < 1 || resultingVersion is < 1
            || (previousVersion.HasValue && resultingVersion.HasValue
                && resultingVersion <= previousVersion))
        {
            throw new ArgumentException("Audit versions must be positive and advance when both are supplied.");
        }

        Id = id;
        CreatorId = creatorId;
        Permission = permission;
        Resource = resource;
        Outcome = outcome;
        ReasonCategory = reasonCategory;
        OccurredAtUtc = occurredAtUtc;
        CorrelationId = correlationId;
        InitiatingActor = initiatingActor;
        PreviousVersion = previousVersion;
        ResultingVersion = resultingVersion;
    }

    /// <summary>Gets the stable audit-event identity.</summary>
    public AuditEventId Id { get; }
    /// <summary>Gets the actor responsible for the operation.</summary>
    public ActorIdentity Actor { get; }
    /// <summary>Gets the ordinary human who initiated non-human execution, when applicable.</summary>
    public ActorIdentity? InitiatingActor { get; }
    /// <summary>Gets the Creator scope.</summary>
    public CreatorId CreatorId { get; }
    /// <summary>Gets the permission evaluated.</summary>
    public Permission Permission { get; }
    /// <summary>Gets the collection or instance scope without protected payload.</summary>
    public AuthorizationResourceScope Resource { get; }
    /// <summary>Gets the audited outcome.</summary>
    public AuditOutcome Outcome { get; }
    /// <summary>Gets the safe reason category explaining the audited outcome.</summary>
    public AuditReasonCategory ReasonCategory { get; }
    /// <summary>Gets the UTC occurrence time.</summary>
    public DateTimeOffset OccurredAtUtc { get; }
    /// <summary>Gets the operation correlation identity.</summary>
    public CorrelationId CorrelationId { get; }
    /// <summary>Gets the previous aggregate version when applicable.</summary>
    public long? PreviousVersion { get; }
    /// <summary>Gets the resulting aggregate version when applicable.</summary>
    public long? ResultingVersion { get; }
}

/// <summary>
/// Collects audit intent that must commit atomically with its protected mutation.
/// </summary>
public interface IRequiredAuditIntentCollector
{
    /// <summary>
    /// Adds required redacted audit intent to the current transaction or its
    /// transactional outbox; failure must prevent the mutation from committing.
    /// </summary>
    void AddRequired(AuditEventIntent auditEvent);
}
