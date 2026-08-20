using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning.Persistence;

/// <summary>Requests one durable, provenance-bearing FootStep application result.</summary>
public sealed record PlannerFootStepApplicationReservation
{
    /// <summary>Gets the target private plan.</summary>
    public required AdventurePlanId AdventurePlanId { get; init; }
    /// <summary>Gets the retry key supplied by the reviewed form.</summary>
    public required PlanningIdempotencyKey IdempotencyKey { get; init; }
    /// <summary>Gets the versioned request fingerprint.</summary>
    public required PlanningRequestFingerprint Fingerprint { get; init; }
    /// <summary>Gets the immutable FootStep identity.</summary>
    public required string FootStepId { get; init; }
    /// <summary>Gets the exact immutable FootStep version.</summary>
    public required string FootStepVersion { get; init; }
    /// <summary>Gets the allowlisted resulting Planning record type.</summary>
    public required string TargetType { get; init; }
    /// <summary>Gets the fresh resulting Planning record identity.</summary>
    public required string TargetId { get; init; }
    /// <summary>Gets the plan version produced by the original application.</summary>
    public required long ResultingVersion { get; init; }
    /// <summary>Gets the immutable attribution snapshot.</summary>
    public required string Attribution { get; init; }
    /// <summary>Gets the opaque use-decision reference.</summary>
    public required string UseDecisionReference { get; init; }
    /// <summary>Gets when the FootStep was applied in UTC.</summary>
    public required DateTimeOffset AppliedAtUtc { get; init; }
}

/// <summary>Classifies durable FootStep application key resolution.</summary>
public enum PlannerFootStepApplicationOutcome
{
    /// <summary>The transaction reserved a new application result.</summary>
    Reserved,
    /// <summary>The identical request previously committed.</summary>
    Replay,
    /// <summary>The key belongs to a different request.</summary>
    Conflict
}

/// <summary>Returns a durable FootStep application result without source content.</summary>
/// <param name="Outcome">The key-resolution outcome.</param>
/// <param name="TargetId">The previously committed target identity for a replay.</param>
/// <param name="ResultingVersion">The previously committed plan version for a replay.</param>
public sealed record PlannerFootStepApplicationResult(
    PlannerFootStepApplicationOutcome Outcome,
    string? TargetId,
    long? ResultingVersion);

/// <summary>Persists append-only, Creator-scoped FootStep application and retry evidence.</summary>
public interface IPlannerFootStepApplicationStore
{
    /// <summary>Serializes and resolves one application key within the transaction Creator.</summary>
    Task<PlannerFootStepApplicationResult> ResolveAsync(
        CreatorId creatorId,
        PlannerFootStepApplicationReservation reservation,
        CancellationToken cancellationToken = default);

    /// <summary>Persists a newly resolved application after its target mutation succeeds.</summary>
    Task AddAsync(
        CreatorId creatorId,
        PlannerFootStepApplicationReservation reservation,
        CancellationToken cancellationToken = default);
}
