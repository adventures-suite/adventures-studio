using System.Globalization;
using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Mobile.Models;

namespace AdventuresSuite.Companion.Mobile.Services;

/// <summary>Loads an Itinerary through the provider selected at composition time.</summary>
public interface ICompanionItineraryProvider
{
    /// <summary>Loads presentation-safe Itinerary data.</summary>
    Task<CompanionItineraryPresentationResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default);
}

/// <summary>Contains a provider-neutral Itinerary presentation outcome.</summary>
public sealed record CompanionItineraryPresentationResult(
    CompanionItineraryResultState State,
    CompanionItineraryPresentation? Itinerary = null,
    string? ErrorTitle = null,
    string? SupportId = null,
    bool Retryable = false);

/// <summary>Contains ordered Journey days.</summary>
public sealed record CompanionItineraryPresentation(IReadOnlyList<CompanionItineraryDayPresentation> Days, DateTimeOffset? FreshUntilUtc);

/// <summary>Contains one local Journey day and its schedule.</summary>
public sealed record CompanionItineraryDayPresentation(
    DateOnly LocalDate, string TimeZone, int DayNumber, string? Title, string DestinationName,
    IReadOnlyList<CompanionScheduleItemPresentation> Items, string? Summary, bool HasMaterialChange);

/// <summary>Maps the typed API client only; failures never select Demo.</summary>
public sealed class ApiCompanionItineraryProvider(ICompanionItineraryService client) : ICompanionItineraryProvider
{
    /// <inheritdoc />
    public async Task<CompanionItineraryPresentationResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default)
    {
        var result = await client.LoadAsync(adventureId, cancellationToken).ConfigureAwait(false);
        return new(result.State, result.Itinerary is null ? null : Map(result.Itinerary), result.ErrorTitle, result.SupportId, result.Retryable);
    }

    private static CompanionItineraryPresentation Map(MobileCompanionItinerary source) => new(
        source.Days.Select(day => new CompanionItineraryDayPresentation(
            day.LocalDate, day.TimeZone, day.DayNumber, day.Title, day.DestinationName,
            day.Items.Select(Map).ToArray(), day.Summary, day.HasMaterialChange)).ToArray(), source.FreshUntilUtc);

    private static CompanionScheduleItemPresentation Map(MobileCompanionScheduleItem source) => new(
        source.ItemType, source.Title, source.Summary, source.LocalDate, source.StartLocalTime, source.EndLocalTime,
        source.TimeZone, Label(source.TimeStatus), Label(source.OperationalStatus), source.PlaceSummary,
        source.TransportationSummary, source.RequiresAcknowledgment, source.ActionLabel,
        source.Resources.Select(resource => new CompanionResourceReferencePresentation(
            resource.Title, resource.MediaType, Label(resource.Availability), resource.AlternativeText, resource.Attribution)).ToArray());

    private static string Label(Contracts.CompanionTimeStatus value) => value switch
    {
        Contracts.CompanionTimeStatus.Scheduled => "Scheduled",
        Contracts.CompanionTimeStatus.AllDay => "All day",
        Contracts.CompanionTimeStatus.ToBeConfirmed => "Time to be confirmed",
        Contracts.CompanionTimeStatus.Cancelled => "Cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Label(Contracts.CompanionOperationalStatus value) => value switch
    {
        Contracts.CompanionOperationalStatus.Proposed => "Proposed",
        Contracts.CompanionOperationalStatus.Reserved => "Reserved",
        Contracts.CompanionOperationalStatus.Confirmed => "Confirmed",
        Contracts.CompanionOperationalStatus.Changed => "Changed",
        Contracts.CompanionOperationalStatus.Cancelled => "Cancelled",
        Contracts.CompanionOperationalStatus.Completed => "Completed",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Label(Contracts.CompanionResourceAvailability value) => value switch
    {
        Contracts.CompanionResourceAvailability.Available => "Available metadata",
        Contracts.CompanionResourceAvailability.Processing => "Processing",
        Contracts.CompanionResourceAvailability.Blocked => "Blocked",
        Contracts.CompanionResourceAvailability.Expired => "Expired",
        Contracts.CompanionResourceAvailability.Revoked => "Revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

/// <summary>Builds Itinerary data only from the explicitly selected fictional Demo bundle.</summary>
public sealed class DemoCompanionItineraryProvider(ICompanionContentProvider content) : ICompanionItineraryProvider
{
    /// <inheritdoc />
    public async Task<CompanionItineraryPresentationResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default)
    {
        var result = await content.LoadAsync(cancellationToken).ConfigureAwait(false);
        var adventure = result.Adventures.SingleOrDefault(value => value.Id == adventureId);
        if (adventure is null) return new(CompanionItineraryResultState.NotFound);
        var days = adventure.Segments.Select((segment, index) => new CompanionItineraryDayPresentation(
            DateOnly.ParseExact(segment.ArrivalDate, "MMMM d, yyyy", CultureInfo.InvariantCulture), segment.TimeZone,
            index + 1, null, segment.To,
            [new(segment.TravelMode, $"{segment.From} to {segment.To}", segment.TravelDescription,
                DateOnly.ParseExact(segment.ArrivalDate, "MMMM d, yyyy", CultureInfo.InvariantCulture), null, null,
                segment.TimeZone, "Time to be confirmed", "Proposed", segment.To, segment.TravelMode, false, null, [])],
            "Bundled fictional Demo itinerary.", false)).ToArray();
        var presentation = new CompanionItineraryPresentation(days, null);
        return new(days.Length == 0 ? CompanionItineraryResultState.Empty : CompanionItineraryResultState.Success, presentation);
    }
}

/// <summary>Prevents cancelled or superseded Itinerary loads from changing presentation state.</summary>
public sealed class CompanionItineraryPresentationState(ICompanionItineraryProvider provider) : IDisposable
{
    private CancellationTokenSource? _cancellation;
    private string? _adventureId;
    private long _version;
    /// <summary>Gets the current result.</summary>
    public CompanionItineraryPresentationResult? Current { get; private set; }
    /// <summary>Gets whether the provider is loading.</summary>
    public bool IsLoading { get; private set; }
    /// <summary>Loads a newly selected Adventure.</summary>
    public Task LoadAsync(string adventureId, CancellationToken cancellationToken = default)
    { _adventureId = adventureId; return StartAsync(adventureId, cancellationToken); }
    /// <summary>Retries the same provider and Adventure.</summary>
    public Task RetryAsync(CancellationToken cancellationToken = default) =>
        _adventureId is null ? Task.CompletedTask : StartAsync(_adventureId, cancellationToken);

    private async Task StartAsync(string adventureId, CancellationToken cancellationToken)
    {
        var version = ++_version;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var local = _cancellation;
        Current = null;
        IsLoading = true;
        try
        {
            var result = await provider.LoadAsync(adventureId, local.Token).ConfigureAwait(false);
            if (version == _version && !local.IsCancellationRequested) { Current = result; IsLoading = false; }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        catch (OperationCanceledException) { if (version == _version) IsLoading = false; throw; }
    }

    /// <inheritdoc />
    public void Dispose() { _version++; _cancellation?.Cancel(); _cancellation?.Dispose(); _cancellation = null; IsLoading = false; Current = null; }
}
