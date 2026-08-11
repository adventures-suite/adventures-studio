using System.Globalization;
using System.Text.Json;
using AdventuresSuite.Companion.Poc.Models;

namespace AdventuresSuite.Companion.Poc.Services;

/// <summary>
/// Loads existing public JSON and transforms it into a minimized Companion POC
/// projection. Production mobile data will come from an authorized API and
/// encrypted offline store rather than bundled editorial files.
/// </summary>
public sealed class CompanionContentService : ICompanionContentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads the bundled Current Adventure and two Planned Adventures.
    /// </summary>
    public async Task<CompanionContentResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var resourceStream = await FileSystem.OpenAppPackageFileAsync("Data/resources.json");
        var catalog = await JsonSerializer.DeserializeAsync<ResourceCatalogDocument>(resourceStream, JsonOptions)
            ?? throw new InvalidOperationException("The Companion Resource catalog is missing.");

        var adventures = new[]
        {
            await LoadAdventureAsync("1", catalog),
            await LoadAdventureAsync("2", catalog),
            await LoadAdventureAsync("3", catalog)
        };

        return CompanionContentResult.Success(adventures
            .OrderBy(adventure => adventure.IsCurrent ? 0 : 1)
            .ThenBy(adventure => adventure.StartDate)
            .ToArray(), hasDetailedContent: true);
    }

    private static async Task<CompanionAdventure> LoadAdventureAsync(string id, ResourceCatalogDocument catalog)
    {
        await using var volumeStream = await FileSystem.OpenAppPackageFileAsync($"Data/volume-{id}.json");
        await using var journeyStream = await FileSystem.OpenAppPackageFileAsync($"Data/journey-{id}.json");

        var volume = await JsonSerializer.DeserializeAsync<VolumeDocument>(volumeStream, JsonOptions)
            ?? throw new InvalidOperationException("The Companion volume data is missing.");
        var journey = await JsonSerializer.DeserializeAsync<JourneyDocument>(journeyStream, JsonOptions)
            ?? throw new InvalidOperationException("The Companion journey data is missing.");
        var cover = catalog.Resources.SingleOrDefault(resource =>
            string.Equals(resource.Id, volume.CoverResourceId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"The cover Resource '{volume.CoverResourceId}' is missing.");

        return new CompanionAdventure(
            id,
            volume.Title,
            volume.Subtitle,
            volume.Status,
            volume.TravelDates,
            $"images/{Path.GetFileName(cover.StorageLocation)}",
            cover.AlternativeText,
            ParseDate(journey.StartDate),
            ParseDate(journey.EndDate),
            journey.Segments
                .OrderBy(segment => segment.DisplayOrder)
                .Select(segment => new CompanionSegment(
                    segment.From,
                    segment.To,
                    segment.TravelMode,
                    segment.TravelDescription,
                    segment.ArrivalDate,
                    segment.VisitSchedule?.TimeZone ?? "Local time varies",
                    (segment.Waypoints ?? []).OrderBy(waypoint => waypoint.DisplayOrder).Select(waypoint => waypoint.Title).ToArray()))
                .ToArray());
    }

    private static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "MMMM d, yyyy", CultureInfo.InvariantCulture);

    private sealed record VolumeDocument(
        string Title,
        string Subtitle,
        string Status,
        string TravelDates,
        string CoverResourceId);

    private sealed record ResourceCatalogDocument(IReadOnlyList<ResourceDocument> Resources);

    private sealed record ResourceDocument(
        string Id,
        string StorageLocation,
        string AlternativeText);

    private sealed record JourneyDocument(
        string StartDate,
        string EndDate,
        IReadOnlyList<SegmentDocument> Segments);

    private sealed record SegmentDocument(
        string From,
        string To,
        string TravelMode,
        string TravelDescription,
        string ArrivalDate,
        int DisplayOrder,
        VisitScheduleDocument? VisitSchedule,
        IReadOnlyList<WaypointDocument>? Waypoints);

    private sealed record VisitScheduleDocument(string TimeZone);

    private sealed record WaypointDocument(string Title, int DisplayOrder);
}
