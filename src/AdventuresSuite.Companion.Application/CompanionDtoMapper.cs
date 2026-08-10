using AdventuresSuite.Companion.Contracts;

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

    internal static CompanionTodayDto MapToday(
        AdventureFixture source, DateTimeOffset now, string supportId)
    {
        var items = source.Items.Where(value => value.Date == new DateOnly(2026, 8, 10))
            .OrderBy(value => value.Sequence).Select(MapScheduleItem).ToArray();
        var next = source.Items.Where(value => value.Date > new DateOnly(2026, 8, 10))
            .OrderBy(value => value.Date).ThenBy(value => value.Sequence).FirstOrDefault();
        return new CompanionTodayDto
        {
            SchemaVersion = "1.0",
            ProjectionVersion = "pv_today_italy_01",
            GeneratedAtUtc = now,
            FreshUntilUtc = now.AddMinutes(5),
            SupportId = supportId,
            AdventureId = source.Id,
            LocalDate = new(2026, 8, 10),
            TimeZone = source.TimeZone,
            State = CompanionTodayState.Active,
            TodayItems = items,
            NextItem = next is null ? null : MapScheduleItem(next),
            Notice = "Times are shown in the Adventure's local time zone."
        };
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
