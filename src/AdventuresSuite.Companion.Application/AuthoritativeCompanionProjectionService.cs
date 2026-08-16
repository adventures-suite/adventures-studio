using System.Security.Cryptography;
using System.Text;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Application;

/// <summary>Composes authoritative application projections into the Companion wire allowlist.</summary>
public sealed class AuthoritativeCompanionProjectionService(
    ICompanionAdventureSummaryQuery summaries,
    ICompanionAdventureDetailQuery details,
    ICompanionTodayQuery today,
    ICompanionItineraryQuery itinerary,
    TimeProvider timeProvider) : ICompanionProjectionService
{
    /// <inheritdoc />
    public async Task<CompanionQueryResult<CompanionAdventureCollectionDto>> ListAdventuresAsync(
        CompanionAccessContext access, int limit, string? continuationToken, bool includeCompleted,
        string supportId, CancellationToken cancellationToken)
    {
        if (!CanQuery(access) || continuationToken is not null || limit > CompanionReadProjectionLimits.MaximumAdventures)
            return Unavailable<CompanionAdventureCollectionDto>();

        var now = timeProvider.GetUtcNow();
        var values = await summaries.ListAsync(CreateScope(access, now), limit, includeCompleted, cancellationToken);
        var projectionVersion = CreateVersion(values.Select(value =>
            $"{value.AdventureId}:{value.PlanVersion}:{value.ParticipationVersion}:{value.UpdatedAtUtc:O}"));
        var dto = new CompanionAdventureCollectionDto
        {
            SchemaVersion = "1.0",
            ProjectionVersion = projectionVersion,
            GeneratedAtUtc = now,
            FreshUntilUtc = now.AddMinutes(5),
            SupportId = supportId,
            Adventures = values.Select(value => MapSummary(value, now)).ToArray(),
            ContinuationToken = null
        };
        return new(dto, projectionVersion);
    }

    /// <inheritdoc />
    public async Task<CompanionQueryResult<CompanionAdventureDto>> GetAdventureAsync(
        CompanionAccessContext access, string adventureId, string supportId,
        CancellationToken cancellationToken)
    {
        if (!CanQuery(access))
            return Unavailable<CompanionAdventureDto>();

        var now = timeProvider.GetUtcNow();
        var value = await details.GetAsync(CreateScope(access, now), adventureId, cancellationToken);
        if (value is null)
            return Unavailable<CompanionAdventureDto>();

        var projectionVersion = CreateVersion([
            value.Adventure.AdventureId,
            value.Adventure.PlanVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.Adventure.ParticipationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.Adventure.UpdatedAtUtc.ToString("O")]);
        var dto = new CompanionAdventureDto
        {
            SchemaVersion = "1.0",
            ProjectionVersion = projectionVersion,
            GeneratedAtUtc = now,
            FreshUntilUtc = now.AddMinutes(5),
            SupportId = supportId,
            AdventureId = value.Adventure.AdventureId,
            Title = value.Adventure.Title,
            Subtitle = null,
            Description = "Your authorized Adventure overview.",
            Status = MapLifecycle(value.Adventure.Lifecycle),
            StartDate = value.Adventure.StartDate,
            EndDate = value.Adventure.EndDate,
            PrimaryTimeZone = value.Adventure.PrimaryTimeZone,
            Countdown = MapCountdown(value.Adventure, now),
            Destinations = value.Destinations.Select(MapDestination).ToArray(),
            NextItemSummary = null,
            ReadinessSummary = "Readiness details are not included in this projection.",
            CapabilityLinks = new Dictionary<string, string>(StringComparer.Ordinal),
            InformationProfileVersion = projectionVersion
        };
        return new(dto, projectionVersion);
    }

    /// <inheritdoc />
    public async Task<CompanionQueryResult<CompanionTodayDto>> GetTodayAsync(
        CompanionAccessContext access,
        string adventureId,
        string supportId,
        CancellationToken cancellationToken)
    {
        if (!CanQuery(access))
            return Unavailable<CompanionTodayDto>();

        var now = timeProvider.GetUtcNow();
        var value = await today.GetAsync(
            new CompanionTodayReadScope(
                access.CreatorId,
                access.Actor.UserId!.Value,
                access.TravelerId,
                access.MembershipVersion,
                now),
            adventureId,
            cancellationToken);
        if (value is null
            || !CompanionDtoMapper.TryMapToday(
                value, access.CreatorId.Value, access.TravelerId, adventureId, now, supportId, out var dto))
        {
            return Unavailable<CompanionTodayDto>();
        }

        return new(dto, dto!.ProjectionVersion);
    }

    /// <inheritdoc />
    public async Task<CompanionQueryResult<CompanionItineraryDto>> GetItineraryAsync(
        CompanionAccessContext access,
        string adventureId,
        string supportId,
        CancellationToken cancellationToken)
    {
        if (!CanQuery(access))
            return Unavailable<CompanionItineraryDto>();

        var now = timeProvider.GetUtcNow();
        var value = await itinerary.GetAsync(
            new CompanionItineraryReadScope(
                access.CreatorId,
                access.Actor.UserId!.Value,
                access.TravelerId,
                access.MembershipVersion,
                now),
            adventureId,
            cancellationToken);
        if (value is null
            || !CompanionDtoMapper.TryMapItinerary(
                value, access.CreatorId.Value, access.TravelerId, adventureId, now, supportId, out var dto))
        {
            return Unavailable<CompanionItineraryDto>();
        }

        return new(dto, dto!.ProjectionVersion);
    }

    private static bool CanQuery(CompanionAccessContext access) =>
        !access.IsRevoked
        && access.Actor.UserId.HasValue
        && access.MembershipVersion > 0
        && access.Scopes.Contains(DeterministicCompanionProjectionService.RequiredScope);

    private static CompanionAdventureReadScope CreateScope(
        CompanionAccessContext access, DateTimeOffset now) =>
        new(access.CreatorId, access.Actor.UserId!.Value, access.MembershipVersion, now);

    private static CompanionAdventureSummaryDto MapSummary(
        CompanionAdventureSummaryProjection value, DateTimeOffset now) => new()
        {
            AdventureId = value.AdventureId,
            Title = value.Title,
            Subtitle = null,
            Status = MapLifecycle(value.Lifecycle),
            StartDate = value.StartDate,
            EndDate = value.EndDate,
            PrimaryTimeZone = value.PrimaryTimeZone,
            Countdown = MapCountdown(value, now),
            HeroResource = null,
            OfflineState = CompanionOfflineState.NotAvailable
        };

    private static CompanionDestinationSummaryDto MapDestination(CompanionDestinationProjection value) => new()
    {
        DestinationVisitId = value.DestinationVisitId,
        Name = value.Name,
        StartDate = value.StartDate,
        EndDate = value.EndDate,
        TimeZone = value.TimeZone,
        Sequence = value.Sequence,
        HeroResource = null
    };

    private static CompanionCountdownDto MapCountdown(
        CompanionAdventureSummaryProjection value, DateTimeOffset now) => new()
        {
            TargetDate = value.StartDate,
            TargetLocalTime = null,
            TimeZone = value.PrimaryTimeZone,
            EvaluatedAtUtc = now,
            State = value.Lifecycle switch
            {
                CompanionAdventureLifecycle.InProgress => CompanionCountdownState.InProgress,
                CompanionAdventureLifecycle.Completed => CompanionCountdownState.Complete,
                _ => CompanionCountdownState.Future
            }
        };

    private static CompanionAdventureStatus MapLifecycle(CompanionAdventureLifecycle value) => value switch
    {
        CompanionAdventureLifecycle.Planned => CompanionAdventureStatus.Planned,
        CompanionAdventureLifecycle.Committed => CompanionAdventureStatus.Committed,
        CompanionAdventureLifecycle.InProgress => CompanionAdventureStatus.InProgress,
        CompanionAdventureLifecycle.Completed => CompanionAdventureStatus.Completed,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string CreateVersion(IEnumerable<string> values)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values)));
        return $"pv_{Convert.ToHexStringLower(bytes)[..32]}";
    }

    private static CompanionQueryResult<T> Unavailable<T>() where T : CompanionProjectionDto => new(null, null);
}
