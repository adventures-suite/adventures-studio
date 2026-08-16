using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Client;

/// <summary>Reads one authorized Today projection from the versioned Companion API.</summary>
public interface ICompanionTodayTransport
{
    /// <summary>Gets Today and Next for one validated opaque Adventure identity.</summary>
    Task<CompanionTodayTransportResponse> GetAsync(string adventureId, CancellationToken cancellationToken = default);
}

/// <summary>Contains a typed Today response and safe HTTP metadata.</summary>
public sealed record CompanionTodayTransportResponse(CompanionTodayDto Today, string? ETag, string? HeaderSupportId);

/// <summary>Uses the injected HTTPS client for the Today read only.</summary>
public sealed class HttpCompanionTodayTransport : ICompanionTodayTransport
{
    private readonly HttpClient _httpClient;

    /// <summary>Initializes the transport with an existing configured HTTPS client.</summary>
    public HttpCompanionTodayTransport(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (httpClient.BaseAddress is null || httpClient.BaseAddress.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("The Companion API client requires an HTTPS base address.", nameof(httpClient));
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<CompanionTodayTransportResponse> GetAsync(
        string adventureId, CancellationToken cancellationToken = default)
    {
        if (!CompanionAdventureDetailValidation.IsOpaqueIdentity(adventureId))
            throw new ArgumentException("The Adventure identity is invalid.", nameof(adventureId));

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"v1/companion/adventures/{Uri.EscapeDataString(adventureId)}/today");
        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var supportId = ReadSupportId(response);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await TryReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
            throw new CompanionTodayApiException(response.StatusCode, problem, supportId);
        }

        try
        {
            if (response.Content.Headers.ContentLength > CompanionContractLimits.MaximumJsonResponseBytes)
                throw new CompanionTodayMalformedException(supportId);
            await response.Content.LoadIntoBufferAsync(
                CompanionContractLimits.MaximumJsonResponseBytes, cancellationToken).ConfigureAwait(false);
            var dto = await response.Content.ReadFromJsonAsync(
                CompanionJsonSerializerContext.Default.CompanionTodayDto, cancellationToken).ConfigureAwait(false)
                ?? throw new CompanionTodayMalformedException(supportId);
            return new(dto, response.Headers.ETag?.ToString(), supportId);
        }
        catch (CompanionTodayMalformedException) { throw; }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or HttpRequestException)
        {
            throw new CompanionTodayMalformedException(supportId);
        }
    }

    private static string? ReadSupportId(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-Support-Id", out var values)) return null;
        var bounded = values.Take(2).ToArray();
        return bounded.Length == 1 ? bounded[0] : null;
    }

    private static async Task<CompanionProblemDto?> TryReadProblemAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
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

/// <summary>Represents a safe Today API failure without retaining response content.</summary>
public sealed class CompanionTodayApiException(
    HttpStatusCode statusCode, CompanionProblemDto? problem, string? headerSupportId)
    : Exception("The Companion Today request failed.")
{
    /// <summary>Gets the HTTP status.</summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
    /// <summary>Gets the allowlisted problem, when readable.</summary>
    public CompanionProblemDto? Problem { get; } = problem;
    /// <summary>Gets the support header, when returned.</summary>
    public string? HeaderSupportId { get; } = headerSupportId;
}

/// <summary>Represents malformed or unsupported Today data without retaining its body.</summary>
public sealed class CompanionTodayMalformedException(string? supportId)
    : Exception("The Companion Today response is malformed or unsupported.")
{
    /// <summary>Gets the safe support header, when returned.</summary>
    public string? SupportId { get; } = supportId;
}

/// <summary>Loads one validated mobile-safe Today projection.</summary>
public interface ICompanionTodayService
{
    /// <summary>Loads Today and Next for one Adventure.</summary>
    Task<CompanionTodayResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default);
}

/// <summary>Provides the typed read-only Today vertical.</summary>
public sealed class CompanionTodayService(ICompanionTodayTransport transport) : ICompanionTodayService
{
    /// <summary>Loads Today and Next for one Adventure.</summary>
    public async Task<CompanionTodayResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default)
    {
        if (!CompanionAdventureDetailValidation.IsOpaqueIdentity(adventureId)) return CompanionTodayResult.Invalid();
        try
        {
            var response = await transport.GetAsync(adventureId, cancellationToken).ConfigureAwait(false);
            return CompanionTodayValidation.TryMap(adventureId, response, out var today)
                ? CompanionTodayResult.Success(today!)
                : CompanionTodayResult.Malformed(CompanionAdventureDetailValidation.SafeSupportId(response.HeaderSupportId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (CompanionTodayMalformedException exception) { return CompanionTodayResult.Malformed(exception.SupportId); }
        catch (CompanionTodayApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        { return CompanionTodayResult.NotFound(SafeSupport(exception)); }
        catch (CompanionTodayApiException exception) when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { return CompanionTodayResult.Unauthorized(SafeSupport(exception)); }
        catch (CompanionTodayApiException exception)
        {
            var problem = CompanionAdventureDetailValidation.IsSafeProblem(exception.Problem)
                && exception.Problem!.Status == (int)exception.StatusCode ? exception.Problem : null;
            return CompanionTodayResult.Error(problem, SafeSupport(exception));
        }
        catch (HttpRequestException) { return CompanionTodayResult.Unavailable(); }
        catch (TaskCanceledException) { return CompanionTodayResult.Unavailable(); }
    }

    private static string? SafeSupport(CompanionTodayApiException exception) =>
        CompanionAdventureDetailValidation.SafeSupportId(exception.HeaderSupportId)
        ?? CompanionAdventureDetailValidation.SafeSupportId(exception.Problem?.SupportId);
}

/// <summary>Identifies every expected Today load outcome.</summary>
public enum CompanionTodayResultState { Success, InvalidRequest, NotFound, Unauthorized, Unavailable, MalformedOrUnsupported, Error }

/// <summary>Contains a Today outcome and allowlisted safe metadata.</summary>
public sealed record CompanionTodayResult(
    CompanionTodayResultState State, MobileCompanionToday? Today = null, string? ErrorCode = null,
    string? ErrorTitle = null, string? SupportId = null, bool Retryable = false, int? RetryAfterSeconds = null)
{
    /// <summary>Creates success.</summary>
    public static CompanionTodayResult Success(MobileCompanionToday value) => new(CompanionTodayResultState.Success, value);
    /// <summary>Creates invalid request.</summary>
    public static CompanionTodayResult Invalid() => new(CompanionTodayResultState.InvalidRequest, ErrorCode: "invalid_request");
    /// <summary>Creates enumeration-safe not found.</summary>
    public static CompanionTodayResult NotFound(string? supportId) => new(CompanionTodayResultState.NotFound, ErrorCode: "resource_unavailable", SupportId: supportId);
    /// <summary>Creates unauthorized.</summary>
    public static CompanionTodayResult Unauthorized(string? supportId) => new(CompanionTodayResultState.Unauthorized, SupportId: supportId);
    /// <summary>Creates unavailable.</summary>
    public static CompanionTodayResult Unavailable() => new(CompanionTodayResultState.Unavailable);
    /// <summary>Creates malformed.</summary>
    public static CompanionTodayResult Malformed(string? supportId) => new(CompanionTodayResultState.MalformedOrUnsupported, ErrorCode: "unsupported_projection", SupportId: CompanionAdventureDetailValidation.SafeSupportId(supportId));
    /// <summary>Creates safe error.</summary>
    public static CompanionTodayResult Error(CompanionProblemDto? problem, string? supportId) => new(
        CompanionTodayResultState.Error, ErrorCode: problem?.Code ?? "companion_request_failed",
        ErrorTitle: problem?.Title, SupportId: supportId, Retryable: problem?.Retryable ?? false,
        RetryAfterSeconds: problem?.RetryAfterSeconds);
}

/// <summary>Contains explicitly mapped mobile-safe Today fields.</summary>
public sealed record MobileCompanionToday(
    string AdventureId, DateOnly LocalDate, string TimeZone, CompanionTodayState State,
    IReadOnlyList<MobileCompanionScheduleItem> TodayItems, MobileCompanionScheduleItem? NextItem,
    string? Notice, string SchemaVersion, string ProjectionVersion, DateTimeOffset GeneratedAtUtc,
    DateTimeOffset FreshUntilUtc, string? SyncCursor, string SupportId, string? ETag);

/// <summary>Contains explicitly mapped mobile-safe schedule fields without protected delivery paths.</summary>
public sealed record MobileCompanionScheduleItem(
    string ItemId, string ItemType, string Title, string? Summary, DateOnly LocalDate,
    TimeOnly? StartLocalTime, TimeOnly? EndLocalTime, string TimeZone, CompanionTimeStatus TimeStatus,
    CompanionOperationalStatus OperationalStatus, string? PlaceSummary, string? TransportationSummary,
    IReadOnlyList<MobileCompanionResource> Resources, bool RequiresAcknowledgment, string? ActionLabel);

internal static class CompanionTodayValidation
{
    public static bool TryMap(string requestedId, CompanionTodayTransportResponse response, out MobileCompanionToday? result)
    {
        result = null;
        var source = response.Today;
        if (source.AdventureId != requestedId || source.SchemaVersion != "1.0"
            || !Bounded(source.ProjectionVersion, 128) || !Utc(source.GeneratedAtUtc) || !Utc(source.FreshUntilUtc)
            || source.FreshUntilUtc < source.GeneratedAtUtc || !Optional(source.SyncCursor, 2048)
            || CompanionAdventureDetailValidation.SafeSupportId(source.SupportId) is null
            || response.HeaderSupportId is not null && response.HeaderSupportId != source.SupportId
            || response.ETag is not null && !SafeETag(response.ETag)
            || !Iana(source.TimeZone) || !Enum.IsDefined(source.State) || source.TodayItems is null
            || source.TodayItems.Count > 250 || !Optional(source.Notice, 300)
            || source.TodayItems.Sum(value => value?.Resources?.Count ?? 0) + (source.NextItem?.Resources?.Count ?? 0) > 500
            || !MapItems(source.TodayItems, source.LocalDate, true, out var items)
            || !MapNext(source.NextItem, source.LocalDate, source.TodayItems, out var next)) return false;
        result = new(source.AdventureId, source.LocalDate, source.TimeZone, source.State, items!, next,
            source.Notice, source.SchemaVersion, source.ProjectionVersion, source.GeneratedAtUtc,
            source.FreshUntilUtc, source.SyncCursor, source.SupportId, response.ETag);
        return true;
    }

    private static bool MapItems(IReadOnlyList<CompanionScheduleItemDto> sources, DateOnly date, bool sameDate, out IReadOnlyList<MobileCompanionScheduleItem>? result)
    {
        // CompanionScheduleItemDto v1 has no explicit sequence. Its authoritative wire-array position is presentation order.
        result = null; var list = new List<MobileCompanionScheduleItem>(sources.Count); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (source is null || sameDate && source.LocalDate != date || !ids.Add(source.ItemId)
                || !MapItem(source, out var item) || source.LocalDate < date) return false;
            list.Add(item!);
        }
        result = list; return true;
    }

    private static bool MapNext(CompanionScheduleItemDto? source, DateOnly date, IReadOnlyList<CompanionScheduleItemDto> today, out MobileCompanionScheduleItem? result)
    {
        result = null;
        if (source is null) return true;
        if (source.LocalDate < date || today.Any(value => value.ItemId == source.ItemId)) return false;
        return MapItem(source, out result);
    }

    internal static bool MapItem(CompanionScheduleItemDto source, out MobileCompanionScheduleItem? result)
    {
        result = null;
        var times = source.TimeStatus switch
        {
            CompanionTimeStatus.Scheduled => source.StartLocalTime is not null && (source.EndLocalTime is null || source.EndLocalTime >= source.StartLocalTime),
            CompanionTimeStatus.AllDay or CompanionTimeStatus.ToBeConfirmed => source.StartLocalTime is null && source.EndLocalTime is null,
            CompanionTimeStatus.Cancelled => source.StartLocalTime is null && source.EndLocalTime is null && source.OperationalStatus == CompanionOperationalStatus.Cancelled,
            _ => false
        };
        if (!CompanionAdventureDetailValidation.IsOpaqueIdentity(source.ItemId) || !Bounded(source.ItemType, 64)
            || !Bounded(source.Title, 200) || !Optional(source.Summary, 2000) || !Iana(source.TimeZone)
            || !Enum.IsDefined(source.TimeStatus) || !Enum.IsDefined(source.OperationalStatus) || !times
            || !Optional(source.PlaceSummary, 300) || !Optional(source.TransportationSummary, 300)
            || !MapResources(source.Resources, out var resources) || !Optional(source.ActionLabel, 100)
            || source.ActionPath is not null
            || source.OperationalStatus == CompanionOperationalStatus.Cancelled && source.TimeStatus != CompanionTimeStatus.Cancelled
            || source.TimeStatus == CompanionTimeStatus.ToBeConfirmed
                && source.OperationalStatus is CompanionOperationalStatus.Reserved
                    or CompanionOperationalStatus.Confirmed or CompanionOperationalStatus.Completed
            || source.OperationalStatus == CompanionOperationalStatus.Changed && !source.RequiresAcknowledgment) return false;
        result = new(source.ItemId, source.ItemType, source.Title, source.Summary, source.LocalDate,
            source.StartLocalTime, source.EndLocalTime, source.TimeZone, source.TimeStatus, source.OperationalStatus,
            source.PlaceSummary, source.TransportationSummary, resources!, source.RequiresAcknowledgment, source.ActionLabel);
        return true;
    }

    private static bool MapResources(IReadOnlyList<CompanionResourceSummaryDto>? sources, out IReadOnlyList<MobileCompanionResource>? result)
    {
        result = null;
        if (sources is null || sources.Count > 500) return false;
        var values = new List<MobileCompanionResource>(sources.Count); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (source is null || !CompanionAdventureDetailValidation.IsOpaqueIdentity(source.ResourceId)
                || !ids.Add(source.ResourceId) || !Bounded(source.MediaType, 127) || source.ByteLength is < 0
                || !Bounded(source.Title, 200) || !Optional(source.AlternativeText, 500)
                || !Optional(source.Attribution, 300) || !Enum.IsDefined(source.Availability)
                || source.RetainUntilUtc is not null && !Utc(source.RetainUntilUtc.Value)
                || source.ContentPath is not null && !SafeRelativePath(source.ContentPath)) return false;
            values.Add(new(source.ResourceId, source.MediaType, source.ByteLength, source.Title,
                source.AlternativeText, source.Attribution, source.Availability, source.OfflineEligible,
                source.RetainUntilUtc, source.ContentPath));
        }
        result = values; return true;
    }

    private static bool SafeRelativePath(string value)
    {
        if (value.Length is < 1 or > 2048 || !value.StartsWith("/v1/companion/", StringComparison.Ordinal)
            || value.Contains('\\') || value.Contains('#')) return false;
        try { return !Uri.UnescapeDataString(value.Split('?', 2)[0]).Split('/').Any(segment => segment is "." or ".."); }
        catch (UriFormatException) { return false; }
    }

    private static bool Iana(string? value)
    {
        if (!Bounded(value, 100) || !value!.Contains('/') || value.Contains('\\')
            || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '/' or '_' or '+' or '-'))) return false;
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(value); return !value.StartsWith("Custom/", StringComparison.Ordinal); }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
    private static bool Utc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;
    private static bool Bounded(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.EnumerateRunes().Count() <= max;
    private static bool Optional(string? value, int max) => value is null || value.EnumerateRunes().Count() <= max;
    private static bool SafeETag(string value) => value.Length <= 256 && EntityTagHeaderValue.TryParse(value, out _);
}
