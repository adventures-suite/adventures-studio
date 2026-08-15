using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Contracts;
using AdventuresSuite.Companion.Mobile.Models;
using System.Globalization;

namespace AdventuresSuite.Companion.Mobile.Services;

/// <summary>Loads one read-only Adventure detail through the explicitly configured provider.</summary>
public interface ICompanionAdventureDetailProvider
{
    /// <summary>Loads presentation-safe detail for one opaque Adventure identity.</summary>
    Task<CompanionAdventureDetailPresentationResult> LoadAsync(
        string adventureId,
        CancellationToken cancellationToken = default);
}

/// <summary>Contains one provider-neutral detail outcome.</summary>
public sealed record CompanionAdventureDetailPresentationResult(
    CompanionAdventureDetailState State,
    CompanionAdventureDetailPresentation? Adventure = null,
    string? ErrorTitle = null,
    string? SupportId = null,
    bool Retryable = false,
    int? RetryAfterSeconds = null);

/// <summary>Contains only fields approved for the read-only detail presentation.</summary>
public sealed record CompanionAdventureDetailPresentation(
    string Title,
    string? Subtitle,
    string? Description,
    string Status,
    DateOnly StartDate,
    DateOnly EndDate,
    string PrimaryTimeZone,
    DateTimeOffset? GeneratedAtUtc,
    DateTimeOffset? FreshUntilUtc,
    IReadOnlyList<CompanionDestinationPresentation> Destinations,
    string? NextItemSummary,
    string? ReadinessSummary,
    IReadOnlyList<string> AvailableCapabilities);

/// <summary>Contains one destination and optional protected-Resource metadata reference.</summary>
public sealed record CompanionDestinationPresentation(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone,
    CompanionResourceReferencePresentation? HeroResource);

/// <summary>Describes Resource metadata without exposing identity, path, or protected bytes.</summary>
public sealed record CompanionResourceReferencePresentation(
    string Title,
    string MediaType,
    string Availability,
    string? AlternativeText,
    string? Attribution);

/// <summary>Provides stable accessible announcements for every detail outcome.</summary>
public static class CompanionAdventureDetailPresentationText
{
    /// <summary>Returns the accessible heading for an outcome.</summary>
    public static string Heading(CompanionAdventureDetailPresentationResult? result) => result?.State switch
    {
        CompanionAdventureDetailState.NotFound => "Adventure not found",
        CompanionAdventureDetailState.Unauthorized => "Access is required",
        CompanionAdventureDetailState.Unavailable => "Adventure unavailable",
        CompanionAdventureDetailState.MalformedOrUnsupported => "Adventure detail unsupported",
        _ => result?.ErrorTitle ?? "Adventure detail unavailable"
    };

    /// <summary>Returns a safe actionable announcement for an outcome.</summary>
    public static string Message(CompanionAdventureDetailPresentationResult? result) => result?.State switch
    {
        CompanionAdventureDetailState.NotFound => "This Adventure is no longer available to this list selection.",
        CompanionAdventureDetailState.Unauthorized => "The configured API session cannot access this Adventure.",
        CompanionAdventureDetailState.Unavailable => "Check the connection and retry with the configured provider.",
        CompanionAdventureDetailState.MalformedOrUnsupported => "The response could not be presented safely.",
        _ => "Try again later or provide the support ID if you contact support."
    };
}

/// <summary>Resolves detail only from the explicitly configured bundled editorial provider.</summary>
public sealed class DemoCompanionAdventureDetailProvider(ICompanionContentProvider demoContent)
    : ICompanionAdventureDetailProvider
{
    /// <inheritdoc />
    public async Task<CompanionAdventureDetailPresentationResult> LoadAsync(
        string adventureId,
        CancellationToken cancellationToken = default)
    {
        var list = await demoContent.LoadAsync(cancellationToken).ConfigureAwait(false);
        var adventure = list.Adventures.SingleOrDefault(item =>
            string.Equals(item.Id, adventureId, StringComparison.Ordinal));
        if (adventure is null)
        {
            return new(CompanionAdventureDetailState.NotFound);
        }

        var destinations = adventure.Segments.Select(Map).ToArray();
        var primaryTimeZone = adventure.Segments
            .Select(segment => segment.TimeZone)
            .FirstOrDefault(value => !string.Equals(value, "Local time varies", StringComparison.Ordinal))
            ?? "Local time varies";
        return new(
            CompanionAdventureDetailState.Success,
            new(
                adventure.Title,
                adventure.Subtitle,
                Description: null,
                adventure.Status,
                adventure.StartDate,
                adventure.EndDate,
                primaryTimeZone,
                GeneratedAtUtc: null,
                FreshUntilUtc: null,
                destinations,
                NextItemSummary: null,
                ReadinessSummary: null,
                AvailableCapabilities: []));
    }

    private static CompanionDestinationPresentation Map(CompanionSegment segment)
    {
        var arrivalDate = DateOnly.ParseExact(segment.ArrivalDate, "MMMM d, yyyy", CultureInfo.InvariantCulture);
        return new(segment.To, arrivalDate, arrivalDate, segment.TimeZone, HeroResource: null);
    }
}

/// <summary>Maps the typed API client onto the detail presentation boundary without fallback.</summary>
public sealed class ApiCompanionAdventureDetailProvider(ICompanionAdventureDetailService client)
    : ICompanionAdventureDetailProvider
{
    /// <inheritdoc />
    public async Task<CompanionAdventureDetailPresentationResult> LoadAsync(
        string adventureId,
        CancellationToken cancellationToken = default)
    {
        var result = await client.LoadAsync(adventureId, cancellationToken).ConfigureAwait(false);
        return new(
            result.State,
            result.Adventure is null ? null : Map(result.Adventure),
            result.ErrorTitle,
            result.SupportId,
            result.Retryable,
            result.RetryAfterSeconds);
    }

    private static CompanionAdventureDetailPresentation Map(MobileCompanionAdventureDetail source) => new(
        source.Title,
        source.Subtitle,
        source.Description,
        StatusLabel(source.Status),
        source.StartDate,
        source.EndDate,
        source.PrimaryTimeZone,
        source.GeneratedAtUtc,
        source.FreshUntilUtc,
        source.Destinations.Select(Map).ToArray(),
        source.NextItemSummary,
        source.ReadinessSummary,
        source.CapabilityLinks.Keys.Order(StringComparer.Ordinal).Select(CapabilityLabel).ToArray());

    private static CompanionDestinationPresentation Map(MobileCompanionDestination source) => new(
        source.Name,
        source.StartDate,
        source.EndDate,
        source.TimeZone,
        source.HeroResource is null ? null : new(
            source.HeroResource.Title,
            source.HeroResource.MediaType,
            ResourceAvailabilityLabel(source.HeroResource.Availability),
            source.HeroResource.AlternativeText,
            source.HeroResource.Attribution));

    private static string StatusLabel(CompanionAdventureStatus status) => status switch
    {
        CompanionAdventureStatus.Planned => "Planned",
        CompanionAdventureStatus.Committed => "Committed",
        CompanionAdventureStatus.InProgress => "In progress",
        CompanionAdventureStatus.Completed => "Completed",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static string ResourceAvailabilityLabel(CompanionResourceAvailability availability) => availability switch
    {
        CompanionResourceAvailability.Available => "Available metadata",
        CompanionResourceAvailability.Processing => "Processing",
        CompanionResourceAvailability.Blocked => "Blocked",
        CompanionResourceAvailability.Expired => "Expired",
        CompanionResourceAvailability.Revoked => "Revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(availability))
    };

    private static string CapabilityLabel(string value) => value switch
    {
        "today" => "Today",
        "itinerary" => "Itinerary",
        "readiness" => "Readiness",
        "playbook" => "Playbook",
        "offlinePackage" => "Offline package",
        _ => "Additional read capability"
    };
}

/// <summary>Owns cancellation and stale-response protection for detail navigation.</summary>
public sealed class CompanionAdventureDetailPresentationState(
    ICompanionAdventureDetailProvider provider) : IDisposable
{
    private CancellationTokenSource? _loadCancellation;
    private string? _adventureId;
    private long _requestVersion;

    /// <summary>Gets the current presentation outcome; null means the list is visible.</summary>
    public CompanionAdventureDetailPresentationResult? Current { get; private set; }

    /// <summary>Gets whether a detail view is active.</summary>
    public bool IsOpen => _adventureId is not null;

    /// <summary>Gets whether the configured provider is currently loading.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Opens and loads detail for a deliberate list selection.</summary>
    public Task OpenAsync(string adventureId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adventureId);
        _adventureId = adventureId;
        return StartLoadAsync(adventureId, cancellationToken);
    }

    /// <summary>Retries through the same configured provider.</summary>
    public Task RetryAsync(CancellationToken cancellationToken = default) =>
        _adventureId is null ? Task.CompletedTask : StartLoadAsync(_adventureId, cancellationToken);

    /// <summary>Cancels detail work and returns to the retained list state.</summary>
    public void Close()
    {
        _requestVersion++;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        _adventureId = null;
        Current = null;
        IsLoading = false;
    }

    private async Task StartLoadAsync(string adventureId, CancellationToken cancellationToken)
    {
        var requestVersion = ++_requestVersion;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var localCancellation = _loadCancellation;
        Current = null;
        IsLoading = true;

        try
        {
            var result = await provider.LoadAsync(adventureId, localCancellation.Token).ConfigureAwait(false);
            if (requestVersion == _requestVersion && !localCancellation.IsCancellationRequested)
            {
                Current = result;
                IsLoading = false;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Navigation, rapid selection, retry, or disposal superseded this request.
        }
        catch (OperationCanceledException)
        {
            if (requestVersion == _requestVersion)
            {
                IsLoading = false;
            }

            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Close();
}
