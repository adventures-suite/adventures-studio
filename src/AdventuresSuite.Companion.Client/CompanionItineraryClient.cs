using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Client;

/// <summary>Reads one authorized Itinerary projection from the versioned Companion API.</summary>
public interface ICompanionItineraryTransport
{
    /// <summary>Gets the Itinerary for one validated opaque Adventure identity.</summary>
    Task<CompanionItineraryTransportResponse> GetAsync(string adventureId, CancellationToken cancellationToken = default);
}

/// <summary>Contains a typed Itinerary response and safe HTTP metadata.</summary>
public sealed record CompanionItineraryTransportResponse(
    CompanionItineraryDto Itinerary,
    string? ETag,
    string? HeaderSupportId);

/// <summary>Uses the injected HTTPS client for the Itinerary read only.</summary>
public sealed class HttpCompanionItineraryTransport(HttpClient httpClient) : ICompanionItineraryTransport
{
    private readonly HttpClient _httpClient = Validate(httpClient);

    /// <inheritdoc />
    public async Task<CompanionItineraryTransportResponse> GetAsync(
        string adventureId,
        CancellationToken cancellationToken = default)
    {
        if (!CompanionAdventureDetailValidation.IsOpaqueIdentity(adventureId))
            throw new ArgumentException("The Adventure identity is invalid.", nameof(adventureId));

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"v1/companion/adventures/{Uri.EscapeDataString(adventureId)}/itinerary");
        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var supportId = ReadSupportId(response);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await TryReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
            throw new CompanionItineraryApiException(response.StatusCode, problem, supportId);
        }

        try
        {
            if (response.Content.Headers.ContentLength > CompanionContractLimits.MaximumJsonResponseBytes)
                throw new CompanionItineraryMalformedException(supportId);
            await response.Content.LoadIntoBufferAsync(
                CompanionContractLimits.MaximumJsonResponseBytes, cancellationToken).ConfigureAwait(false);
            var dto = await response.Content.ReadFromJsonAsync(
                CompanionJsonSerializerContext.Default.CompanionItineraryDto, cancellationToken).ConfigureAwait(false)
                ?? throw new CompanionItineraryMalformedException(supportId);
            return new(dto, response.Headers.ETag?.ToString(), supportId);
        }
        catch (CompanionItineraryMalformedException) { throw; }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or HttpRequestException)
        {
            throw new CompanionItineraryMalformedException(supportId);
        }
    }

    private static HttpClient Validate(HttpClient value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.BaseAddress is null || value.BaseAddress.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("The Companion API client requires an HTTPS base address.", nameof(value));
        return value;
    }

    private static string? ReadSupportId(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-Support-Id", out var values)) return null;
        var bounded = values.Take(2).ToArray();
        return bounded.Length == 1 ? bounded[0] : null;
    }

    private static async Task<CompanionProblemDto?> TryReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            if (response.Content.Headers.ContentLength > CompanionContractLimits.MaximumJsonResponseBytes) return null;
            await response.Content.LoadIntoBufferAsync(
                CompanionContractLimits.MaximumJsonResponseBytes, cancellationToken).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync(
                CompanionJsonSerializerContext.Default.CompanionProblemDto, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or HttpRequestException)
        {
            return null;
        }
    }
}

/// <summary>Represents a safe Itinerary API failure without retaining response content.</summary>
public sealed class CompanionItineraryApiException(
    HttpStatusCode statusCode,
    CompanionProblemDto? problem,
    string? headerSupportId) : Exception("The Companion Itinerary request failed.")
{
    /// <summary>Gets the HTTP status.</summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
    /// <summary>Gets the allowlisted problem, when readable.</summary>
    public CompanionProblemDto? Problem { get; } = problem;
    /// <summary>Gets the support header, when returned.</summary>
    public string? HeaderSupportId { get; } = headerSupportId;
}

/// <summary>Represents malformed or unsupported Itinerary data without retaining its body.</summary>
public sealed class CompanionItineraryMalformedException(string? supportId)
    : Exception("The Companion Itinerary response is malformed or unsupported.")
{
    /// <summary>Gets the safe support header, when returned.</summary>
    public string? SupportId { get; } = supportId;
}

/// <summary>Loads one validated mobile-safe Itinerary projection.</summary>
public interface ICompanionItineraryService
{
    /// <summary>Loads the Itinerary for one Adventure.</summary>
    Task<CompanionItineraryResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default);
}

/// <summary>Provides the typed read-only Itinerary vertical.</summary>
public sealed class CompanionItineraryService(
    ICompanionItineraryTransport transport,
    TimeProvider timeProvider) : ICompanionItineraryService
{
    /// <inheritdoc />
    public async Task<CompanionItineraryResult> LoadAsync(
        string adventureId,
        CancellationToken cancellationToken = default)
    {
        if (!CompanionAdventureDetailValidation.IsOpaqueIdentity(adventureId))
            return CompanionItineraryResult.Invalid();
        try
        {
            var response = await transport.GetAsync(adventureId, cancellationToken).ConfigureAwait(false);
            if (!CompanionItineraryValidation.TryMap(adventureId, response, out var itinerary))
                return CompanionItineraryResult.Malformed(Safe(response.HeaderSupportId));
            if (itinerary!.FreshUntilUtc < timeProvider.GetUtcNow())
                return CompanionItineraryResult.Stale(itinerary);
            return itinerary.Days.Count == 0
                ? CompanionItineraryResult.Empty(itinerary)
                : CompanionItineraryResult.Success(itinerary);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (CompanionItineraryMalformedException exception) { return CompanionItineraryResult.Malformed(exception.SupportId); }
        catch (CompanionItineraryApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        { return CompanionItineraryResult.NotFound(Safe(exception)); }
        catch (CompanionItineraryApiException exception) when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { return CompanionItineraryResult.Unauthorized(Safe(exception)); }
        catch (CompanionItineraryApiException exception)
        {
            var problem = CompanionAdventureDetailValidation.IsSafeProblem(exception.Problem)
                && exception.Problem!.Status == (int)exception.StatusCode ? exception.Problem : null;
            return CompanionItineraryResult.Error(problem, Safe(exception));
        }
        catch (HttpRequestException) { return CompanionItineraryResult.Unavailable(); }
        catch (TaskCanceledException) { return CompanionItineraryResult.Unavailable(); }
    }

    private static string? Safe(CompanionItineraryApiException exception) =>
        Safe(exception.HeaderSupportId) ?? Safe(exception.Problem?.SupportId);

    private static string? Safe(string? value) => CompanionAdventureDetailValidation.SafeSupportId(value);
}

/// <summary>Identifies every expected Itinerary load outcome.</summary>
public enum CompanionItineraryResultState
{
    /// <summary>The current projection is available.</summary>
    Success,
    /// <summary>The authorized projection contains no days.</summary>
    Empty,
    /// <summary>The request identity is invalid.</summary>
    InvalidRequest,
    /// <summary>The resource is unavailable without revealing existence.</summary>
    NotFound,
    /// <summary>The current session is unauthorized.</summary>
    Unauthorized,
    /// <summary>The API or network is unavailable.</summary>
    Unavailable,
    /// <summary>The response is malformed or unsupported.</summary>
    MalformedOrUnsupported,
    /// <summary>The valid projection is past its freshness boundary.</summary>
    Stale,
    /// <summary>A safe API problem was returned.</summary>
    Error
}

/// <summary>Contains an Itinerary outcome and allowlisted safe metadata.</summary>
public sealed record CompanionItineraryResult(
    CompanionItineraryResultState State,
    MobileCompanionItinerary? Itinerary = null,
    string? ErrorCode = null,
    string? ErrorTitle = null,
    string? SupportId = null,
    bool Retryable = false)
{
    /// <summary>Creates success.</summary>
    public static CompanionItineraryResult Success(MobileCompanionItinerary value) => new(CompanionItineraryResultState.Success, value);
    /// <summary>Creates empty.</summary>
    public static CompanionItineraryResult Empty(MobileCompanionItinerary value) => new(CompanionItineraryResultState.Empty, value);
    /// <summary>Creates stale.</summary>
    public static CompanionItineraryResult Stale(MobileCompanionItinerary value) => new(CompanionItineraryResultState.Stale, value);
    /// <summary>Creates invalid request.</summary>
    public static CompanionItineraryResult Invalid() => new(CompanionItineraryResultState.InvalidRequest, ErrorCode: "invalid_request");
    /// <summary>Creates enumeration-safe not found.</summary>
    public static CompanionItineraryResult NotFound(string? supportId) => new(CompanionItineraryResultState.NotFound, ErrorCode: "resource_unavailable", SupportId: supportId);
    /// <summary>Creates unauthorized.</summary>
    public static CompanionItineraryResult Unauthorized(string? supportId) => new(CompanionItineraryResultState.Unauthorized, SupportId: supportId);
    /// <summary>Creates unavailable.</summary>
    public static CompanionItineraryResult Unavailable() => new(CompanionItineraryResultState.Unavailable);
    /// <summary>Creates malformed.</summary>
    public static CompanionItineraryResult Malformed(string? supportId) => new(CompanionItineraryResultState.MalformedOrUnsupported, ErrorCode: "unsupported_projection", SupportId: Safe(supportId));
    /// <summary>Creates safe error.</summary>
    public static CompanionItineraryResult Error(CompanionProblemDto? problem, string? supportId) => new(
        CompanionItineraryResultState.Error, ErrorCode: problem?.Code ?? "companion_request_failed",
        ErrorTitle: problem?.Title, SupportId: supportId, Retryable: problem?.Retryable ?? false);

    private static string? Safe(string? value) => CompanionAdventureDetailValidation.SafeSupportId(value);
}

/// <summary>Contains explicitly mapped mobile-safe Itinerary fields.</summary>
public sealed record MobileCompanionItinerary(
    string AdventureId,
    IReadOnlyList<MobileCompanionItineraryDay> Days,
    string ProjectionVersion,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset FreshUntilUtc,
    string SupportId,
    string? ETag);

/// <summary>Contains one explicitly mapped mobile-safe Itinerary day.</summary>
public sealed record MobileCompanionItineraryDay(
    DateOnly LocalDate,
    string TimeZone,
    int DayNumber,
    string? Title,
    string DestinationName,
    IReadOnlyList<MobileCompanionScheduleItem> Items,
    string? Summary,
    bool HasMaterialChange);

internal static class CompanionItineraryValidation
{
    internal static bool TryMap(
        string requestedId,
        CompanionItineraryTransportResponse response,
        out MobileCompanionItinerary? result)
    {
        result = null;
        var source = response.Itinerary;
        if (source.AdventureId != requestedId
            || source.SchemaVersion != "1.0"
            || !Bounded(source.ProjectionVersion, 128)
            || !Utc(source.GeneratedAtUtc)
            || !Utc(source.FreshUntilUtc)
            || source.FreshUntilUtc < source.GeneratedAtUtc
            || CompanionAdventureDetailValidation.SafeSupportId(source.SupportId) is null
            || response.HeaderSupportId is not null && response.HeaderSupportId != source.SupportId
            || response.ETag is not null && !SafeETag(response.ETag)
            || source.Days is null
            || source.Days.Count > CompanionContractLimits.MaximumItineraryDays)
        {
            return false;
        }

        var days = new List<MobileCompanionItineraryDay>(source.Days.Count);
        var dayIds = new HashSet<string>(StringComparer.Ordinal);
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        var previousDate = DateOnly.MinValue;
        var resourceCount = 0;
        for (var index = 0; index < source.Days.Count; index++)
        {
            var day = source.Days[index];
            if (day is null
                || !CompanionAdventureDetailValidation.IsOpaqueIdentity(day.ItineraryDayId)
                || !dayIds.Add(day.ItineraryDayId)
                || day.DayNumber != index + 1
                || index > 0 && day.LocalDate <= previousDate
                || !Iana(day.TimeZone)
                || !Optional(day.Title, 200)
                || !CompanionAdventureDetailValidation.IsOpaqueIdentity(day.DestinationVisitId)
                || !Bounded(day.DestinationName, 200)
                || !Optional(day.Summary, 2000)
                || day.Items is null
                || day.Items.Count > CompanionContractLimits.MaximumScheduleItemsPerDay
                || day.HasMaterialChange != day.Items.Any(value => value.RequiresAcknowledgment)
                || day.HasMaterialChange != (day.AcknowledgmentId is not null)
                || day.AcknowledgmentId is not null && !CompanionAdventureDetailValidation.IsOpaqueIdentity(day.AcknowledgmentId))
            {
                return false;
            }

            var items = new List<MobileCompanionScheduleItem>(day.Items.Count);
            foreach (var item in day.Items)
            {
                if (item is null || item.LocalDate != day.LocalDate || item.TimeZone != day.TimeZone
                    || !itemIds.Add(item.ItemId) || !CompanionTodayValidation.MapItem(item, out var mapped)) return false;
                resourceCount += item.Resources.Count;
                if (resourceCount > 500) return false;
                items.Add(mapped!);
            }

            previousDate = day.LocalDate;
            days.Add(new(day.LocalDate, day.TimeZone, day.DayNumber, day.Title, day.DestinationName,
                items, day.Summary, day.HasMaterialChange));
        }

        result = new(source.AdventureId, days, source.ProjectionVersion, source.GeneratedAtUtc,
            source.FreshUntilUtc, source.SupportId, response.ETag);
        return true;
    }

    private static bool Iana(string? value)
    {
        if (!Bounded(value, 100) || !value!.Contains('/') || value.Contains('\\')) return false;
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(value); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    private static bool Utc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;
    private static bool Bounded(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.EnumerateRunes().Count() <= maximum;
    private static bool Optional(string? value, int maximum) =>
        value is null || value.EnumerateRunes().Count() <= maximum;
    private static bool SafeETag(string value) =>
        value.Length <= 256 && EntityTagHeaderValue.TryParse(value, out _);
}
