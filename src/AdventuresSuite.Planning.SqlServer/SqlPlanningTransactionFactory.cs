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

    public SqlPlanningTransaction(
        CreatorId creatorId,
        SqlConnection connection,
        SqlTransaction transaction)
    {
        CreatorId = creatorId;
        this.connection = connection;
        this.transaction = transaction;
        auditTracker = new PlanningMutationAuditTracker(creatorId);
        AdventurePlans = new DapperAdventurePlanRepository(
            creatorId, connection, transaction, auditTracker);
    }

    public CreatorId CreatorId { get; }

    public IAdventurePlanRepository AdventurePlans { get; }

    public IRequiredAuditIntentCollector RequiredAuditIntents => auditTracker;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        var auditEvents = auditTracker.ValidateForCommit();
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

    private sealed record PlanningMutation(
        AdventurePlanId PlanId,
        long? PreviousVersion,
        long ResultingVersion);
}
