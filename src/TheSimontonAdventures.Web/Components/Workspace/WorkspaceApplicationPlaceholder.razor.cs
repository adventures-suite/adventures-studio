using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Resources;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders one honest, non-interactive workspace experience preview.</summary>
public partial class WorkspaceApplicationPlaceholder
{
    private static readonly ResourceId CompanionPreviewResourceId = new("resource_workspace_companion_preview");

    [Inject]
    private IResourceService ResourceService { get; set; } = null!;

    /// <summary>Gets or sets the allowlisted application definition.</summary>
    [Parameter, EditorRequired]
    public WorkspaceApplicationDefinition Application { get; set; } = null!;

    /// <summary>Gets or sets the addressed Creator ownership boundary.</summary>
    [Parameter, EditorRequired]
    public CreatorId CreatorId { get; set; }

    /// <summary>Gets or sets the authenticated Planner return path.</summary>
    [Parameter]
    public string PlannerPath { get; set; } = "/workspace";

    /// <summary>Gets or sets the validated public Simonton Adventures preview URL.</summary>
    [Parameter]
    public Uri? SimontonAdventuresUrl { get; set; }

    /// <summary>Gets or sets the authorized Journey FootSteps shown by Dream.</summary>
    [Parameter]
    public IReadOnlyList<AdventureTemplateBlueprint> JourneyTemplates { get; set; } = [];

    private ResolvedResource? CompanionPreview { get; set; }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        CompanionPreview = Application.Kind == WorkspaceApplicationKind.Companion
            ? await ResolveCompanionPreviewAsync()
            : null;
    }

    private async Task<ResolvedResource?> ResolveCompanionPreviewAsync()
    {
        try
        {
            return await ResourceService.ResolvePublicAsync(CreatorId, CompanionPreviewResourceId);
        }
        catch (InvalidDataException)
        {
            // A private Creator does not need a public Content Engine registry.
            // The Companion placeholder remains useful with its built-in preview.
            return null;
        }
    }
}
