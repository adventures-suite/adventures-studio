namespace TheSimontonAdventures.Web.Planning.Persistence;

/// <summary>Reports a rejected write against a stale Adventure Plan version.</summary>
public sealed class PlanningConcurrencyException : Exception
{
    /// <summary>Initializes a concurrency failure for a specific plan and version.</summary>
    public PlanningConcurrencyException(
        AdventurePlanId planId,
        long expectedVersion)
        : base($"Adventure Plan '{planId}' no longer has expected version {expectedVersion}.")
    {
        if (planId == default)
        {
            throw new ArgumentException("A valid Adventure Plan identity is required.", nameof(planId));
        }

        if (expectedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        }

        PlanId = planId;
        ExpectedVersion = expectedVersion;
    }

    /// <summary>Gets the plan whose update was rejected.</summary>
    public AdventurePlanId PlanId { get; }

    /// <summary>Gets the stale version supplied by the caller.</summary>
    public long ExpectedVersion { get; }
}
