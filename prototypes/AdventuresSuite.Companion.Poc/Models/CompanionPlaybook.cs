namespace AdventuresSuite.Companion.Poc.Models;

/// <summary>
/// Represents a privacy-minimized POC Playbook projection.
/// </summary>
public sealed record CompanionPlaybook(
    string Title,
    string Subtitle,
    string TravelDates,
    string Rhythm,
    IReadOnlyList<PlaybookSection> Sections,
    IReadOnlyList<PlaybookDocument> ProtectedDocuments);

/// <summary>
/// Represents one Playbook section.
/// </summary>
public sealed record PlaybookSection(
    string Id,
    string Title,
    string Icon,
    IReadOnlyList<PlaybookItem> Items);

/// <summary>
/// Represents one traveler-safe Playbook item.
/// </summary>
public sealed record PlaybookItem(
    string Heading,
    string? Meta,
    string Summary,
    IReadOnlyList<string> Details);

/// <summary>
/// Represents a protected document reference without embedding credentials.
/// </summary>
public sealed record PlaybookDocument(string Title, string Category, string Status);
