using System.Globalization;
using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Contracts;
using AdventuresSuite.Companion.Mobile.Models;

namespace AdventuresSuite.Companion.Mobile.Services;

/// <summary>Loads Today and Next through the provider selected at composition time.</summary>
public interface ICompanionTodayProvider
{
    /// <summary>Loads presentation-safe Today and Next data.</summary>
    Task<CompanionTodayPresentationResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default);
}

/// <summary>Contains a provider-neutral Today presentation outcome.</summary>
public sealed record CompanionTodayPresentationResult(
    CompanionTodayResultState State,
    CompanionTodayPresentation? Today = null,
    string? ErrorTitle = null,
    string? SupportId = null,
    bool Retryable = false);

/// <summary>Contains ordered Today and Next presentation fields.</summary>
public sealed record CompanionTodayPresentation(
    DateOnly LocalDate,
    string TimeZone,
    string State,
    IReadOnlyList<CompanionScheduleItemPresentation> TodayItems,
    CompanionScheduleItemPresentation? NextItem,
    string? Notice,
    DateTimeOffset? FreshUntilUtc);

/// <summary>Contains one display-safe schedule item without identity or delivery paths.</summary>
public sealed record CompanionScheduleItemPresentation(
    string Type,
    string Title,
    string? Summary,
    DateOnly LocalDate,
    TimeOnly? StartLocalTime,
    TimeOnly? EndLocalTime,
    string TimeZone,
    string TimeStatus,
    string OperationalStatus,
    string? PlaceSummary,
    string? TransportationSummary,
    bool RequiresAcknowledgment,
    string? ActionLabel,
    IReadOnlyList<CompanionResourceReferencePresentation> Resources);

/// <summary>Maps the typed Today client without reordering or falling back to Demo.</summary>
public sealed class ApiCompanionTodayProvider(ICompanionTodayService client) : ICompanionTodayProvider
{
    /// <inheritdoc />
    public async Task<CompanionTodayPresentationResult> LoadAsync(
        string adventureId, CancellationToken cancellationToken = default)
    {
        var result = await client.LoadAsync(adventureId, cancellationToken).ConfigureAwait(false);
        return new(result.State, result.Today is null ? null : Map(result.Today), result.ErrorTitle, result.SupportId, result.Retryable);
    }

    private static CompanionTodayPresentation Map(MobileCompanionToday source) => new(
        source.LocalDate, source.TimeZone, Label(source.State), source.TodayItems.Select(Map).ToArray(),
        source.NextItem is null ? null : Map(source.NextItem), source.Notice, source.FreshUntilUtc);

    private static CompanionScheduleItemPresentation Map(MobileCompanionScheduleItem source) => new(
        source.ItemType, source.Title, source.Summary, source.LocalDate, source.StartLocalTime, source.EndLocalTime,
        source.TimeZone, Label(source.TimeStatus), Label(source.OperationalStatus), source.PlaceSummary,
        source.TransportationSummary, source.RequiresAcknowledgment, source.ActionLabel,
        source.Resources.Select(resource => new CompanionResourceReferencePresentation(
            resource.Title, resource.MediaType, Label(resource.Availability), resource.AlternativeText, resource.Attribution)).ToArray());

    private static string Label(CompanionTodayState value) => value switch
    {
        CompanionTodayState.BeforeAdventure => "Before Adventure",
        CompanionTodayState.Active => "Active",
        CompanionTodayState.AfterAdventure => "After Adventure",
        CompanionTodayState.NoScheduledItems => "No scheduled items",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static string Label(CompanionTimeStatus value) => value switch
    {
        CompanionTimeStatus.Scheduled => "Scheduled",
        CompanionTimeStatus.AllDay => "All day",
        CompanionTimeStatus.ToBeConfirmed => "Time to be confirmed",
        CompanionTimeStatus.Cancelled => "Cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static string Label(CompanionOperationalStatus value) => value switch
    {
        CompanionOperationalStatus.Proposed => "Proposed",
        CompanionOperationalStatus.Reserved => "Reserved",
        CompanionOperationalStatus.Confirmed => "Confirmed",
        CompanionOperationalStatus.Changed => "Changed",
        CompanionOperationalStatus.Cancelled => "Cancelled",
        CompanionOperationalStatus.Completed => "Completed",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static string Label(CompanionResourceAvailability value) => value switch
    {
        CompanionResourceAvailability.Available => "Available metadata",
        CompanionResourceAvailability.Processing => "Processing",
        CompanionResourceAvailability.Blocked => "Blocked",
        CompanionResourceAvailability.Expired => "Expired",
        CompanionResourceAvailability.Revoked => "Revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

/// <summary>Builds Today and Next only from the explicitly selected bundled fictional data.</summary>
public sealed class DemoCompanionTodayProvider(ICompanionContentProvider content) : ICompanionTodayProvider
{
    /// <inheritdoc />
    public async Task<CompanionTodayPresentationResult> LoadAsync(
        string adventureId, CancellationToken cancellationToken = default)
    {
        var result = await content.LoadAsync(cancellationToken).ConfigureAwait(false);
        var adventure = result.Adventures.SingleOrDefault(value => value.Id == adventureId);
        if (adventure is null) return new(CompanionTodayResultState.NotFound);
        if (adventure.Segments.Count == 0)
            return new(CompanionTodayResultState.Success, new(adventure.StartDate, "Local time varies", "No scheduled items", [], null, null, null));

        const int index = 0;
        var current = Map(adventure.Segments[index]);
        var next = index + 1 < adventure.Segments.Count ? Map(adventure.Segments[index + 1]) : null;
        return new(CompanionTodayResultState.Success, new(
            current.LocalDate, current.TimeZone, adventure.IsCurrent ? "Active" : "Before Adventure",
            [current], next, "Bundled fictional Demo schedule.", null));
    }

    private static CompanionScheduleItemPresentation Map(CompanionSegment source) => new(
        source.TravelMode, $"{source.From} to {source.To}", source.TravelDescription,
        DateOnly.ParseExact(source.ArrivalDate, "MMMM d, yyyy", CultureInfo.InvariantCulture),
        null, null, source.TimeZone, "Time to be confirmed", "Proposed", source.To,
        source.TravelMode, false, null, []);
}

/// <summary>Prevents cancelled or superseded Today loads from changing presentation state.</summary>
public sealed class CompanionTodayPresentationState(ICompanionTodayProvider provider) : IDisposable
{
    private CancellationTokenSource? _cancellation;
    private string? _adventureId;
    private long _version;
    /// <summary>Gets the current result.</summary>
    public CompanionTodayPresentationResult? Current { get; private set; }
    /// <summary>Gets whether the configured provider is loading.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Loads a newly selected Adventure.</summary>
    public Task LoadAsync(string adventureId, CancellationToken cancellationToken = default)
    { _adventureId = adventureId; return StartAsync(adventureId, cancellationToken); }
    /// <summary>Retries the same configured provider and Adventure.</summary>
    public Task RetryAsync(CancellationToken cancellationToken = default) =>
        _adventureId is null ? Task.CompletedTask : StartAsync(_adventureId, cancellationToken);

    private async Task StartAsync(string adventureId, CancellationToken cancellationToken)
    {
        var version = ++_version; _cancellation?.Cancel(); _cancellation?.Dispose();
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); var local = _cancellation;
        Current = null; IsLoading = true;
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
