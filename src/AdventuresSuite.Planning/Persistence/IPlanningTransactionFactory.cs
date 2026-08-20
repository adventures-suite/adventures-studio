using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Authorization;

namespace TheSimontonAdventures.Web.Planning.Persistence;

/// <summary>Begins Creator-scoped Planning persistence transactions.</summary>
public interface IPlanningTransactionFactory
{
    /// <summary>Begins a transaction bound to one explicit Creator.</summary>
    Task<IPlanningTransaction> BeginAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents one private Planning transaction and its scoped repositories.
/// </summary>
public interface IPlanningTransaction : IAsyncDisposable
{
    /// <summary>Gets the Creator identity fixed when the transaction began.</summary>
    CreatorId CreatorId { get; }

    /// <summary>Gets Adventure Plan persistence operations participating in this transaction.</summary>
    IAdventurePlanRepository AdventurePlans { get; }

    /// <summary>Gets Creator-scoped durable Adventure Plan creation idempotency.</summary>
    IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency { get; }

    /// <summary>Gets immutable template-origin persistence participating in this transaction.</summary>
    IAdventurePlanTemplateOriginStore AdventurePlanTemplateOrigins =>
        throw new NotSupportedException(
            "This transaction does not support Adventure Template provenance.");

    /// <summary>Gets durable FootStep application idempotency and provenance persistence.</summary>
    IPlannerFootStepApplicationStore PlannerFootStepApplications =>
        throw new NotSupportedException(
            "This transaction does not support FootStep application provenance.");

    /// <summary>
    /// Gets the collector for audit intent that must commit atomically with
    /// Planning mutations in this transaction.
    /// </summary>
    IRequiredAuditIntentCollector RequiredAuditIntents { get; }

    /// <summary>Commits all validated changes atomically.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
