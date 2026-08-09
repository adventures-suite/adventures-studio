using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Represents the local planned schedule for the destination reached by one
/// Journey segment, including optional cruise gangway operations.
/// </summary>
public sealed class JourneyVisitSchedule
{
    /// <summary>Gets the destination's IANA time-zone identifier.</summary>
    [JsonPropertyName("timeZone")]
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>Gets the planned local arrival date.</summary>
    [JsonPropertyName("plannedArrivalDate")]
    public DateOnly? PlannedArrivalDate { get; init; }

    /// <summary>Gets the optional planned local arrival time.</summary>
    [JsonPropertyName("plannedArrivalTime")]
    public TimeOnly? PlannedArrivalTime { get; init; }

    /// <summary>Gets the optional local time when guests may leave the ship.</summary>
    [JsonPropertyName("plannedGangwayDownTime")]
    public TimeOnly? PlannedGangwayDownTime { get; init; }

    /// <summary>Gets the optional local time when guests must return to the ship.</summary>
    [JsonPropertyName("plannedGangwayUpTime")]
    public TimeOnly? PlannedGangwayUpTime { get; init; }

    /// <summary>Gets the planned local departure date.</summary>
    [JsonPropertyName("plannedDepartureDate")]
    public DateOnly? PlannedDepartureDate { get; init; }

    /// <summary>Gets the optional planned local departure time.</summary>
    [JsonPropertyName("plannedDepartureTime")]
    public TimeOnly? PlannedDepartureTime { get; init; }
}
