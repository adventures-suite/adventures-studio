using Dapper;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace AdventuresSuite.Planning.SqlServer;

/// <summary>Persists append-only template-origin evidence in a Planning transaction.</summary>
internal sealed class SqlAdventurePlanTemplateOriginStore(
    CreatorId transactionCreatorId,
    SqlConnection connection,
    SqlTransaction transaction,
    AdventurePlanTemplateOriginTracker tracker) : IAdventurePlanTemplateOriginStore
{
    /// <inheritdoc />
    public async Task AddAsync(
        CreatorId creatorId,
        AdventurePlanTemplateOrigin origin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (creatorId == default || creatorId != transactionCreatorId
            || origin.CreatorId != creatorId || origin.AdventurePlanId == default
            || origin.TemplateVersion == default || origin.ParameterFingerprint is null
            || origin.InstantiatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Template origin must match the transaction Creator and plan.");
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT planning.AdventurePlanTemplateOrigins
              (CreatorId,AdventurePlanId,TemplateId,TemplateVersion,TemplateOwnerType,
               TemplateOwnerId,SourceLocale,Attribution,UseDecisionReference,
               ParameterFingerprintVersion,ParameterFingerprint,InstantiatedAtUtc)
            VALUES
              (@CreatorId,@AdventurePlanId,@TemplateId,@TemplateVersion,@TemplateOwnerType,
               @TemplateOwnerId,@SourceLocale,@Attribution,@UseDecisionReference,
               @ParameterFingerprintVersion,@ParameterFingerprint,@InstantiatedAtUtc);
            """, new
        {
            CreatorId = creatorId.Value,
            AdventurePlanId = origin.AdventurePlanId.Value,
            origin.TemplateVersion.TemplateId,
            TemplateVersion = origin.TemplateVersion.Version,
            TemplateOwnerType = origin.TemplateOwnerType.ToString(),
            origin.TemplateOwnerId,
            origin.SourceLocale,
            origin.Attribution,
            origin.UseDecisionReference,
            ParameterFingerprintVersion = origin.ParameterFingerprint.Version,
            ParameterFingerprint = origin.ParameterFingerprint.ToArray(),
            origin.InstantiatedAtUtc
        }, transaction, cancellationToken: cancellationToken));
        tracker.Record(origin.AdventurePlanId);
    }
}
