using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Explores one authorized Journey FootStep inside Dream before Planner customization.</summary>
public partial class DreamJourneyFootStepDetail
{
    /// <summary>Gets or sets the exact immutable Journey FootStep version.</summary>
    [Parameter, EditorRequired]
    public AdventureTemplateBlueprint Template { get; set; } = null!;

    /// <summary>Gets or sets the Dream catalog return path.</summary>
    [Parameter]
    public string DreamPath { get; set; } = "/workspace";

    /// <summary>Gets or sets the Planner path that owns private Journey configuration.</summary>
    [Parameter]
    public string PlannerPath { get; set; } = "/workspace";

    private string CustomizePath =>
        $"{PlannerPath}?journeyFootStep={Uri.EscapeDataString(Template.VersionId.TemplateId)}";
}
