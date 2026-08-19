using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders the pre-plan choice between manual creation and authorized Journey Templates.</summary>
public partial class PlannerJourneyStarter : ComponentBase
{
    private static readonly IReadOnlyList<int> JourneyPageSizeOptions = [1, 2, 4];
    private readonly Dictionary<AdventureTemplateVersionId, string> idempotencyKeys = [];

    /// <summary>Gets or sets the authorized immutable Journey Templates.</summary>
    [Parameter]
    public IReadOnlyList<AdventureTemplateBlueprint> Templates { get; set; } = [];

    /// <summary>Gets or sets the antiforgery-protected template creation path.</summary>
    [Parameter]
    public string CreateFromTemplatePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the requested BCP 47 catalog locale.</summary>
    [Parameter]
    public string Locale { get; set; } = "en-US";

    /// <summary>Gets or sets the callback indicating whether template discovery is active.</summary>
    [Parameter]
    public EventCallback<bool> OnTemplateModeChanged { get; set; }

    /// <summary>Gets or sets whether Journey discovery is initially expanded.</summary>
    [Parameter]
    public bool StartWithIdeasOpen { get; set; }

    /// <summary>Gets or sets the optional exact template identity initially shown in preview.</summary>
    [Parameter]
    public string? InitialPreviewTemplateId { get; set; }

    /// <summary>Gets whether the Journey discovery panel is open.</summary>
    public bool IsBrowsingIdeas { get; private set; }

    private AdventureTemplateBlueprint? SelectedTemplate { get; set; }
    private int PageSize { get; set; } = 2;
    private int CurrentPage { get; set; } = 1;
    private IReadOnlyList<AdventureTemplateBlueprint> PagedTemplates => Templates
        .Skip((CurrentPage - 1) * PageSize)
        .Take(PageSize)
        .ToArray();

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        IsBrowsingIdeas = StartWithIdeasOpen;
        if (InitialPreviewTemplateId is not null)
        {
            SelectedTemplate = Templates.FirstOrDefault(candidate =>
                candidate.VersionId.TemplateId == InitialPreviewTemplateId);
        }
    }

    private async Task BrowseIdeasAsync()
    {
        IsBrowsingIdeas = true;
        CurrentPage = 1;
        await OnTemplateModeChanged.InvokeAsync(true);
    }

    private async Task StartFromScratchAsync()
    {
        IsBrowsingIdeas = false;
        SelectedTemplate = null;
        await OnTemplateModeChanged.InvokeAsync(false);
    }

    private void PreviewTemplate(AdventureTemplateBlueprint template) =>
        SelectedTemplate = template;

    private Task ChangePageSizeAsync(int pageSize)
    {
        PageSize = pageSize;
        CurrentPage = 1;
        return Task.CompletedTask;
    }

    private Task ChangePageAsync(int page)
    {
        CurrentPage = page;
        SelectedTemplate = null;
        return Task.CompletedTask;
    }

    private string IdempotencyKey(AdventureTemplateVersionId versionId)
    {
        if (!idempotencyKeys.TryGetValue(versionId, out var key))
        {
            key = $"request_{Guid.NewGuid():N}";
            idempotencyKeys.Add(versionId, key);
        }

        return key;
    }

    private static string Monogram(string title) => string.Concat(
        title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(word => char.ToUpperInvariant(word[0])));

    private static string Route(AdventureTemplateBlueprint template) =>
        string.Join(" → ", template.Destinations.Select(destination => destination.Name));
}
