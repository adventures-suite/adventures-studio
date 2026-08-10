using AdventuresSuite.Companion.Application;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.Companion.SqlServer;

/// <summary>Queries authoritative, traveler-scoped Companion read projections from SQL Server.</summary>
public sealed class SqlCompanionAdventureQueries(string connectionString)
    : ICompanionAdventureSummaryQuery, ICompanionAdventureDetailQuery
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CompanionAdventureSummaryProjection>> ListAsync(
        CompanionAdventureReadScope scope,
        int maximumResults,
        bool includeCompleted,
        CancellationToken cancellationToken = default)
    {
        Validate(scope, maximumResults);
        const string sql = """
            SELECT TOP (@MaximumResults)
                ap.AdventurePlanId, tp.TravelerId, ap.Title, ap.PlanningStatus,
                ap.StartDate, ap.EndDate,
                COALESCE(firstVisit.TimeZone, 'Etc/UTC') AS PrimaryTimeZone,
                ap.Version AS PlanVersion, tp.Version AS ParticipationVersion,
                CASE WHEN ap.UpdatedAtUtc >= tp.UpdatedAtUtc THEN ap.UpdatedAtUtc
                     ELSE tp.UpdatedAtUtc END AS UpdatedAtUtc
            FROM planning.AdventurePlans AS ap
            INNER JOIN planning.TravelerParticipations AS tp
                ON tp.CreatorId = ap.CreatorId
                AND tp.AdventurePlanId = ap.AdventurePlanId
                AND tp.UserId = @UserId
                AND tp.Status = 'Accepted'
                AND tp.EffectiveFromUtc <= @EvaluatedAtUtc
                AND (tp.ExpiresAtUtc IS NULL OR tp.ExpiresAtUtc > @EvaluatedAtUtc)
            INNER JOIN auth.CreatorMemberships AS cm
                ON cm.CreatorId = ap.CreatorId COLLATE Latin1_General_100_BIN2
                AND cm.UserId = @UserId
                AND cm.Status = 'Active'
                AND cm.Version = @MembershipVersion
                AND cm.EffectiveFromUtc <= @EvaluatedAtUtc
                AND (cm.ExpiresAtUtc IS NULL OR cm.ExpiresAtUtc > @EvaluatedAtUtc)
            OUTER APPLY
            (
                SELECT TOP (1) dv.TimeZone
                FROM planning.DestinationVisits AS dv
                WHERE dv.CreatorId = ap.CreatorId
                  AND dv.AdventurePlanId = ap.AdventurePlanId
                ORDER BY dv.Sequence, dv.DestinationVisitId
            ) AS firstVisit
            WHERE ap.CreatorId = @CreatorId
              AND ap.PlanningStatus IN ('Planned', 'Upcoming', 'InProgress', 'Completed')
              AND (@IncludeCompleted = 1 OR ap.PlanningStatus <> 'Completed')
            ORDER BY ap.StartDate, ap.AdventurePlanId;
            """;

        await using var connection = new SqlConnection(connectionString);
        var rows = await connection.QueryAsync<AdventureSummaryRow>(new CommandDefinition(
            sql, Parameters(scope, maximumResults, includeCompleted), cancellationToken: cancellationToken));
        return rows.Select(MapSummary).ToArray();
    }

    /// <inheritdoc />
    public async Task<CompanionAdventureDetailProjection?> GetAsync(
        CompanionAdventureReadScope scope,
        string adventureId,
        CancellationToken cancellationToken = default)
    {
        Validate(scope, 1);
        if (string.IsNullOrWhiteSpace(adventureId) || adventureId.Length > 64)
            throw new ArgumentException("A bounded Adventure identity is required.", nameof(adventureId));

        const string adventureSql = """
            SELECT TOP (1)
                ap.AdventurePlanId, tp.TravelerId, ap.Title, ap.WorkingDescription,
                ap.PlanningStatus, ap.StartDate, ap.EndDate,
                COALESCE(firstVisit.TimeZone, 'Etc/UTC') AS PrimaryTimeZone,
                ap.Version AS PlanVersion, tp.Version AS ParticipationVersion,
                CASE WHEN ap.UpdatedAtUtc >= tp.UpdatedAtUtc THEN ap.UpdatedAtUtc
                     ELSE tp.UpdatedAtUtc END AS UpdatedAtUtc
            FROM planning.AdventurePlans AS ap
            INNER JOIN planning.TravelerParticipations AS tp
                ON tp.CreatorId = ap.CreatorId
                AND tp.AdventurePlanId = ap.AdventurePlanId
                AND tp.UserId = @UserId
                AND tp.Status = 'Accepted'
                AND tp.EffectiveFromUtc <= @EvaluatedAtUtc
                AND (tp.ExpiresAtUtc IS NULL OR tp.ExpiresAtUtc > @EvaluatedAtUtc)
            INNER JOIN auth.CreatorMemberships AS cm
                ON cm.CreatorId = ap.CreatorId COLLATE Latin1_General_100_BIN2
                AND cm.UserId = @UserId
                AND cm.Status = 'Active'
                AND cm.Version = @MembershipVersion
                AND cm.EffectiveFromUtc <= @EvaluatedAtUtc
                AND (cm.ExpiresAtUtc IS NULL OR cm.ExpiresAtUtc > @EvaluatedAtUtc)
            OUTER APPLY
            (
                SELECT TOP (1) dv.TimeZone
                FROM planning.DestinationVisits AS dv
                WHERE dv.CreatorId = ap.CreatorId
                  AND dv.AdventurePlanId = ap.AdventurePlanId
                ORDER BY dv.Sequence, dv.DestinationVisitId
            ) AS firstVisit
            WHERE ap.CreatorId = @CreatorId
              AND ap.AdventurePlanId = @AdventureId
              AND ap.PlanningStatus IN ('Planned', 'Upcoming', 'InProgress', 'Completed');
            """;

        const string destinationsSql = """
            SELECT dv.DestinationVisitId, dv.Name, dv.StartDate, dv.EndDate,
                   dv.TimeZone, dv.Sequence
            FROM planning.DestinationVisits AS dv
            INNER JOIN planning.AdventurePlans AS ap
                ON ap.CreatorId = dv.CreatorId
                AND ap.AdventurePlanId = dv.AdventurePlanId
            INNER JOIN planning.TravelerParticipations AS tp
                ON tp.CreatorId = ap.CreatorId
                AND tp.AdventurePlanId = ap.AdventurePlanId
                AND tp.UserId = @UserId
                AND tp.Status = 'Accepted'
                AND tp.EffectiveFromUtc <= @EvaluatedAtUtc
                AND (tp.ExpiresAtUtc IS NULL OR tp.ExpiresAtUtc > @EvaluatedAtUtc)
            INNER JOIN auth.CreatorMemberships AS cm
                ON cm.CreatorId = ap.CreatorId COLLATE Latin1_General_100_BIN2
                AND cm.UserId = @UserId
                AND cm.Status = 'Active'
                AND cm.Version = @MembershipVersion
                AND cm.EffectiveFromUtc <= @EvaluatedAtUtc
                AND (cm.ExpiresAtUtc IS NULL OR cm.ExpiresAtUtc > @EvaluatedAtUtc)
            WHERE ap.CreatorId = @CreatorId
              AND ap.AdventurePlanId = @AdventureId
              AND ap.PlanningStatus IN ('Planned', 'Upcoming', 'InProgress', 'Completed')
            ORDER BY dv.Sequence, dv.DestinationVisitId;
            """;

        var parameters = Parameters(scope, 1, false, adventureId);
        await using var connection = new SqlConnection(connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<AdventureDetailRow>(new CommandDefinition(
            adventureSql, parameters, cancellationToken: cancellationToken));
        if (row is null)
            return null;

        var destinations = await connection.QueryAsync<DestinationRow>(new CommandDefinition(
            destinationsSql, parameters, cancellationToken: cancellationToken));
        return new CompanionAdventureDetailProjection(
            MapSummary(row), row.WorkingDescription,
            destinations.Select(MapDestination).ToArray());
    }

    private static object Parameters(
        CompanionAdventureReadScope scope, int maximumResults, bool includeCompleted,
        string? adventureId = null) => new
        {
            CreatorId = scope.CreatorId.Value,
            UserId = scope.UserId.Value,
            scope.MembershipVersion,
            scope.EvaluatedAtUtc,
            MaximumResults = maximumResults,
            IncludeCompleted = includeCompleted,
            AdventureId = adventureId
        };

    private static void Validate(CompanionAdventureReadScope scope, int maximumResults)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.CreatorId == default || scope.UserId == default || scope.MembershipVersion < 1)
            throw new ArgumentException("A complete current authorization scope is required.", nameof(scope));
        if (scope.EvaluatedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Authorization evaluation time must be UTC.", nameof(scope));
        if (maximumResults is < 1 or > CompanionReadProjectionLimits.MaximumAdventures)
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
    }

    private static CompanionAdventureSummaryProjection MapSummary(AdventureSummaryRow row) => new(
        row.AdventurePlanId, row.TravelerId, row.Title, MapLifecycle(row.PlanningStatus),
        DateOnly.FromDateTime(row.StartDate), DateOnly.FromDateTime(row.EndDate),
        row.PrimaryTimeZone, row.PlanVersion, row.ParticipationVersion, row.UpdatedAtUtc);

    private static CompanionDestinationProjection MapDestination(DestinationRow row) => new(
        row.DestinationVisitId, row.Name, DateOnly.FromDateTime(row.StartDate),
        DateOnly.FromDateTime(row.EndDate), row.TimeZone, row.Sequence);

    private static CompanionAdventureLifecycle MapLifecycle(string status) => status switch
    {
        "Planned" => CompanionAdventureLifecycle.Planned,
        "Upcoming" => CompanionAdventureLifecycle.Committed,
        "InProgress" => CompanionAdventureLifecycle.InProgress,
        "Completed" => CompanionAdventureLifecycle.Completed,
        _ => throw new InvalidDataException("SQL returned an unsupported Adventure lifecycle.")
    };
}

internal class AdventureSummaryRow
{
    public required string AdventurePlanId { get; init; }
    public required string TravelerId { get; init; }
    public required string Title { get; init; }
    public required string PlanningStatus { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public required string PrimaryTimeZone { get; init; }
    public long PlanVersion { get; init; }
    public long ParticipationVersion { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

internal sealed class AdventureDetailRow : AdventureSummaryRow
{
    public string? WorkingDescription { get; init; }
}

internal sealed class DestinationRow
{
    public required string DestinationVisitId { get; init; }
    public required string Name { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public required string TimeZone { get; init; }
    public int Sequence { get; init; }
}
