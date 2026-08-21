using Microsoft.AspNetCore.Components;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Displays an authorized Journey cover image inside the Plan overview.</summary>
public partial class PlannerPlanHero
{
    /// <summary>Gets or sets the authorized image URL or protected-resource delivery URL.</summary>
    [Parameter]
    public string? ImageUrl { get; set; }

    /// <summary>Gets or sets concise alternative text for a meaningful customer-selected image.</summary>
    [Parameter]
    public string AltText { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the displayed image is temporary illustrative artwork.</summary>
    [Parameter]
    public bool IsPlaceholder { get; set; }
}
