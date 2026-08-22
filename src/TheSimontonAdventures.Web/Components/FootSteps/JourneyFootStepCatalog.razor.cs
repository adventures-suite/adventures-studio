using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders an authorized Journey FootStep catalog across discovery and planning workspaces.</summary>
public partial class JourneyFootStepCatalog
{
    private static readonly IReadOnlyList<int> PageSizeOptions = [3, 5, 10, 20];

    /// <summary>Gets or sets the authorized immutable Journey templates.</summary>
    [Parameter]
    public IReadOnlyList<AdventureTemplateBlueprint> Templates { get; set; } = [];

    /// <summary>Gets or sets the selected template identity used by the Planner review surface.</summary>
    [Parameter]
    public AdventureTemplateVersionId? SelectedTemplateVersion { get; set; }

    /// <summary>Gets or sets the callback used when the current workspace owns configuration.</summary>
    [Parameter]
    public EventCallback<AdventureTemplateBlueprint> OnConfigure { get; set; }

    /// <summary>Gets or sets the Dream path used to explore an exact Journey FootStep.</summary>
    [Parameter]
    public string DetailsPath { get; set; } = "/workspace";

    private int PageSize { get; set; } = 3;
    private int CurrentPage { get; set; } = 1;
    private IReadOnlyList<AdventureTemplateBlueprint> PagedTemplates => Templates
        .Skip((CurrentPage - 1) * PageSize)
        .Take(PageSize)
        .ToArray();

    private Task ChangePageSizeAsync(int pageSize)
    {
        PageSize = pageSize;
        CurrentPage = 1;
        return Task.CompletedTask;
    }

    private Task ChangePageAsync(int page)
    {
        CurrentPage = page;
        return Task.CompletedTask;
    }

    private string ConfigurePath(AdventureTemplateBlueprint template) =>
        $"{DetailsPath}?journeyFootStep={Uri.EscapeDataString(template.VersionId.TemplateId)}";

    private static string Monogram(string title) => string.Concat(
        title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(word => char.ToUpperInvariant(word[0])));

    private static string Route(AdventureTemplateBlueprint template) =>
        string.Join(" → ", template.Destinations.Select(destination => destination.Name));

    private static IReadOnlyList<string> DiscoveryTags(AdventureTemplateBlueprint template) =>
        new[]
        {
            $"{template.DurationDays} days",
            $"{template.Destinations.Count} {(template.Destinations.Count == 1 ? "destination" : "destinations")}"
        }
        .Concat(template.Transportation
            .Select(segment => segment.Mode)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        .ToArray();
}
