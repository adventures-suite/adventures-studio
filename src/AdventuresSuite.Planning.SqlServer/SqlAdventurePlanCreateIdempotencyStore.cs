using Dapper;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace AdventuresSuite.Planning.SqlServer;

internal sealed class SqlAdventurePlanCreateIdempotencyStore(
    CreatorId transactionCreatorId,
    SqlConnection connection,
    SqlTransaction transaction,
    AdventurePlanCreateIdempotencyTracker tracker)
    : IAdventurePlanCreateIdempotencyStore
{
    public async Task<AdventurePlanCreateIdempotencyResult> ReserveAsync(
        CreatorId creatorId,
        AdventurePlanCreateReservation reservation,
        CancellationToken cancellationToken = default)
    {
        if (creatorId == default || creatorId != transactionCreatorId)
        {
            throw new ArgumentException(
                "The idempotency Creator must match the transaction Creator.",
                nameof(creatorId));
        }

        ArgumentNullException.ThrowIfNull(reservation);
        var parameters = new
        {
            CreatorId = creatorId.Value,
            reservation.Operation,
            IdempotencyKey = reservation.IdempotencyKey.Value,
            FingerprintVersion = reservation.Fingerprint.Version,
            RequestFingerprint = reservation.Fingerprint.ToArray(),
            AdventurePlanId = reservation.AdventurePlanId.Value,
            reservation.ResultingVersion,
            reservation.CreatedAtUtc,
            reservation.ExpiresAtUtc
        };

        var lockResult = await connection.QuerySingleAsync<int>(Command("""
            DECLARE @Result int;
            DECLARE @Resource nvarchar(255) = N'PlanningCreate:' +
                CONVERT(varchar(64), HASHBYTES('SHA2_256',
                    CONCAT(@CreatorId, NCHAR(31), @Operation, NCHAR(31), @IdempotencyKey)), 2);
            EXEC @Result = sys.sp_getapplock
                @Resource=@Resource,
                @LockMode='Exclusive',
                @LockOwner='Transaction',
                @LockTimeout=15000;
            SELECT @Result;
            """, parameters, cancellationToken));
        if (lockResult < 0)
        {
            throw new InvalidOperationException(
                "The Adventure Plan creation idempotency key could not be serialized.");
        }

        var existing = await connection.QuerySingleOrDefaultAsync<StoredResult>(Command("""
            SELECT results.FingerprintVersion,
                   results.RequestFingerprint,
                   results.AdventurePlanId,
                   results.ResultingVersion,
                   CASE WHEN plans.AdventurePlanId IS NULL THEN 0 ELSE 1 END AS PlanExists,
                   plans.Version AS PlanVersion,
                   (SELECT COUNT_BIG(*)
                      FROM audit.AuditEvents AS auditEvents
                     WHERE auditEvents.CreatorId=results.CreatorId
                       AND auditEvents.Permission='AdventurePlan.Create'
                       AND auditEvents.ResourceType='AdventurePlan'
                       AND auditEvents.ResourceId=results.AdventurePlanId
                       AND auditEvents.Outcome='Succeeded'
                       AND auditEvents.PreviousVersion IS NULL
                       AND auditEvents.ResultingVersion=results.ResultingVersion) AS AuditCount
              FROM planning.AdventurePlanCreateResults AS results
              LEFT JOIN planning.AdventurePlans AS plans
                ON plans.CreatorId COLLATE Latin1_General_100_BIN2=results.CreatorId
               AND plans.AdventurePlanId COLLATE Latin1_General_100_BIN2=results.AdventurePlanId
             WHERE results.CreatorId=@CreatorId
               AND results.Operation=@Operation
               AND results.IdempotencyKey=@IdempotencyKey;
            """, parameters, cancellationToken));

        if (existing is not null)
        {
            if (existing.FingerprintVersion != reservation.Fingerprint.Version
                || !existing.RequestFingerprint.AsSpan().SequenceEqual(
                    reservation.Fingerprint.ToArray()))
            {
                return new(
                    AdventurePlanCreateIdempotencyOutcome.Conflict,
                    AdventurePlanId: null,
                    ResultingVersion: null);
            }

            if (existing.PlanExists != 1
                || existing.PlanVersion != existing.ResultingVersion
                || existing.ResultingVersion != 1
                || existing.AuditCount != 1)
            {
                throw new InvalidOperationException(
                    "The durable Adventure Plan creation result does not match authoritative state.");
            }

            return new(
                AdventurePlanCreateIdempotencyOutcome.Replay,
                new AdventurePlanId(existing.AdventurePlanId),
                existing.ResultingVersion);
        }

        await connection.ExecuteAsync(Command("""
            INSERT planning.AdventurePlanCreateResults
              (CreatorId,Operation,IdempotencyKey,FingerprintVersion,RequestFingerprint,
               AdventurePlanId,ResultingVersion,CreatedAtUtc,ExpiresAtUtc)
            VALUES
              (@CreatorId,@Operation,@IdempotencyKey,@FingerprintVersion,@RequestFingerprint,
               @AdventurePlanId,@ResultingVersion,@CreatedAtUtc,@ExpiresAtUtc);
            """, parameters, cancellationToken));
        tracker.Record(reservation.AdventurePlanId, reservation.ResultingVersion);
        return new(
            AdventurePlanCreateIdempotencyOutcome.Reserved,
            reservation.AdventurePlanId,
            reservation.ResultingVersion);
    }

    private CommandDefinition Command(
        string commandText,
        object parameters,
        CancellationToken cancellationToken) =>
        new(commandText, parameters, transaction, cancellationToken: cancellationToken);

    private sealed record StoredResult(
        int FingerprintVersion,
        byte[] RequestFingerprint,
        string AdventurePlanId,
        long ResultingVersion,
        int PlanExists,
        long? PlanVersion,
        long AuditCount);
}
