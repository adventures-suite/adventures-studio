using System.Text.Json;

namespace TheSimontonAdventures.Web.Showcase;

internal interface IShowcaseAdventureService
{
    Task<ShowcaseAdventure> GetAsync(CancellationToken cancellationToken = default);
}

internal sealed class JsonShowcaseAdventureService(IHostEnvironment hostEnvironment)
    : IShowcaseAdventureService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _path = Path.Combine(
        hostEnvironment.ContentRootPath,
        "Showcase",
        "Fixtures",
        "adventure.json");

    public async Task<ShowcaseAdventure> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(_path);
        var adventure = await JsonSerializer.DeserializeAsync<ShowcaseAdventure>(
            stream,
            SerializerOptions,
            cancellationToken) ?? throw new InvalidOperationException(
                "The development showcase fixture is empty.");

        adventure.Validate();
        return adventure;
    }
}

internal sealed record ShowcaseAdventure
{
    public string Title { get; init; } = string.Empty;
    public string Eyebrow { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Dates { get; init; } = string.Empty;
    public string Countdown { get; init; } = string.Empty;
    public string HeroImage { get; init; } = string.Empty;
    public string HeroAlt { get; init; } = string.Empty;
    public IReadOnlyList<ShowcaseStat> Stats { get; init; } = [];
    public IReadOnlyList<ShowcaseDestination> Destinations { get; init; } = [];
    public IReadOnlyList<ShowcaseDay> Days { get; init; } = [];
    public IReadOnlyList<ShowcaseTraveler> Travelers { get; init; } = [];
    public IReadOnlyList<ShowcaseReadinessItem> Readiness { get; init; } = [];
    public IReadOnlyList<ShowcaseRecommendation> Recommendations { get; init; } = [];

    public void Validate()
    {
        Require(Title, nameof(Title));
        Require(Eyebrow, nameof(Eyebrow));
        Require(Summary, nameof(Summary));
        Require(Status, nameof(Status));
        Require(Dates, nameof(Dates));
        Require(Countdown, nameof(Countdown));
        RequireLocalImage(HeroImage, nameof(HeroImage));
        Require(HeroAlt, nameof(HeroAlt));
        if (Stats.Count < 3 || Destinations.Count < 3 || Days.Count < 5
            || Travelers.Count < 2 || Readiness.Count < 3)
        {
            throw new InvalidOperationException(
                "The development showcase fixture must remain complete enough to tell the product story.");
        }

        foreach (var destination in Destinations)
        {
            Require(destination.Name, nameof(destination.Name));
            Require(destination.Country, nameof(destination.Country));
            Require(destination.Dates, nameof(destination.Dates));
            RequireLocalImage(destination.Image, nameof(destination.Image));
            Require(destination.ImageAlt, nameof(destination.ImageAlt));
        }

        foreach (var day in Days)
        {
            if (day.Day < 1)
            {
                throw new InvalidOperationException("Showcase itinerary days must be positive.");
            }

            Require(day.Date, nameof(day.Date));
            Require(day.Title, nameof(day.Title));
            Require(day.Location, nameof(day.Location));
            Require(day.Summary, nameof(day.Summary));
        }
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 500)
        {
            throw new InvalidOperationException($"Showcase field '{name}' is invalid.");
        }
    }

    private static void RequireLocalImage(string value, string name)
    {
        Require(value, name);
        if (!value.StartsWith("/images/", StringComparison.Ordinal)
            || value.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Showcase image '{name}' must reference an existing local public image.");
        }
    }
}

internal sealed record ShowcaseStat(string Label, string Value);
internal sealed record ShowcaseDestination(
    string Name,
    string Country,
    string Dates,
    string Image,
    string ImageAlt,
    string Highlight);
internal sealed record ShowcaseActivity(string Time, string Title, string State);
internal sealed record ShowcaseDay(
    int Day,
    string Date,
    string Title,
    string Location,
    string Summary,
    string Transportation,
    IReadOnlyList<ShowcaseActivity> Activities);
internal sealed record ShowcaseTraveler(
    string Initials,
    string Name,
    string Role,
    string Transportation,
    string Pace,
    string Readiness,
    string Accent);
internal sealed record ShowcaseReadinessItem(
    string Category,
    string Title,
    string Detail,
    string State);
internal sealed record ShowcaseRecommendation(
    string Label,
    string Title,
    string Explanation,
    string State);
