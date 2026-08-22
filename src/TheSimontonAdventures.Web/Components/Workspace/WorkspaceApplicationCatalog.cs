namespace TheSimontonAdventures.Web.Components;

/// <summary>Identifies one presentation-only AdventuresSuite workspace preview.</summary>
public enum WorkspaceApplicationKind
{
    /// <summary>Represents visual Journey discovery and creation.</summary>
    Dream,
    /// <summary>Represents guided planning advice.</summary>
    Advisor,
    /// <summary>Represents the traveler companion experience.</summary>
    Companion,
    /// <summary>Represents intentional story publishing.</summary>
    Publisher,
    /// <summary>Represents Creator website presentation.</summary>
    Web,
    /// <summary>Represents authorized workspace discovery.</summary>
    Search,
    /// <summary>Represents spatial Adventure planning.</summary>
    Maps
}

/// <summary>Describes one non-interactive preview item rendered inside a workspace placeholder.</summary>
public sealed record WorkspaceApplicationPreviewItem
{
    /// <summary>Gets the short preview label.</summary>
    public required string Label { get; init; }
    /// <summary>Gets the representative preview detail.</summary>
    public required string Detail { get; init; }
    /// <summary>Gets the optional compact metadata value.</summary>
    public string? Metadata { get; init; }
}

/// <summary>Defines the allowlisted presentation metadata for one workspace placeholder.</summary>
public sealed record WorkspaceApplicationDefinition
{
    /// <summary>Gets the stable route segment.</summary>
    public required string Slug { get; init; }
    /// <summary>Gets the workspace kind used by the shared icon and preview composition.</summary>
    public required WorkspaceApplicationKind Kind { get; init; }
    /// <summary>Gets the visible workspace name.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the concise workspace purpose.</summary>
    public required string Description { get; init; }
    /// <summary>Gets the preview heading.</summary>
    public required string PreviewTitle { get; init; }
    /// <summary>Gets the preview supporting copy.</summary>
    public required string PreviewDescription { get; init; }
    /// <summary>Gets the representative non-interactive preview items.</summary>
    public required IReadOnlyList<WorkspaceApplicationPreviewItem> PreviewItems { get; init; }
}

/// <summary>Provides the fixed, presentation-only AdventuresSuite workspace catalog.</summary>
public static class WorkspaceApplicationCatalog
{
    private static readonly IReadOnlyList<WorkspaceApplicationDefinition> Applications =
    [
        Definition("dream", WorkspaceApplicationKind.Dream, "Dream",
            "A visual place to discover Journey possibilities, explore FootSteps, and begin a private Adventure.",
            "Imagine the Journey before planning it",
            "A representative discovery experience for photography, curated Journey FootSteps, and clear paths into a new private plan.",
            ("Explore", "Browse complete Journey FootSteps and editorial collections.", "Inspiration"),
            ("Shape", "Choose an origin, dates, pace, and supported preferences.", "Review first"),
            ("Begin", "Create an independent private Journey and continue in Planner.", "Creator-owned")),
        Definition("advisor", WorkspaceApplicationKind.Advisor, "Advisor",
            "Thoughtful guidance for turning travel intent into reviewable planning decisions.",
            "A calmer planning brief",
            "A representative view of how Advisor could organize questions, tradeoffs, and next decisions without changing a plan.",
            ("Journey focus", "Balance cultural depth with an unhurried arrival rhythm.", "Review first"),
            ("Decision to make", "Choose whether the first full day favors landmarks or neighborhood time.", "Creator decides"),
            ("Worth checking", "Confirm current opening hours and transfer conditions from attributable sources.", "Verify current facts")),
        Definition("companion", WorkspaceApplicationKind.Companion, "Companion",
            "A traveler-focused view of what is next, what changed, and what is available offline.",
            "Today on your Adventure",
            "A mobile-minded preview designed around reassurance and essential traveler context, not the full Planner workspace.",
            ("09:30", "Meet at the canal-side departure point.", "Venice"),
            ("12:15", "Lunch window and independent neighborhood time.", "Flexible"),
            ("Ready offline", "Day overview and approved travel guidance.", "Private to travelers")),
        Definition("publisher", WorkspaceApplicationKind.Publisher, "Publisher",
            "An intentional review space for shaping selected Adventure moments into public stories.",
            "Shape the story before publishing",
            "A representative editorial canvas that keeps private Planning data separate until fields are explicitly selected and approved.",
            ("Opening scene", "Arrival light across the Venetian lagoon.", "Draft"),
            ("Journey chapter", "From Florence craft traditions to the Adriatic crossing.", "Outline"),
            ("Publication review", "Only approved story fields and public Resources move forward.", "Nothing published")),
        Definition("web", WorkspaceApplicationKind.Web, "Web",
            "A visual home for each Creator's approved stories, destinations, and distinctive voice.",
            "A story-led Creator site",
            "A representative public presentation composed from intentionally published content rather than private workspace records.",
            ("Featured Journey", "Italy, Greece & Croatia", "Editorial preview"),
            ("Destination story", "Venice beyond the familiar landmarks", "Visual narrative"),
            ("Creator voice", "Practical details paired with personal perspective.", "Brand-led")),
        Definition("search", WorkspaceApplicationKind.Search, "Search",
            "Authorized discovery across the AdventuresSuite information available to the current user.",
            "Find the right Adventure context",
            "A representative search result view that distinguishes private plans, approved Resources, and published stories.",
            ("Private plan", "Italy, Greece & Croatia", "Planner"),
            ("Approved Resource", "Adriatic crossing notes", "Protected"),
            ("Published story", "A first morning in Florence", "Creator site")),
        Definition("maps", WorkspaceApplicationKind.Maps, "Maps",
            "An authorized spatial view of destinations, itinerary days, routes, and candidate places.",
            "See the shape of the Journey",
            "A representative map-and-itinerary composition where planned records remain distinct from ideas and provider data.",
            ("Venice", "Arrival and two local itinerary days.", "Planned"),
            ("Florence", "Rail connection and neighborhood exploration.", "Planned"),
            ("Adriatic crossing", "Route shape is illustrative, not navigation guidance.", "Preview"))
    ];

    /// <summary>Gets the ordered workspace navigation catalog.</summary>
    public static IReadOnlyList<WorkspaceApplicationDefinition> All => Applications;

    /// <summary>Resolves one exact, allowlisted workspace route segment.</summary>
    /// <param name="slug">The route segment to resolve.</param>
    /// <param name="application">The matching definition when found.</param>
    /// <returns><see langword="true"/> when the segment names a known workspace.</returns>
    public static bool TryGet(string slug, out WorkspaceApplicationDefinition? application)
    {
        application = Applications.FirstOrDefault(candidate =>
            string.Equals(candidate.Slug, slug, StringComparison.Ordinal));
        return application is not null;
    }

    private static WorkspaceApplicationDefinition Definition(
        string slug,
        WorkspaceApplicationKind kind,
        string name,
        string description,
        string previewTitle,
        string previewDescription,
        params (string Label, string Detail, string Metadata)[] items) => new()
        {
            Slug = slug,
            Kind = kind,
            Name = name,
            Description = description,
            PreviewTitle = previewTitle,
            PreviewDescription = previewDescription,
            PreviewItems = items.Select(item => new WorkspaceApplicationPreviewItem
            {
                Label = item.Label,
                Detail = item.Detail,
                Metadata = item.Metadata
            }).ToArray()
        };
}
