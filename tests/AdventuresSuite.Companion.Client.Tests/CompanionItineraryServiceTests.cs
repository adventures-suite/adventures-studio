using System.Net;
using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Client.Tests;

/// <summary>Exercises fail-closed mobile Itinerary mapping.</summary>
public sealed class CompanionItineraryServiceTests
{
    /// <summary>Ensures ordered valid local days are retained.</summary>
    [Fact]
    public async Task ValidItineraryPreservesWireOrder()
    {
        var result = await Service(Itinerary()).LoadAsync("adv_demo");
        Assert.Equal(CompanionItineraryResultState.Success, result.State);
        Assert.Equal([1, 2], result.Itinerary!.Days.Select(day => day.DayNumber));
    }

    /// <summary>Ensures identity, ordering, time-zone, enum, and change inconsistencies fail closed.</summary>
    [Fact]
    public async Task MalformedAndUnsupportedDataFailsClosed()
    {
        var valid = Itinerary();
        var malformed = new[]
        {
            valid with { AdventureId = "other" },
            valid with { Days = valid.Days.Reverse().ToArray() },
            valid with { Days = [valid.Days[0] with { TimeZone = "Not/AZone" }] },
            valid with { Days = [valid.Days[0] with { Items = [Item() with { OperationalStatus = (CompanionOperationalStatus)999 }] }] },
            valid with { Days = [valid.Days[0] with { HasMaterialChange = true, AcknowledgmentId = null }] }
        };
        foreach (var dto in malformed)
            Assert.Equal(CompanionItineraryResultState.MalformedOrUnsupported, (await Service(dto).LoadAsync("adv_demo")).State);
    }

    /// <summary>Ensures stale, unavailable, unauthorized, and enumeration-safe outcomes remain distinct.</summary>
    [Fact]
    public async Task ExpectedFailureStatesRemainDistinct()
    {
        var stale = Itinerary() with
        {
            GeneratedAtUtc = new DateTimeOffset(2026, 8, 10, 9, 50, 0, TimeSpan.Zero),
            FreshUntilUtc = new DateTimeOffset(2026, 8, 10, 9, 59, 0, TimeSpan.Zero)
        };
        Assert.Equal(CompanionItineraryResultState.Stale, (await Service(stale).LoadAsync("adv_demo")).State);
        Assert.Equal(CompanionItineraryResultState.NotFound, (await Throw(new CompanionItineraryApiException(HttpStatusCode.NotFound, null, null)).LoadAsync("adv_demo")).State);
        Assert.Equal(CompanionItineraryResultState.Unauthorized, (await Throw(new CompanionItineraryApiException(HttpStatusCode.Forbidden, null, null)).LoadAsync("adv_demo")).State);
        Assert.Equal(CompanionItineraryResultState.Unavailable, (await Throw(new HttpRequestException()).LoadAsync("adv_demo")).State);
    }

    private static CompanionItineraryService Service(CompanionItineraryDto dto) => new(new Stub(dto), new FixedTime());
    private static CompanionItineraryService Throw(Exception exception) => new(new Throwing(exception), new FixedTime());
    private static CompanionItineraryDto Itinerary() => new()
    {
        SchemaVersion = "1.0",
        ProjectionVersion = "pv_itinerary",
        GeneratedAtUtc = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero),
        FreshUntilUtc = new(2026, 8, 10, 10, 5, 0, TimeSpan.Zero),
        SupportId = "support_demo",
        AdventureId = "adv_demo",
        Days = [Day(1, new(2026, 8, 10)), Day(2, new(2026, 8, 11))]
    };
    private static CompanionItineraryDayDto Day(int number, DateOnly date) => new()
    {
        ItineraryDayId = $"day_{number}",
        LocalDate = date,
        TimeZone = "Europe/Rome",
        DayNumber = number,
        DestinationVisitId = $"visit_{number}",
        DestinationName = "Fictional Rome",
        Items = [Item() with { ItemId = $"item_{number}", LocalDate = date }],
        HasMaterialChange = false
    };
    private static CompanionScheduleItemDto Item() => new()
    {
        ItemId = "item_demo",
        ItemType = "activity",
        Title = "Fictional activity",
        LocalDate = new(2026, 8, 10),
        StartLocalTime = new(9, 0),
        EndLocalTime = new(10, 0),
        TimeZone = "Europe/Rome",
        TimeStatus = CompanionTimeStatus.Scheduled,
        OperationalStatus = CompanionOperationalStatus.Proposed,
        Resources = [],
        RequiresAcknowledgment = false
    };
    private sealed class Stub(CompanionItineraryDto dto) : ICompanionItineraryTransport
    { public Task<CompanionItineraryTransportResponse> GetAsync(string adventureId, CancellationToken cancellationToken = default) => Task.FromResult(new CompanionItineraryTransportResponse(dto, "\"safe\"", "support_demo")); }
    private sealed class Throwing(Exception exception) : ICompanionItineraryTransport
    { public Task<CompanionItineraryTransportResponse> GetAsync(string adventureId, CancellationToken cancellationToken = default) => Task.FromException<CompanionItineraryTransportResponse>(exception); }
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 8, 10, 10, 1, 0, TimeSpan.Zero); }
}
