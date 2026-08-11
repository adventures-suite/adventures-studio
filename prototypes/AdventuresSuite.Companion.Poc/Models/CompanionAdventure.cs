namespace AdventuresSuite.Companion.Poc.Models;

/// <summary>
/// Represents the minimized, read-only Adventure projection used by the POC.
/// </summary>
public sealed record CompanionAdventure(
    string Id,
    string Title,
    string Subtitle,
    string Status,
    string TravelDates,
    string? HeroImagePath,
    string? HeroAlternativeText,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<CompanionSegment> Segments)
{
    /// <summary>
    /// Gets whether the source content presents this Adventure as current.
    /// </summary>
    public bool IsCurrent => string.Equals(Status, "Current", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Represents one traveler-safe route segment in the POC projection.
/// </summary>
public sealed record CompanionSegment(
    string From,
    string To,
    string TravelMode,
    string TravelDescription,
    string ArrivalDate,
    string TimeZone,
    IReadOnlyList<string> Waypoints);

/// <summary>Identifies the explicitly configured Companion content provider.</summary>
public enum CompanionContentProviderKind
{
    /// <summary>Uses bundled fictional editorial JSON for the presentation demo.</summary>
    Demo,

    /// <summary>Uses the typed read-only Companion Adventure-list API client.</summary>
    Api
}
