using System.Text.Json;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Loads reviewed, environment-isolated FootSteps for authenticated development previews.</summary>
public sealed class DevelopmentPlannerFootStepCatalogSource : IPlannerFootStepCatalogSource, IPlannerFootStepUseResolver
{
    private readonly IReadOnlyList<PlannerFootStepDefinition> items;

    /// <summary>Loads and validates the reviewed Development catalogs from application content.</summary>
    public DevelopmentPlannerFootStepCatalogSource(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var directory = Path.Combine(environment.ContentRootPath, "Content", "PlannerFootSteps");
        var records = new[]
        {
            "development.json", "real-world.json", "us-motorcycle.json", "us-motorcycle-journeys.json",
            "us-national-parks-rv.json"
        }
            .SelectMany(fileName => Deserialize(Path.Combine(directory, fileName)))
            .ToArray();
        Validate(records);
        items = records.Select(ToDefinition).ToArray();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PlannerFootStepDefinition>> ListAsync(
        CreatorId customerCreatorId,
        string requestedLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(customerCreatorId == default || string.IsNullOrWhiteSpace(requestedLocale)
            ? (IReadOnlyList<PlannerFootStepDefinition>)[]
            : items);

    /// <inheritdoc />
    public Task<AuthorizedPlannerFootStepUse?> ResolveAsync(
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId customerCreatorId,
        string footStepId,
        string version,
        CancellationToken cancellationToken = default)
    {
        var item = actor is { IsHuman: true } && customerCreatorId != default
            ? items.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, footStepId, StringComparison.Ordinal)
                && string.Equals(candidate.Version, version, StringComparison.Ordinal))
            : null;
        return Task.FromResult(item?.DestinationDraft is null
            ? null
            : new AuthorizedPlannerFootStepUse(item, $"development:{item.Id}:{item.Version}"));
    }

    private static PlannerFootStepDefinition ToDefinition(Record source) => new()
    {
        Id = source.Id,
        Version = source.Version,
        OwnerCreatorId = new(source.OwnerCreatorId),
        Kind = source.Kind,
        Title = source.Title,
        Summary = source.Summary,
        Attribution = source.Attribution,
        Freshness = source.Freshness,
        Sources = source.Sources.Select(item => new PlannerFootStepSourceEvidence(
            item.Owner,
            new Uri(item.Url, UriKind.Absolute),
            DateOnly.ParseExact(item.RetrievedOn, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            DateOnly.ParseExact(item.ReviewedOn, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            DateOnly.ParseExact(item.ReviewAfter, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))).ToArray(),
        ContextKinds = source.ContextKinds.Select(value => Enum.Parse<PlannerFootStepContextKind>(value, false)).ToHashSet(),
        Places = Set(source.Places),
        TransportationModes = Set(source.TransportationModes),
        Categories = Set(source.Categories),
        RouteStyles = Set(source.RouteStyles),
        Surfaces = Set(source.Surfaces),
        Accessibility = Set(source.Accessibility),
        Paces = Set(source.Paces),
        Seasons = Set(source.Seasons),
        EquipmentNeeds = Set(source.EquipmentNeeds),
        BudgetBands = Set(source.BudgetBands),
        TravelerCompositions = Set(source.TravelerCompositions),
        SourceClasses = Set(source.SourceClasses),
        Languages = Set(source.Languages),
        DurationDays = source.DurationDays,
        DestinationDraft = source.DestinationDraft is null
            ? null
            : new(source.DestinationDraft.Name, source.DestinationDraft.TimeZoneId),
        ActivityDraft = source.ActivityDraft is null
            ? null
            : new(source.ActivityDraft.Title, ParseTime(source.ActivityDraft.SuggestedStartTime),
                ParseTime(source.ActivityDraft.SuggestedEndTime))
    };

    private static IReadOnlyList<Record> Deserialize(string path) =>
        JsonSerializer.Deserialize<IReadOnlyList<Record>>(
            File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

    private static void Validate(IReadOnlyList<Record> records)
    {
        var duplicate = records.GroupBy(item => (item.Id, item.Version))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Duplicate FootStep identity and version: {duplicate.Key.Id} {duplicate.Key.Version}.");
        }

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Id) || string.IsNullOrWhiteSpace(record.Version)
                || string.IsNullOrWhiteSpace(record.OwnerCreatorId) || string.IsNullOrWhiteSpace(record.Kind)
                || string.IsNullOrWhiteSpace(record.Title) || string.IsNullOrWhiteSpace(record.Summary)
                || string.IsNullOrWhiteSpace(record.Attribution) || string.IsNullOrWhiteSpace(record.Freshness)
                || record.ContextKinds.Count == 0
                || (record.SourceClasses.Contains("real-world-curated", StringComparer.Ordinal)
                    && record.Sources.Count == 0))
            {
                throw new InvalidDataException($"FootStep '{record.Id}' is missing required catalog metadata.");
            }

            _ = new CreatorId(record.OwnerCreatorId);
            foreach (var source in record.Sources)
            {
                if (string.IsNullOrWhiteSpace(source.Owner)
                    || !Uri.TryCreate(source.Url, UriKind.Absolute, out var uri)
                    || uri.Scheme != Uri.UriSchemeHttps
                    || !DateOnly.TryParseExact(source.RetrievedOn, "yyyy-MM-dd", out var retrievedOn)
                    || !DateOnly.TryParseExact(source.ReviewedOn, "yyyy-MM-dd", out var reviewedOn)
                    || !DateOnly.TryParseExact(source.ReviewAfter, "yyyy-MM-dd", out var reviewAfter)
                    || reviewedOn < retrievedOn || reviewAfter <= reviewedOn)
                {
                    throw new InvalidDataException($"FootStep '{record.Id}' has invalid source or freshness evidence.");
                }
            }
        }
    }

    private static TimeOnly? ParseTime(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : TimeOnly.ParseExact(value, "HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static IReadOnlySet<string> Set(IEnumerable<string> values) =>
        values.ToHashSet(StringComparer.Ordinal);

    private sealed record Record
    {
        public required string Id { get; init; }
        public required string Version { get; init; }
        public string OwnerCreatorId { get; init; } = "creator_tsa_01";
        public required string Kind { get; init; }
        public required string Title { get; init; }
        public required string Summary { get; init; }
        public required string Attribution { get; init; }
        public required string Freshness { get; init; }
        public IReadOnlyList<SourceRecord> Sources { get; init; } = [];
        public IReadOnlyList<string> ContextKinds { get; init; } = [];
        public IReadOnlyList<string> Places { get; init; } = [];
        public IReadOnlyList<string> TransportationModes { get; init; } = [];
        public IReadOnlyList<string> Categories { get; init; } = [];
        public IReadOnlyList<string> RouteStyles { get; init; } = [];
        public IReadOnlyList<string> Surfaces { get; init; } = [];
        public IReadOnlyList<string> Accessibility { get; init; } = [];
        public IReadOnlyList<string> Paces { get; init; } = [];
        public IReadOnlyList<string> Seasons { get; init; } = [];
        public IReadOnlyList<string> EquipmentNeeds { get; init; } = [];
        public IReadOnlyList<string> BudgetBands { get; init; } = [];
        public IReadOnlyList<string> TravelerCompositions { get; init; } = [];
        public IReadOnlyList<string> SourceClasses { get; init; } = [];
        public IReadOnlyList<string> Languages { get; init; } = [];
        public int? DurationDays { get; init; }
        public DestinationDraftRecord? DestinationDraft { get; init; }
        public ActivityDraftRecord? ActivityDraft { get; init; }
    }

    private sealed record SourceRecord
    {
        public required string Owner { get; init; }
        public required string Url { get; init; }
        public required string RetrievedOn { get; init; }
        public required string ReviewedOn { get; init; }
        public required string ReviewAfter { get; init; }
    }

    private sealed record DestinationDraftRecord
    {
        public required string Name { get; init; }
        public required string TimeZoneId { get; init; }
    }

    private sealed record ActivityDraftRecord
    {
        public required string Title { get; init; }
        public string? SuggestedStartTime { get; init; }
        public string? SuggestedEndTime { get; init; }
    }
}
