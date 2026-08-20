using Dapper;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace AdventuresSuite.Planning.SqlServer;

internal sealed class SqlPlannerFootStepApplicationStore(
    CreatorId transactionCreatorId,
    SqlConnection connection,
    SqlTransaction transaction,
    PlannerFootStepApplicationTracker tracker) : IPlannerFootStepApplicationStore
{
    public async Task<PlannerFootStepApplicationResult> ResolveAsync(
        CreatorId creatorId,
        PlannerFootStepApplicationReservation reservation,
        CancellationToken cancellationToken = default)
    {
        Validate(creatorId, reservation);

        var parameters = new
        {
            CreatorId = creatorId.Value,
            AdventurePlanId = reservation.AdventurePlanId.Value,
            IdempotencyKey = reservation.IdempotencyKey.Value,
            FingerprintVersion = reservation.Fingerprint.Version,
            RequestFingerprint = reservation.Fingerprint.ToArray(),
            reservation.FootStepId,
            reservation.FootStepVersion,
            reservation.TargetType,
            reservation.TargetId,
            reservation.ResultingVersion,
            reservation.Attribution,
            reservation.UseDecisionReference,
            reservation.AppliedAtUtc
        };
        var lockResult = await connection.QuerySingleAsync<int>(new CommandDefinition("""
            DECLARE @Result int;
            DECLARE @Resource nvarchar(255) = N'PlanningFootStep:' +
                CONVERT(varchar(64), HASHBYTES('SHA2_256',
                    CONCAT(@CreatorId, NCHAR(31), @AdventurePlanId, NCHAR(31), @IdempotencyKey)), 2);
            EXEC @Result = sys.sp_getapplock @Resource=@Resource, @LockMode='Exclusive',
                @LockOwner='Transaction', @LockTimeout=15000;
            SELECT @Result;
            """, parameters, transaction, cancellationToken: cancellationToken));
        if (lockResult < 0) throw new InvalidOperationException("The FootStep application key could not be serialized.");

        var existing = await connection.QuerySingleOrDefaultAsync<StoredResult>(new CommandDefinition("""
            SELECT a.FingerprintVersion,a.RequestFingerprint,a.TargetId,a.ResultingVersion,
                   CASE WHEN d.DestinationVisitId IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS TargetExists,
                   CASE WHEN EXISTS
                     (SELECT 1 FROM audit.AuditEvents e
                       WHERE e.CreatorId=a.CreatorId AND e.ResourceType=N'AdventurePlan'
                         AND e.ResourceId=a.AdventurePlanId AND e.ResultingVersion=a.ResultingVersion
                         AND e.Outcome=N'Succeeded' AND e.ReasonCategory=N'Completed')
                     THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS AuditExists
              FROM planning.PlannerFootStepApplications a
              LEFT JOIN planning.DestinationVisits d
                ON d.CreatorId=a.CreatorId AND d.AdventurePlanId=a.AdventurePlanId
               AND d.DestinationVisitId=a.TargetId
             WHERE a.CreatorId=@CreatorId AND a.AdventurePlanId=@AdventurePlanId
               AND a.IdempotencyKey=@IdempotencyKey;
            """, parameters, transaction, cancellationToken: cancellationToken));
        if (existing is not null)
        {
            return existing.FingerprintVersion == reservation.Fingerprint.Version
                && existing.RequestFingerprint.AsSpan().SequenceEqual(reservation.Fingerprint.ToArray())
                && existing.TargetExists && existing.AuditExists
                ? new(PlannerFootStepApplicationOutcome.Replay, existing.TargetId, existing.ResultingVersion)
                : new(PlannerFootStepApplicationOutcome.Conflict, null, null);
        }

        return new(PlannerFootStepApplicationOutcome.Reserved, reservation.TargetId, reservation.ResultingVersion);
    }

    public async Task AddAsync(
        CreatorId creatorId,
        PlannerFootStepApplicationReservation reservation,
        CancellationToken cancellationToken = default)
    {
        Validate(creatorId, reservation);

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT planning.PlannerFootStepApplications
              (CreatorId,AdventurePlanId,IdempotencyKey,FingerprintVersion,RequestFingerprint,
               FootStepId,FootStepVersion,TargetType,TargetId,ResultingVersion,Attribution,
               UseDecisionReference,AppliedAtUtc)
            VALUES
              (@CreatorId,@AdventurePlanId,@IdempotencyKey,@FingerprintVersion,@RequestFingerprint,
               @FootStepId,@FootStepVersion,@TargetType,@TargetId,@ResultingVersion,@Attribution,
               @UseDecisionReference,@AppliedAtUtc);
            """, new
        {
            CreatorId = creatorId.Value,
            AdventurePlanId = reservation.AdventurePlanId.Value,
            IdempotencyKey = reservation.IdempotencyKey.Value,
            FingerprintVersion = reservation.Fingerprint.Version,
            RequestFingerprint = reservation.Fingerprint.ToArray(),
            reservation.FootStepId,
            reservation.FootStepVersion,
            reservation.TargetType,
            reservation.TargetId,
            reservation.ResultingVersion,
            reservation.Attribution,
            reservation.UseDecisionReference,
            reservation.AppliedAtUtc
        }, transaction, cancellationToken: cancellationToken));
        tracker.Record(reservation.AdventurePlanId, reservation.ResultingVersion);
    }

    private void Validate(CreatorId creatorId, PlannerFootStepApplicationReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (creatorId == default || creatorId != transactionCreatorId
            || reservation.AdventurePlanId == default || reservation.IdempotencyKey == default
            || reservation.Fingerprint is null || reservation.TargetType != "DestinationVisit"
            || string.IsNullOrWhiteSpace(reservation.TargetId)
            || reservation.ResultingVersion < 2 || reservation.AppliedAtUtc.Offset != TimeSpan.Zero
            || string.IsNullOrWhiteSpace(reservation.FootStepId)
            || string.IsNullOrWhiteSpace(reservation.FootStepVersion)
            || string.IsNullOrWhiteSpace(reservation.Attribution)
            || string.IsNullOrWhiteSpace(reservation.UseDecisionReference))
        {
            throw new ArgumentException("FootStep application evidence is invalid or crosses Creator scope.");
        }
    }

    private sealed record StoredResult(
        int FingerprintVersion,
        byte[] RequestFingerprint,
        string TargetId,
        long ResultingVersion,
        bool TargetExists,
        bool AuditExists);
}

internal sealed class PlannerFootStepApplicationTracker
{
    private readonly List<(AdventurePlanId PlanId, long ResultingVersion)> reservations = [];

    public void Record(AdventurePlanId planId, long resultingVersion) =>
        reservations.Add((planId, resultingVersion));

    public void ValidateForCommit(PlanningMutationAuditTracker auditTracker)
    {
        foreach (var reservation in reservations)
        {
            if (!auditTracker.HasExactlyOneAuditedUpdate(reservation.PlanId, reservation.ResultingVersion))
            {
                throw new InvalidOperationException(
                    "A FootStep application requires one matching audited Planning mutation.");
            }
        }
    }
}
