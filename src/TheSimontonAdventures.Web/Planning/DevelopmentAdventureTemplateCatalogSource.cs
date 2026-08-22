using System.Text.Json;
using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Loads the reviewed, deterministic development-preview template collection from JSON.</summary>
public sealed class DevelopmentAdventureTemplateCatalogSource : IAdventureTemplateCatalogSource
{
    private const string RelativeCatalogPath = "Content/PlannerTemplates/development.json";
    private readonly IReadOnlyList<AdventureTemplateBlueprint> templates;

    /// <summary>Loads and validates the development-only catalog from the application content root.</summary>
    public DevelopmentAdventureTemplateCatalogSource(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var path = Path.Combine(environment.ContentRootPath, RelativeCatalogPath);
        var json = File.ReadAllText(path);
        var records = JsonSerializer.Deserialize<TemplateRecord[]>(json, JsonOptions)
            ?? throw new InvalidOperationException("The development Adventure Template catalog is unavailable.");
        templates = records.Select(ToBlueprint).ToArray();
        if (templates.Count == 0
            || templates.Select(item => item.VersionId).Distinct().Count() != templates.Count)
        {
            throw new InvalidOperationException("The development Adventure Template catalog is not unique.");
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AdventureTemplateBlueprint>> ListAsync(
        CreatorId customerCreatorId,
        string requestedLocale,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AdventureTemplateBlueprint> result = customerCreatorId == default
            ? []
            : templates.Where(item => string.Equals(
                item.SourceLocale, requestedLocale, StringComparison.OrdinalIgnoreCase)).ToArray();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<AuthorizedAdventureTemplateUse?> ResolveUseAsync(
        ActorIdentity actor,
        CreatorId customerCreatorId,
        AdventureTemplateVersionId templateVersion,
        string requestedLocale,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (actor is null || !actor.IsHuman || customerCreatorId == default)
        {
            return Task.FromResult<AuthorizedAdventureTemplateUse?>(null);
        }

        var template = templates.SingleOrDefault(item =>
            item.VersionId == templateVersion
            && string.Equals(item.SourceLocale, requestedLocale, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(template is null
            ? null
            : new AuthorizedAdventureTemplateUse(
                template,
                $"local-alpha:{template.VersionId.TemplateId}:{template.VersionId.Version}"));
    }

    private static AdventureTemplateBlueprint ToBlueprint(TemplateRecord source)
    {
        if (!Enum.TryParse<AdventureTemplateOwnerType>(source.OwnerType, false, out var ownerType))
        {
            throw new InvalidOperationException("A development template owner type is invalid.");
        }

        return new AdventureTemplateBlueprint
        {
            VersionId = new(source.TemplateId, source.Version),
            OwnerType = ownerType,
            OwnerId = source.OwnerId,
            SourceLocale = source.SourceLocale,
            Attribution = source.Attribution,
            Title = source.Title,
            WorkingDescription = source.WorkingDescription,
            DurationDays = source.DurationDays,
            Destinations = source.Destinations.Select(item => new AdventureTemplateDestination(
                item.Key, item.Name, item.StartDayOffset, item.EndDayOffset,
                new IanaTimeZone(item.TimeZone), item.Guidance, item.UsesConfiguredOrigin)).ToArray(),
            Days = source.Days.Select(item => new AdventureTemplateDay(
                item.Key, item.DayOffset, item.DestinationKey,
                new IanaTimeZone(item.TimeZone), item.Title)).ToArray(),
            Activities = source.Activities.Select(item => new AdventureTemplateActivity(
                item.DayKey, item.Title, item.StartsAtLocal, item.EndsAtLocal)).ToArray(),
            Transportation = source.Transportation.Select(item => new AdventureTemplateTransportation(
                item.Mode, item.From, item.To,
                item.DepartureDayOffset, item.DepartureTimeLocal,
                new IanaTimeZone(item.DepartureTimeZone),
                item.ArrivalDayOffset, item.ArrivalTimeLocal,
                new IanaTimeZone(item.ArrivalTimeZone),
                item.DepartureDestinationKey, item.ArrivalDestinationKey)).ToArray(),
            Accommodations = source.Accommodations.Select(item => new AdventureTemplateAccommodation(
                item.Name, item.StartDayOffset, item.EndDayOffset,
                new IanaTimeZone(item.TimeZone), item.DestinationKey)).ToArray()
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record TemplateRecord(
        string TemplateId,
        string Version,
        string OwnerType,
        string OwnerId,
        string SourceLocale,
        string Attribution,
        string Title,
        string? WorkingDescription,
        int DurationDays,
        DestinationRecord[] Destinations,
        DayRecord[] Days,
        ActivityRecord[] Activities,
        TransportationRecord[] Transportation,
        AccommodationRecord[] Accommodations);

    private sealed record DestinationRecord(
        string Key, string Name, int StartDayOffset, int EndDayOffset,
        string TimeZone, string? Guidance, bool UsesConfiguredOrigin = false);

    private sealed record DayRecord(
        string Key, int DayOffset, string? DestinationKey, string TimeZone, string Title);

    private sealed record ActivityRecord(
        string DayKey, string Title, TimeOnly? StartsAtLocal, TimeOnly? EndsAtLocal);

    private sealed record TransportationRecord(
        string Mode, string From, string To,
        int DepartureDayOffset, TimeOnly? DepartureTimeLocal, string DepartureTimeZone,
        int ArrivalDayOffset, TimeOnly? ArrivalTimeLocal, string ArrivalTimeZone,
        string? DepartureDestinationKey, string? ArrivalDestinationKey);

    private sealed record AccommodationRecord(
        string Name, int StartDayOffset, int EndDayOffset, string TimeZone,
        string? DestinationKey);
}
