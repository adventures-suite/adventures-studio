using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning.Persistence;

/// <summary>
/// Defines provider-independent persistence operations for private,
/// Creator-scoped Adventure Plans.
/// </summary>
public interface IAdventurePlanRepository
{
    /// <summary>Retrieves one plan only within the supplied Creator boundary.</summary>
    Task<AdventurePlan?> GetAsync(
        CreatorId creatorId,
        AdventurePlanId planId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists plans owned by the supplied Creator.</summary>
    Task<IReadOnlyList<AdventurePlan>> ListAsync(
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
