using Microsoft.AspNetCore.Components;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders consistent accessible pagination controls for Planner idea collections.</summary>
public partial class PlannerIdeaPager : ComponentBase
{
    /// <summary>Gets or sets the total number of ideas in the current collection.</summary>
    [Parameter]
    public int TotalItems { get; set; }

    /// <summary>Gets or sets the selected number of cards per page.</summary>
    [Parameter]
    public int PageSize { get; set; } = 2;

    /// <summary>Gets or sets the one-based current page.</summary>
    [Parameter]
    public int CurrentPage { get; set; } = 1;

    /// <summary>Gets or sets the allowed cards-per-page choices.</summary>
    [Parameter]
    public IReadOnlyList<int> PageSizeOptions { get; set; } = [1, 2, 4];

    /// <summary>Gets or sets the callback raised when cards per page changes.</summary>
    [Parameter]
    public EventCallback<int> OnPageSizeChanged { get; set; }

    /// <summary>Gets or sets the callback raised when the current page changes.</summary>
    [Parameter]
    public EventCallback<int> OnPageChanged { get; set; }

    /// <summary>Gets the number of pages required for the current item count.</summary>
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)Math.Max(1, PageSize)));

    /// <summary>Gets the current page clamped to the available range.</summary>
    public int EffectivePage => Math.Clamp(CurrentPage, 1, TotalPages);

    private Task PreviousPageAsync() => OnPageChanged.InvokeAsync(Math.Max(1, EffectivePage - 1));

    private Task NextPageAsync() => OnPageChanged.InvokeAsync(Math.Min(TotalPages, EffectivePage + 1));

    private Task ChangePageSizeAsync(ChangeEventArgs args)
    {
        var requested = int.TryParse(args.Value?.ToString(), out var parsed) && PageSizeOptions.Contains(parsed)
            ? parsed
            : PageSizeOptions.FirstOrDefault(2);
        return OnPageSizeChanged.InvokeAsync(requested);
    }
}
