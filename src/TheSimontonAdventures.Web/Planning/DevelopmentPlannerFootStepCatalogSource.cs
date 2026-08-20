using System.Text.Json;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Loads fictional, environment-isolated FootSteps for authenticated local development.</summary>
public sealed class DevelopmentPlannerFootStepCatalogSource : IPlannerFootStepCatalogSource, IPlannerFootStepUseResolver
{
    private readonly IReadOnlyList<PlannerFootStepDefinition> items;

    /// <summary>Loads the reviewed fictional Development catalog from application content.</summary>
    public DevelopmentPlannerFootStepCatalogSource(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var path = Path.Combine(environment.ContentRootPath, "Content", "PlannerFootSteps", "development.json");
        var records = JsonSerializer.Deserialize<IReadOnlyList<Record>>(
            File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        items = records.Select(ToDefinition).ToArray();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PlannerFootStepDefinition>> ListAsync(
        CreatorId customerCreatorId,
        string requestedLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(items);

    /// <inheritdoc />
    public Task<AuthorizedPlannerFootStepUse?> ResolveAsync(
        AdventuresSuite.Identity.ActorIdentity actor,
        CreatorId customerCreatorId,
        string footStepId,
        string version,
        CancellationToken cancellationToken = default)
    {
        var item = actor is { IsHuman: true }
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
        Kind = source.Kind,
        Title = source.Title,
        Summary = source.Summary,
        Attribution = source.Attribution,
        Freshness = source.Freshness,
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
            : new(source.DestinationDraft.Name, source.DestinationDraft.TimeZoneId)
    };

    private static IReadOnlySet<string> Set(IEnumerable<string> values) =>
        values.ToHashSet(StringComparer.Ordinal);

    private sealed record Record
    {
        public required string Id { get; init; }
        public required string Version { get; init; }
        public required string Kind { get; init; }
        public required string Title { get; init; }
        public required string Summary { get; init; }
        public required string Attribution { get; init; }
        public required string Freshness { get; init; }
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
    }

    private sealed record DestinationDraftRecord
    {
        public required string Name { get; init; }
        public required string TimeZoneId { get; init; }
    }
}
