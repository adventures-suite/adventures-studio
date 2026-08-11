using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning.Persistence;

/// <summary>
/// Defines provider-independent persistence operations for private,
/// Creator-scoped Adventure Plans.
/// </summary>
public interface IAdventurePlanRepository
{
    /// <summary>Lists minimum non-sensitive dashboard projections for active plans.</summary>
    Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves one plan only within the supplied Creator boundary.</summary>
    Task<AdventurePlan?> GetAsync(
        CreatorId creatorId,
        AdventurePlanId planId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists non-archived plans owned by the supplied Creator.</summary>
    Task<IReadOnlyList<AdventurePlan>> ListAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists recoverable archived plans owned by the supplied Creator.</summary>
    Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new plan whose ownership matches the supplied Creator.</summary>
    Task AddAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a plan when its persisted version matches the expected version.
    /// </summary>
    /// <exception cref="PlanningConcurrencyException">
    /// The persisted plan has changed since <paramref name="expectedVersion"/>.
    /// </exception>
    Task UpdateAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        long expectedVersion,
        CancellationToken cancellationToken = default);
}

/// <summary>Projects only fields approved for the read-only Planner dashboard.</summary>
public sealed record AdventurePlanDashboardItem
{
    /// <summary>Gets the stable plan identity.</summary>
    public required AdventurePlanId Id { get; init; }
    /// <summary>Gets the private working title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets the optional private working description.</summary>
    public string? WorkingDescription { get; init; }
    /// <summary>Gets the lifecycle stage independently of publication.</summary>
    public required AdventureLifecycleStage LifecycleStage { get; init; }
    /// <summary>Gets the private planning status.</summary>
    public required PlanningStatus Status { get; init; }
    /// <summary>Gets the local-calendar planning range.</summary>
    public required PlanningDateRange Dates { get; init; }
    /// <summary>Gets the number of planned destination visits.</summary>
    public required int DestinationCount { get; init; }
    /// <summary>Gets the number of incomplete planning tasks.</summary>
    public required int OpenTaskCount { get; init; }
}
