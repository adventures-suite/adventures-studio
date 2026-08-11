using AdventuresSuite.Companion.Contracts;
using System.Text;

namespace AdventuresSuite.Companion.Application;

/// <summary>Maps fictional application projections to the explicit Companion DTO allowlist.</summary>
internal static class CompanionDtoMapper
{
    internal static CompanionAdventureSummaryDto MapSummary(AdventureFixture source, DateTimeOffset now) =>
        new()
        {
            AdventureId = source.Id,
            Title = source.Title,
            Subtitle = source.Subtitle,
            Status = source.Status,
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            PrimaryTimeZone = source.TimeZone,
            Countdown = MapCountdown(source, now),
            HeroResource = source.Id == "adv_demo_spain_2027" ? MapHeroResource() : null,
            OfflineState = source.OfflineState
        };

    internal static CompanionAdventureDto MapAdventure(
        AdventureFixture source, DateTimeOffset now, string supportId) =>
        new()
        {
            SchemaVersion = "1.0",
            ProjectionVersion = $"pv_detail_{source.Id}_01",
            GeneratedAtUtc = now,
            FreshUntilUtc = now.AddMinutes(15),
            SupportId = supportId,
            AdventureId = source.Id,
            Title = source.Title,
            Subtitle = source.Subtitle,
            Description = source.Description,
            Status = source.Status,
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            PrimaryTimeZone = source.TimeZone,
            Countdown = MapCountdown(source, now),
            Destinations = source.Destinations.OrderBy(value => value.Sequence).Select(MapDestination).ToArray(),
            NextItemSummary = source.Items.OrderBy(value => value.Sequence).FirstOrDefault()?.Title,
            ReadinessSummary = source.Id == DeterministicCompanionProjectionService.ItalyAdventureId
                ? "One traveler-visible action needs attention."
                : "No traveler-visible blockers are present in this fictional projection.",
            // Capabilities appear only when their independently authorized route is active.
            CapabilityLinks = new Dictionary<string, string>(StringComparer.Ordinal),
            InformationProfileVersion = "info_demo_01"
        };

    internal static bool TryMapToday(
        CompanionTodayProjection source,
        string requestedAdventureId,
        DateTimeOffset now,
        string supportId,
        out CompanionTodayDto? result)
    {
        result = null;
        if (!IsValidIdentity(requestedAdventureId)
            || !string.Equals(source.Adventure.AdventureId, requestedAdventureId, StringComparison.Ordinal)
            || !IsValidIdentity(source.Adventure.TravelerId)
            || !Enum.IsDefined(source.Adventure.Lifecycle)
            || source.Adventure.EndDate < source.Adventure.StartDate
            || !IsIanaTimeZone(source.Adventure.PrimaryTimeZone)
            || !IsIanaTimeZone(source.TimeZone)
            || source.TimeZone != source.Adventure.PrimaryTimeZone
            || source.Adventure.PlanVersion < 1
            || source.Adventure.ParticipationVersion < 1
            || source.Adventure.UpdatedAtUtc.Offset != TimeSpan.Zero
            || now.Offset != TimeSpan.Zero
            || !IsBounded(source.InformationProfileVersion, 64)
            || !IsOptionalBounded(source.Notice, 300)
            || !Enum.IsDefined(source.State)
            || source.TodayItems is null
            || source.TodayItems.Count > 250
            || !TryGetLocalDate(now, source.TimeZone, out var evaluatedLocalDate)
            || source.LocalDate != evaluatedLocalDate
            || ExpectedTodayState(source) != source.State
            || !TryMapScheduleItems(source.TodayItems, source.Adventure, source.LocalDate, out var items)
            || !TryMapNextItem(source.NextItem, source.Adventure, source.LocalDate, source.TodayItems, out var nextItem))
        {
            return false;
        }

        var projectionVersion = $"pv_today_{source.Adventure.PlanVersion}_{source.Adventure.ParticipationVersion}_{source.LocalDate:yyyyMMdd}";
        result = new CompanionTodayDto
        {
            SchemaVersion = "1.0",
            ProjectionVersion = projectionVersion,
            GeneratedAtUtc = now,
            FreshUntilUtc = now.AddMinutes(5),
            SupportId = supportId,
            AdventureId = source.Adventure.AdventureId,
            LocalDate = source.LocalDate,
            TimeZone = source.TimeZone,
            State = MapTodayState(source.State),
            TodayItems = items!,
            NextItem = nextItem,
            Notice = source.Notice
        };
        return true;
    }

    internal static CompanionItineraryDto MapItinerary(
        AdventureFixture source, DateTimeOffset now, string supportId)
    {
        var days = source.Items.GroupBy(value => value.Date).OrderBy(value => value.Key)
            .Select((group, index) => new CompanionItineraryDayDto
            {
                ItineraryDayId = $"day_{source.Id}_{index + 1}",
                LocalDate = group.Key,
                TimeZone = group.First().TimeZone,
                DayNumber = index + 1,
                Title = group.First().Place,
                DestinationVisitId = source.Destinations.FirstOrDefault(value =>
                    group.Key >= value.StartDate && group.Key <= value.EndDate)?.Id ?? source.Destinations.First().Id,
                DestinationName = source.Destinations.FirstOrDefault(value =>
                    group.Key >= value.StartDate && group.Key <= value.EndDate)?.Name ?? source.Destinations.First().Name,
                Items = group.OrderBy(value => value.Sequence).Select(MapScheduleItem).ToArray(),
                Summary = null,
                HasMaterialChange = group.Any(value => value.RequiresAcknowledgment),
                AcknowledgmentId = group.Any(value => value.RequiresAcknowledgment) ? $"ack_{group.Key:yyyyMMdd}" : null
            }).ToArray();
        return new CompanionItineraryDto
        {
            SchemaVersion = "1.0",
            ProjectionVersion = $"pv_itinerary_{source.Id}_01",
            GeneratedAtUtc = now,
            FreshUntilUtc = now.AddMinutes(15),
            SupportId = supportId,
            AdventureId = source.Id,
            Days = days
        };
    }

    internal static CompanionReadinessDto MapReadiness(
        AdventureFixture source, DateTimeOffset now, string supportId)
    {
        var attention = source.Id == DeterministicCompanionProjectionService.ItalyAdventureId;
        return new CompanionReadinessDto
        {
            SchemaVersion = "1.0",
            ProjectionVersion = $"pv_readiness_{source.Id}_01",
            GeneratedAtUtc = now,
            FreshUntilUtc = now.AddMinutes(5),
            SupportId = supportId,
            AdventureId = source.Id,
            OverallState = attention ? CompanionReadinessState.AttentionRequired : CompanionReadinessState.Ready,
            EvaluatedAtUtc = now,
            Categories =
            [
                new() { Category = CompanionReadinessCategory.Travel, State = CompanionReadinessState.Ready, Title = "Travel", TotalCount = 2, CompletedCount = 2 },
                new() { Category = CompanionReadinessCategory.Tasks, State = attention ? CompanionReadinessState.AttentionRequired : CompanionReadinessState.Ready, Title = "Tasks", TotalCount = 1, CompletedCount = attention ? 0 : 1 }
            ],
            Actions = attention
                ? [new CompanionReadinessActionDto { ActionId = "action_demo_ack_change", Category = CompanionReadinessCategory.Tasks, Title = "Review the changed activity", DueDate = new(2026, 8, 10), DueLocalTime = new(8, 30), TimeZone = "Europe/Rome", Urgency = 2, IsComplete = false, ActionPath = null }]
                : []
        };
    }

    internal static CompanionPlaybookDto MapPlaybook(
        AdventureFixture source, DateTimeOffset now, string supportId) =>
        new()
        {
            SchemaVersion = "1.0",
            ProjectionVersion = "pv_playbook_italy_01",
            GeneratedAtUtc = now,
            FreshUntilUtc = now.AddHours(1),
            SupportId = supportId,
            AdventureId = source.Id,
            PlaybookVersion = "playbook_demo_01",
            PlanVersion = "plan_demo_07",
            PlaybookGeneratedAtUtc = now.AddHours(-1),
            ExpiresAtUtc = now.AddDays(1),
            StaleState = CompanionPlaybookStaleState.Current,
            Sections =
            [
                new CompanionPlaybookSectionDto
                {
                    SectionId = "section_demo_overview",
                    SectionType = "overview",
                    Title = "Adventure overview",
                    Introduction = "A fictional guide for contract testing.",
                    Entries = [new CompanionPlaybookEntryDto { EntryId = "entry_demo_arrival", Title = "Arrival", Summary = "Use the approved local transportation plan." }]
                },
                new CompanionPlaybookSectionDto
                {
                    SectionId = "section_demo_contingencies",
                    SectionType = "contingencies",
                    Title = "Contingencies",
                    Introduction = null,
                    Entries = [new CompanionPlaybookEntryDto { EntryId = "entry_demo_help", Title = "If plans change", Summary = "Refresh the Companion projection when connectivity returns." }]
                }
            ],
            Resources = []
        };

    private static CompanionCountdownDto MapCountdown(AdventureFixture source, DateTimeOffset now) =>
        new()
        {
            TargetDate = source.StartDate,
            TargetLocalTime = null,
            TimeZone = source.TimeZone,
            EvaluatedAtUtc = now,
            State = source.Status switch
            {
                CompanionAdventureStatus.InProgress => CompanionCountdownState.InProgress,
                CompanionAdventureStatus.Completed => CompanionCountdownState.Complete,
                _ => CompanionCountdownState.Future
            }
        };

    private static CompanionDestinationSummaryDto MapDestination(DestinationFixture source) =>
        new()
        {
            DestinationVisitId = source.Id,
            Name = source.Name,
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            TimeZone = source.TimeZone,
            Sequence = source.Sequence,
            HeroResource = null
        };

    private static CompanionScheduleItemDto MapScheduleItem(ScheduleFixture source) =>
        new()
        {
            ItemId = source.Id,
            ItemType = source.Type,
            Title = source.Title,
            Summary = source.Summary,
            LocalDate = source.Date,
            StartLocalTime = source.StartTime,
            EndLocalTime = source.EndTime,
            TimeZone = source.TimeZone,
            TimeStatus = source.TimeStatus,
            OperationalStatus = source.OperationalStatus,
            PlaceSummary = source.Place,
            TransportationSummary = source.Transportation,
            Resources = [],
            RequiresAcknowledgment = source.RequiresAcknowledgment,
            ActionLabel = source.RequiresAcknowledgment ? "Review change" : null,
            ActionPath = null
        };

    private static bool TryMapScheduleItems(
        IReadOnlyList<CompanionScheduleItemProjection> sources,
        CompanionAdventureSummaryProjection adventure,
        DateOnly localDate,
        out IReadOnlyList<CompanionScheduleItemDto>? result)
    {
        result = null;
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<CompanionScheduleItemDto>(sources.Count);
        var previousSequence = 0;
        foreach (var source in sources)
        {
            if (source is null
                || source.LocalDate != localDate
                || source.Sequence <= previousSequence
                || !identities.Add(source.ItemId)
                || !TryMapScheduleItem(source, adventure, out var item))
            {
                return false;
            }

            previousSequence = source.Sequence;
            items.Add(item!);
        }

        result = items;
        return true;
    }

    private static bool TryMapNextItem(
        CompanionScheduleItemProjection? source,
        CompanionAdventureSummaryProjection adventure,
        DateOnly localDate,
        IReadOnlyList<CompanionScheduleItemProjection> todayItems,
        out CompanionScheduleItemDto? result)
    {
        result = null;
        if (source is null)
        {
            return true;
        }

        if (source.LocalDate < localDate
            || todayItems.Any(value => string.Equals(value.ItemId, source.ItemId, StringComparison.Ordinal)))
        {
            return false;
        }

        return TryMapScheduleItem(source, adventure, out result);
    }

    private static bool TryMapScheduleItem(
        CompanionScheduleItemProjection source,
        CompanionAdventureSummaryProjection adventure,
        out CompanionScheduleItemDto? result)
    {
        result = null;
        if (!IsValidIdentity(source.ItemId)
            || !IsBounded(source.ItemType, 64)
            || !IsBounded(source.Title, 200)
            || !IsOptionalBounded(source.Summary, 2000)
            || source.LocalDate < adventure.StartDate
            || source.LocalDate > adventure.EndDate
            || !IsIanaTimeZone(source.TimeZone)
            || !Enum.IsDefined(source.TimeState)
            || !Enum.IsDefined(source.OperationalState)
            || !IsOptionalBounded(source.PlaceSummary, 300)
            || !IsOptionalBounded(source.TransportationSummary, 300)
            || source.Sequence < 1
            || !HasConsistentTime(source)
            || source.OperationalState == CompanionScheduleOperationalState.Changed && !source.RequiresAcknowledgment
            || source.TimeState == CompanionScheduleTimeState.ToBeConfirmed
                && source.OperationalState is CompanionScheduleOperationalState.Reserved
                    or CompanionScheduleOperationalState.Confirmed
                    or CompanionScheduleOperationalState.Completed)
        {
            return false;
        }

        result = new CompanionScheduleItemDto
        {
            ItemId = source.ItemId,
            ItemType = source.ItemType,
            Title = source.Title,
            Summary = source.Summary,
            LocalDate = source.LocalDate,
            StartLocalTime = source.StartLocalTime,
            EndLocalTime = source.EndLocalTime,
            TimeZone = source.TimeZone,
            TimeStatus = MapTimeState(source.TimeState),
            OperationalStatus = MapOperationalState(source.OperationalState),
            PlaceSummary = source.PlaceSummary,
            TransportationSummary = source.TransportationSummary,
            Resources = [],
            RequiresAcknowledgment = source.RequiresAcknowledgment,
            ActionLabel = source.RequiresAcknowledgment ? "Review change" : null,
            ActionPath = null
        };
        return true;
    }

    private static bool HasConsistentTime(CompanionScheduleItemProjection source) => source.TimeState switch
    {
        CompanionScheduleTimeState.Scheduled => source.StartLocalTime is not null
            && (source.EndLocalTime is null || source.EndLocalTime >= source.StartLocalTime),
        CompanionScheduleTimeState.AllDay or CompanionScheduleTimeState.ToBeConfirmed =>
            source.StartLocalTime is null && source.EndLocalTime is null,
        CompanionScheduleTimeState.Cancelled =>
            source.StartLocalTime is null && source.EndLocalTime is null
            && source.OperationalState == CompanionScheduleOperationalState.Cancelled,
        _ => false
    } && (source.OperationalState != CompanionScheduleOperationalState.Cancelled
        || source.TimeState == CompanionScheduleTimeState.Cancelled);

    private static CompanionTodayProjectionState ExpectedTodayState(CompanionTodayProjection source) =>
        source.LocalDate < source.Adventure.StartDate
            ? CompanionTodayProjectionState.BeforeAdventure
            : source.LocalDate > source.Adventure.EndDate
                ? CompanionTodayProjectionState.AfterAdventure
                : source.TodayItems.Count == 0
                    ? CompanionTodayProjectionState.NoScheduledItems
                    : CompanionTodayProjectionState.Active;

    private static CompanionTodayState MapTodayState(CompanionTodayProjectionState state) => state switch
    {
        CompanionTodayProjectionState.BeforeAdventure => CompanionTodayState.BeforeAdventure,
        CompanionTodayProjectionState.Active => CompanionTodayState.Active,
        CompanionTodayProjectionState.AfterAdventure => CompanionTodayState.AfterAdventure,
        CompanionTodayProjectionState.NoScheduledItems => CompanionTodayState.NoScheduledItems,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private static CompanionTimeStatus MapTimeState(CompanionScheduleTimeState state) => state switch
    {
        CompanionScheduleTimeState.Scheduled => CompanionTimeStatus.Scheduled,
        CompanionScheduleTimeState.AllDay => CompanionTimeStatus.AllDay,
        CompanionScheduleTimeState.ToBeConfirmed => CompanionTimeStatus.ToBeConfirmed,
        CompanionScheduleTimeState.Cancelled => CompanionTimeStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private static CompanionOperationalStatus MapOperationalState(CompanionScheduleOperationalState state) => state switch
    {
        CompanionScheduleOperationalState.Proposed => CompanionOperationalStatus.Proposed,
        CompanionScheduleOperationalState.Reserved => CompanionOperationalStatus.Reserved,
        CompanionScheduleOperationalState.Confirmed => CompanionOperationalStatus.Confirmed,
        CompanionScheduleOperationalState.Changed => CompanionOperationalStatus.Changed,
        CompanionScheduleOperationalState.Cancelled => CompanionOperationalStatus.Cancelled,
        CompanionScheduleOperationalState.Completed => CompanionOperationalStatus.Completed,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private static bool TryGetLocalDate(DateTimeOffset utc, string timeZone, out DateOnly result)
    {
        result = default;
        try
        {
            var local = TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(timeZone));
            result = DateOnly.FromDateTime(local.DateTime);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool IsIanaTimeZone(string? value)
    {
        if (!IsBounded(value, 100)
            || !value!.Contains('/')
            || value.Contains('\\'))
        {
            return false;
        }

        return TryGetLocalDate(DateTimeOffset.UnixEpoch, value, out _);
    }

    private static bool IsValidIdentity(string? value) =>
        value is { Length: >= 1 and <= 128 }
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or ':' or '-');

    private static bool IsBounded(string? value, int maximumRunes) =>
        !string.IsNullOrWhiteSpace(value) && value.EnumerateRunes().Count() <= maximumRunes;

    private static bool IsOptionalBounded(string? value, int maximumRunes) =>
        value is null || value.EnumerateRunes().Count() <= maximumRunes;

    private static CompanionResourceSummaryDto MapHeroResource() =>
        new()
        {
            ResourceId = "res_demo_spain_hero",
            MediaType = "image/jpeg",
            ByteLength = 128000,
            Title = "Fictional Barcelona skyline",
            AlternativeText = "Illustrated skyline used only for a fictional API fixture.",
            Attribution = "AdventuresSuite fictional fixture",
            Availability = CompanionResourceAvailability.Available,
            OfflineEligible = false,
            RetainUntilUtc = null,
            ContentPath = null
        };
}
