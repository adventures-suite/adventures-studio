using Dapper;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace AdventuresSuite.Planning.SqlServer;

/// <summary>Creates Creator-scoped SQL Server transactions for Planning persistence.</summary>
public sealed class SqlPlanningTransactionFactory : IPlanningTransactionFactory
{
    private readonly string connectionString;

    /// <summary>Initializes the factory with a SQL Server connection string.</summary>
    public SqlPlanningTransactionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));
        }

        this.connectionString = connectionString;
    }

    /// <inheritdoc />
    public async Task<IPlanningTransaction> BeginAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        if (creatorId == default)
        {
            throw new ArgumentException("A valid Creator identity is required.", nameof(creatorId));
        }

        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            return new SqlPlanningTransaction(creatorId, connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

internal sealed class SqlPlanningTransaction : IPlanningTransaction
{
    private readonly SqlConnection connection;
    private readonly SqlTransaction transaction;
    private bool completed;
    private readonly PlanningMutationAuditTracker auditTracker;
    private readonly AdventurePlanCreateIdempotencyTracker idempotencyTracker;
    private readonly AdventurePlanTemplateOriginTracker templateOriginTracker;
    private readonly PlannerFootStepApplicationTracker footStepApplicationTracker;

    public SqlPlanningTransaction(
        CreatorId creatorId,
        SqlConnection connection,
        SqlTransaction transaction)
    {
        CreatorId = creatorId;
        this.connection = connection;
        this.transaction = transaction;
        auditTracker = new PlanningMutationAuditTracker(creatorId);
        idempotencyTracker = new AdventurePlanCreateIdempotencyTracker();
        templateOriginTracker = new AdventurePlanTemplateOriginTracker();
        footStepApplicationTracker = new PlannerFootStepApplicationTracker();
        AdventurePlans = new DapperAdventurePlanRepository(
            creatorId, connection, transaction, auditTracker);
        AdventurePlanCreateIdempotency = new SqlAdventurePlanCreateIdempotencyStore(
            creatorId, connection, transaction, idempotencyTracker);
        AdventurePlanTemplateOrigins = new SqlAdventurePlanTemplateOriginStore(
            creatorId, connection, transaction, templateOriginTracker);
        PlannerFootStepApplications = new SqlPlannerFootStepApplicationStore(
            creatorId, connection, transaction, footStepApplicationTracker);
    }

    public CreatorId CreatorId { get; }

    public IAdventurePlanRepository AdventurePlans { get; }

    public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency { get; }

    public IAdventurePlanTemplateOriginStore AdventurePlanTemplateOrigins { get; }

    public IPlannerFootStepApplicationStore PlannerFootStepApplications { get; }

    public IRequiredAuditIntentCollector RequiredAuditIntents => auditTracker;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        var auditEvents = auditTracker.ValidateForCommit();
        idempotencyTracker.ValidateForCommit(auditTracker, templateOriginTracker);
        footStepApplicationTracker.ValidateForCommit(auditTracker);
        foreach (var auditEvent in auditEvents)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT audit.AuditEvents
                  (AuditEventId,CreatorId,ActorType,ActorUserId,Permission,ResourceType,ResourceId,
                   Outcome,ReasonCategory,OccurredAtUtc,CorrelationId,PreviousVersion,ResultingVersion)
                VALUES
                  (@AuditEventId,@CreatorId,@ActorType,@ActorUserId,@Permission,@ResourceType,@ResourceId,
                   @Outcome,@ReasonCategory,@OccurredAtUtc,@CorrelationId,@PreviousVersion,@ResultingVersion);
                """, new
            {
                AuditEventId = auditEvent.Id.Value,
                CreatorId = auditEvent.CreatorId.Value,
                ActorType = auditEvent.Actor.Type.ToString(),
                ActorUserId = auditEvent.Actor.UserId?.Value,
                Permission = auditEvent.Permission.Value,
                ResourceType = auditEvent.Resource.ResourceType.Value,
                auditEvent.Resource.ResourceId,
                Outcome = auditEvent.Outcome.ToString(),
                ReasonCategory = auditEvent.ReasonCategory.ToString(),
                auditEvent.OccurredAtUtc,
                CorrelationId = auditEvent.CorrelationId.Value,
                auditEvent.PreviousVersion,
                auditEvent.ResultingVersion
            }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!completed)
            {
                await transaction.RollbackAsync();
            }
        }
        finally
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                await connection.DisposeAsync();
                completed = true;
            }
        }
    }
}

internal sealed class PlanningMutationAuditTracker(CreatorId creatorId)
    : IRequiredAuditIntentCollector
{
    private readonly List<PlanningMutation> mutations = [];
    private readonly List<AuditEventIntent> auditEvents = [];
    private bool mutationFailed;

    public void AddRequired(AuditEventIntent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        if (auditEvent.CreatorId != creatorId
            || auditEvent.Resource.CreatorId != creatorId
            || auditEvent.Resource.ScopeType != AuthorizationResourceScopeType.ResourceInstance
            || auditEvent.Resource.ResourceType != AuthorizationResourceTypes.AdventurePlan
            || auditEvent.Outcome != AuditOutcome.Succeeded
            || auditEvent.ReasonCategory != AuditReasonCategory.Completed)
        {
            throw new ArgumentException(
                "Planning audit intent must describe one successful Adventure Plan mutation in the transaction Creator.",
                nameof(auditEvent));
        }

        auditEvents.Add(auditEvent);
    }

    public void RecordMutation(
        AdventurePlanId planId,
        long? previousVersion,
        long resultingVersion)
    {
        if (planId == default || previousVersion is < 1 || resultingVersion < 1
            || (previousVersion.HasValue && resultingVersion != previousVersion.Value + 1)
            || (!previousVersion.HasValue && resultingVersion != 1))
        {
            mutationFailed = true;
            throw new ArgumentException("Planning mutation versions are invalid.");
        }

        mutations.Add(new(planId, previousVersion, resultingVersion));
    }

    public void RecordFailure() => mutationFailed = true;

    public IReadOnlyList<AuditEventIntent> ValidateForCommit()
    {
        if (mutationFailed)
        {
            throw new InvalidOperationException(
                "A failed Planning mutation cannot be committed.");
        }

        if (mutations.Count != auditEvents.Count)
        {
            throw new InvalidOperationException(
                "Every Planning mutation requires exactly one matching audit intent.");
        }

        var remaining = new List<AuditEventIntent>(auditEvents);
        foreach (var mutation in mutations)
        {
            var index = remaining.FindIndex(auditEvent => Matches(mutation, auditEvent));
            if (index < 0)
            {
                throw new InvalidOperationException(
                    "Planning mutation audit intent does not match the affected resource and version.");
            }

            remaining.RemoveAt(index);
        }

        return auditEvents.AsReadOnly();
    }

    private static bool Matches(PlanningMutation mutation, AuditEventIntent auditEvent)
    {
        var permissionMatches = mutation.PreviousVersion.HasValue
            ? auditEvent.Permission == Permissions.AdventurePlanEdit
                || auditEvent.Permission == Permissions.AdventurePlanArchive
                || auditEvent.Permission == Permissions.AdventurePlanRestore
            : auditEvent.Permission == Permissions.AdventurePlanCreate;
        return permissionMatches
            && auditEvent.Resource.ResourceId == mutation.PlanId.Value
            && auditEvent.PreviousVersion == mutation.PreviousVersion
            && auditEvent.ResultingVersion == mutation.ResultingVersion;
    }

    public bool HasExactlyOneAuditedCreate(AdventurePlanId planId, long resultingVersion) =>
        mutations.Count(mutation => mutation.PlanId == planId
            && mutation.PreviousVersion is null
            && mutation.ResultingVersion == resultingVersion) == 1
        && auditEvents.Count(auditEvent =>
            auditEvent.Permission == Permissions.AdventurePlanCreate
            && auditEvent.Resource.ResourceId == planId.Value
            && auditEvent.PreviousVersion is null
            && auditEvent.ResultingVersion == resultingVersion) == 1;

    public bool HasExactlyOneAuditedUpdate(AdventurePlanId planId, long resultingVersion) =>
        mutations.Count(mutation => mutation.PlanId == planId
            && mutation.PreviousVersion == resultingVersion - 1
            && mutation.ResultingVersion == resultingVersion) == 1
        && auditEvents.Count(auditEvent =>
            auditEvent.Permission == Permissions.AdventurePlanEdit
            && auditEvent.Resource.ResourceId == planId.Value
            && auditEvent.PreviousVersion == resultingVersion - 1
            && auditEvent.ResultingVersion == resultingVersion) == 1;

    private sealed record PlanningMutation(
        AdventurePlanId PlanId,
        long? PreviousVersion,
        long ResultingVersion);
}

internal sealed class AdventurePlanCreateIdempotencyTracker
{
    private readonly List<(string Operation, AdventurePlanId PlanId, long ResultingVersion)> reservations = [];

    public void Record(string operation, AdventurePlanId planId, long resultingVersion) =>
        reservations.Add((operation, planId, resultingVersion));

    public void ValidateForCommit(
        PlanningMutationAuditTracker auditTracker,
        AdventurePlanTemplateOriginTracker templateOriginTracker)
    {
        foreach (var reservation in reservations)
        {
            if (!auditTracker.HasExactlyOneAuditedCreate(
                    reservation.PlanId,
                    reservation.ResultingVersion)
                || (string.Equals(
                        reservation.Operation,
                        PlanningIdempotencyOperations.AdventurePlanTemplateInstantiateV1,
                        StringComparison.Ordinal)
                    ? !templateOriginTracker.HasExactlyOne(reservation.PlanId)
                    : templateOriginTracker.HasAny(reservation.PlanId)))
            {
                throw new InvalidOperationException(
                    "A new idempotency reservation must match exactly one created and audited Adventure Plan.");
            }
        }

        if (!templateOriginTracker.AllMatch(
                reservations
                    .Where(item => string.Equals(
                        item.Operation,
                        PlanningIdempotencyOperations.AdventurePlanTemplateInstantiateV1,
                        StringComparison.Ordinal))
                    .Select(item => item.PlanId)))
        {
            throw new InvalidOperationException(
                "Template provenance must match exactly one template-instantiation reservation.");
        }

        if (reservations.Select(item => item.PlanId).Distinct().Count() != reservations.Count)
        {
            throw new InvalidOperationException(
                "An Adventure Plan creation cannot satisfy multiple idempotency reservations.");
        }
    }
}

internal sealed class AdventurePlanTemplateOriginTracker
{
    private readonly List<AdventurePlanId> planIds = [];

    public void Record(AdventurePlanId planId) => planIds.Add(planId);

    public bool HasExactlyOne(AdventurePlanId planId) => planIds.Count(item => item == planId) == 1;

    public bool HasAny(AdventurePlanId planId) => planIds.Contains(planId);

    public bool AllMatch(IEnumerable<AdventurePlanId> expectedPlanIds) =>
        planIds.OrderBy(item => item.Value, StringComparer.Ordinal).SequenceEqual(
            expectedPlanIds.OrderBy(item => item.Value, StringComparer.Ordinal));
}
