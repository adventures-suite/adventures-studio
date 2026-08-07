using System.Text.Json;
using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Presentation;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies typed local Journey visit and cruise gangway schedules.</summary>
public sealed class JourneyVisitScheduleTests
{
    /// <summary>Ensures typed dates and local times deserialize from JSON.</summary>
    [Fact]
    public void Deserialize_CompletePortCall_PreservesLocalValues()
    {
        const string json = """
            {
              "timeZone": "America/St_Thomas",
              "plannedArrivalDate": "2027-05-20",
              "plannedArrivalTime": "07:00:00",
              "plannedGangwayDownTime": "08:00:00",
              "plannedGangwayUpTime": "17:00:00",
              "plannedDepartureDate": "2027-05-20",
              "plannedDepartureTime": "18:00:00"
            }
            """;

        var schedule = JsonSerializer.Deserialize<JourneyVisitSchedule>(json);

        Assert.NotNull(schedule);
        Assert.Equal(new DateOnly(2027, 5, 20), schedule.PlannedArrivalDate);
        Assert.Equal(new TimeOnly(7, 0), schedule.PlannedArrivalTime);
        Assert.Equal(new TimeOnly(8, 0), schedule.PlannedGangwayDownTime);
        Assert.Equal(new TimeOnly(17, 0), schedule.PlannedGangwayUpTime);
        Assert.Equal(new TimeOnly(18, 0), schedule.PlannedDepartureTime);
    }

    /// <summary>Ensures visit dates and gangway times use the Creator locale.</summary>
    [Fact]
    public void Format_PortCall_UsesCreatorLocale()
    {
        var schedule = new JourneyVisitSchedule
        {
            PlannedArrivalDate = new DateOnly(2027, 5, 20),
            PlannedDepartureDate = new DateOnly(2027, 5, 20),
            PlannedGangwayDownTime = new TimeOnly(8, 0),
            PlannedGangwayUpTime = new TimeOnly(17, 0)
        };

        Assert.Equal("5/20/2027", schedule.FormatDateRange("en-US"));
        Assert.Equal(
            "Gangway down 8:00\u202fAM · Gangway up 5:00\u202fPM",
            schedule.FormatGangwayWindow("en-US"));
    }

    /// <summary>Ensures unknown gangway times remain absent from presentation.</summary>
    [Fact]
    public void FormatGangwayWindow_UnknownTimes_ReturnsEmptyString()
    {
        var schedule = new JourneyVisitSchedule();

        Assert.Equal(string.Empty, schedule.FormatGangwayWindow("en-US"));
    }
}
