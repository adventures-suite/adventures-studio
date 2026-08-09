using System.Text.Json;
using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Presentation;
using TheSimontonAdventures.Web.Resources;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the Destination date, time-zone, and lifecycle JSON contract.</summary>
public sealed class DestinationTemporalContractTests
{
    /// <summary>Ensures every temporal field deserializes with its intended type.</summary>
    [Fact]
    public void Deserialize_AllTemporalFields_PreservesValues()
    {
        const string json = """
            {
              "timeZone": "Europe/Madrid",
              "plannedArrivalDate": "2027-10-25",
              "plannedDepartureDate": "2027-10-29",
              "visitedFrom": "2028-01-02",
              "visitedTo": "2028-01-03",
              "createdAtUtc": "2026-08-07T18:30:00Z",
              "updatedAtUtc": "2026-08-08T18:30:00Z",
              "publishedAtUtc": "2026-08-09T18:30:00Z",
              "lastPublishedAtUtc": "2026-08-10T18:30:00Z"
            }
            """;

        var destination = JsonSerializer.Deserialize<Destination>(json);

        Assert.NotNull(destination);
        Assert.Equal("Europe/Madrid", destination.TimeZone);
        Assert.Equal(new DateOnly(2027, 10, 25), destination.PlannedArrivalDate);
        Assert.Equal(new DateOnly(2027, 10, 29), destination.PlannedDepartureDate);
        Assert.Equal(new DateOnly(2028, 1, 2), destination.VisitedFrom);
        Assert.Equal(new DateOnly(2028, 1, 3), destination.VisitedTo);
        Assert.Equal(TimeSpan.Zero, destination.CreatedAtUtc?.Offset);
        Assert.Equal(new DateTimeOffset(2026, 8, 10, 18, 30, 0, TimeSpan.Zero), destination.LastPublishedAtUtc);
    }

    /// <summary>Ensures serialization retains camelCase names and canonical formats.</summary>
    [Fact]
    public void Serialize_TemporalFields_RoundTripsCanonicalJson()
    {
        var destination = new Destination
        {
            HeroResourceId = new ResourceId("resource_test_hero"),
            HomepageResourceId = new ResourceId("resource_test_card"),
            TimeZone = "Europe/Madrid",
            PlannedArrivalDate = new DateOnly(2027, 10, 25),
            PlannedDepartureDate = new DateOnly(2027, 10, 29),
            VisitedFrom = new DateOnly(2028, 1, 2),
            VisitedTo = new DateOnly(2028, 1, 3),
            CreatedAtUtc = new DateTimeOffset(2026, 8, 7, 18, 30, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 8, 8, 18, 30, 0, TimeSpan.Zero),
            PublishedAtUtc = new DateTimeOffset(2026, 8, 9, 18, 30, 0, TimeSpan.Zero),
            LastPublishedAtUtc = new DateTimeOffset(2026, 8, 10, 18, 30, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize(destination);
        var roundTripped = JsonSerializer.Deserialize<Destination>(json);

        Assert.Contains("\"plannedArrivalDate\":\"2027-10-25\"", json);
        Assert.Contains("\"createdAtUtc\":\"2026-08-07T18:30:00Z\"", json);
        Assert.DoesNotContain("PlannedArrivalDate", json);
        Assert.Equal(destination.PlannedArrivalDate, roundTripped?.PlannedArrivalDate);
        Assert.Equal(destination.LastPublishedAtUtc, roundTripped?.LastPublishedAtUtc);
    }

    /// <summary>Ensures a single-day visit is formatted once in the Creator locale.</summary>
    [Fact]
    public void FormatVisitedDateRange_SingleDay_UsesCreatorLocale()
    {
        var destination = new Destination
        {
            VisitedFrom = new DateOnly(2026, 6, 30),
            VisitedTo = new DateOnly(2026, 6, 30)
        };

        var result = destination.FormatVisitedDateRange("en-US");

        Assert.Equal("6/30/2026", result);
    }

    /// <summary>Ensures a multi-day plan is formatted as a localized range.</summary>
    [Fact]
    public void FormatPlannedDateRange_MultipleDays_UsesCreatorLocale()
    {
        var destination = new Destination
        {
            PlannedArrivalDate = new DateOnly(2027, 10, 25),
            PlannedDepartureDate = new DateOnly(2027, 10, 29)
        };

        var result = destination.FormatPlannedDateRange("en-US");

        Assert.Equal("10/25/2027 – 10/29/2027", result);
    }
}
