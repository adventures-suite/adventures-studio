using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning.Persistence;

/// <summary>Records the immutable source of one independently owned Adventure Plan.</summary>
public sealed record AdventurePlanTemplateOrigin
{
    /// <summary>Gets the customer Creator that owns the resulting plan.</summary>
    public required CreatorId CreatorId { get; init; }
    /// <summary>Gets the independently created customer plan identity.</summary>
    public required AdventurePlanId AdventurePlanId { get; init; }
    /// <summary>Gets the exact source template and published version.</summary>
    public required AdventureTemplateVersionId TemplateVersion { get; init; }
    /// <summary>Gets the immutable template owner classification.</summary>
    public required AdventureTemplateOwnerType TemplateOwnerType { get; init; }
    /// <summary>Gets the source-owner identity without granting plan access.</summary>
    public required string TemplateOwnerId { get; init; }
    /// <summary>Gets the BCP 47 source content locale.</summary>
    public required string SourceLocale { get; init; }
    /// <summary>Gets the immutable attribution snapshot.</summary>
    public required string Attribution { get; init; }
    /// <summary>Gets the opaque approved use-decision reference.</summary>
    public required string UseDecisionReference { get; init; }
    /// <summary>Gets a versioned request fingerprint without retaining parameters.</summary>
    public required PlanningRequestFingerprint ParameterFingerprint { get; init; }
    /// <summary>Gets when instantiation occurred as UTC.</summary>
    public required DateTimeOffset InstantiatedAtUtc { get; init; }
}

/// <summary>Persists append-only Adventure Plan template provenance.</summary>
public interface IAdventurePlanTemplateOriginStore
{
    /// <summary>Adds origin evidence within the transaction Creator boundary.</summary>
    Task AddAsync(
        CreatorId creatorId,
        AdventurePlanTemplateOrigin origin,
        CancellationToken cancellationToken = default);
}
