using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Mobile.Services;
using Microsoft.AspNetCore.Components;

namespace AdventuresSuite.Companion.Mobile.Components.Pages;

/// <summary>Renders the complete set of explicit Itinerary presentation states.</summary>
public partial class JourneyTab
{
    /// <summary>Gets whether the itinerary is loading.</summary>
    [Parameter] public bool IsLoading { get; set; }
    /// <summary>Gets the current provider-neutral result.</summary>
    [Parameter] public CompanionItineraryPresentationResult? Result { get; set; }
    /// <summary>Gets the retry callback.</summary>
    [Parameter] public EventCallback OnRetry { get; set; }

    private string StateHeading => Result?.State switch
    {
        CompanionItineraryResultState.Empty => "No itinerary days yet",
        CompanionItineraryResultState.Unauthorized => "Sign-in is required",
        CompanionItineraryResultState.MalformedOrUnsupported => "Itinerary cannot be displayed",
        CompanionItineraryResultState.Stale => "Itinerary needs refreshing",
        _ => Result?.ErrorTitle ?? "Itinerary unavailable"
    };
    private string StateMessage => Result?.State switch
    {
        CompanionItineraryResultState.Empty => "Authorized Journey days will appear here when they are planned.",
        CompanionItineraryResultState.Unauthorized => "This session cannot access this Adventure.",
        CompanionItineraryResultState.Unavailable => "Check your connection and try again.",
        CompanionItineraryResultState.MalformedOrUnsupported => "The server returned an unsupported itinerary.",
        CompanionItineraryResultState.Stale => "Companion will not present an itinerary beyond its freshness boundary.",
        CompanionItineraryResultState.InvalidRequest or CompanionItineraryResultState.NotFound => "This Adventure is not available to the current traveler.",
        _ => "Try again later or provide the support ID if you contact support."
    };
    private bool CanRetry => Result?.State is CompanionItineraryResultState.Unavailable
        or CompanionItineraryResultState.MalformedOrUnsupported or CompanionItineraryResultState.Stale
        || Result?.Retryable == true;
    private static string FormatDate(DateOnly value) => value.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
    private static string FormatTime(CompanionScheduleItemPresentation item) => item.StartLocalTime is null
        ? item.TimeStatus
        : item.EndLocalTime is null ? item.StartLocalTime.Value.ToString("h:mm tt")
        : $"{item.StartLocalTime.Value:h:mm tt}–{item.EndLocalTime.Value:h:mm tt}";
}
