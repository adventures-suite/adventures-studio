using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Client;

/// <summary>Reads one authorized Adventure overview from the versioned Companion API.</summary>
public interface ICompanionAdventureDetailTransport
{
    /// <summary>Gets one Adventure overview.</summary>
    /// <param name="adventureId">The validated opaque Adventure identity.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The typed response and safe HTTP metadata.</returns>
    Task<CompanionAdventureDetailTransportResponse> GetAsync(
        string adventureId,
        CancellationToken cancellationToken = default);
}

/// <summary>Contains a typed detail response plus safe transport metadata.</summary>
/// <param name="Adventure">The source-generated contract DTO.</param>
/// <param name="ETag">The opaque HTTP entity tag, when returned.</param>
/// <param name="HeaderSupportId">The support header, when returned.</param>
public sealed record CompanionAdventureDetailTransportResponse(
    CompanionAdventureDto Adventure,
    string? ETag,
    string? HeaderSupportId);

/// <summary>Uses the injected HTTPS client configuration for the detail read only.</summary>
public sealed class HttpCompanionAdventureDetailTransport : ICompanionAdventureDetailTransport
{
    private const string AdventureDetailPrefix = "v1/companion/adventures/";
    private readonly HttpClient _httpClient;

    /// <summary>Initializes the transport with the existing configured HTTPS client.</summary>
    /// <param name="httpClient">The configured Companion API client.</param>
    public HttpCompanionAdventureDetailTransport(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (httpClient.BaseAddress is null || httpClient.BaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The Companion API client requires an HTTPS base address.", nameof(httpClient));
        }

        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<CompanionAdventureDetailTransportResponse> GetAsync(
        string adventureId,
        CancellationToken cancellationToken = default)
    {
        if (!CompanionAdventureDetailValidation.IsOpaqueIdentity(adventureId))
        {
            throw new ArgumentException("The Adventure identity is invalid.", nameof(adventureId));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            AdventureDetailPrefix + Uri.EscapeDataString(adventureId));
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        var headerSupportId = ReadSupportId(response);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await TryReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
            throw new CompanionAdventureDetailApiException(response.StatusCode, problem, headerSupportId);
        }

        try
        {
            if (response.Content.Headers.ContentLength > CompanionContractLimits.MaximumJsonResponseBytes)
            {
                throw new CompanionAdventureDetailMalformedException(headerSupportId);
            }

            await response.Content.LoadIntoBufferAsync(
                CompanionContractLimits.MaximumJsonResponseBytes,
                cancellationToken).ConfigureAwait(false);
            var dto = await response.Content.ReadFromJsonAsync(
                    CompanionJsonSerializerContext.Default.CompanionAdventureDto,
                    cancellationToken).ConfigureAwait(false)
                ?? throw new CompanionAdventureDetailMalformedException(headerSupportId);
            return new(dto, response.Headers.ETag?.ToString(), headerSupportId);
        }
        catch (CompanionAdventureDetailMalformedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or HttpRequestException)
        {
            throw new CompanionAdventureDetailMalformedException(headerSupportId);
        }
    }

    private static string? ReadSupportId(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-Support-Id", out var values))
        {
            return null;
        }

        var boundedValues = values.Take(2).ToArray();
        return boundedValues.Length == 1 ? boundedValues[0] : null;
    }

    private static async Task<CompanionProblemDto?> TryReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            if (response.Content.Headers.ContentLength > CompanionContractLimits.MaximumJsonResponseBytes)
            {
                return null;
            }

            await response.Content.LoadIntoBufferAsync(
                CompanionContractLimits.MaximumJsonResponseBytes,
                cancellationToken).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync(
                CompanionJsonSerializerContext.Default.CompanionProblemDto,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or HttpRequestException)
        {
            return null;
        }
    }
}

/// <summary>Represents a safe detail API failure without retaining response content.</summary>
public sealed class CompanionAdventureDetailApiException : Exception
{
    /// <summary>Initializes the safe failure.</summary>
    public CompanionAdventureDetailApiException(
        HttpStatusCode statusCode,
        CompanionProblemDto? problem,
        string? headerSupportId)
        : base("The Companion Adventure detail request failed.")
    {
        StatusCode = statusCode;
        Problem = problem;
        HeaderSupportId = headerSupportId;
    }

    /// <summary>Gets the HTTP status.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Gets the allowlisted safe problem, when readable.</summary>
    public CompanionProblemDto? Problem { get; }

    /// <summary>Gets the safe support header, when returned.</summary>
    public string? HeaderSupportId { get; }
}

/// <summary>Represents malformed or unsupported detail data without retaining its body.</summary>
public sealed class CompanionAdventureDetailMalformedException : Exception
{
    /// <summary>Initializes the safe malformed response.</summary>
    public CompanionAdventureDetailMalformedException(string? supportId)
        : base("The Companion Adventure detail response is malformed or unsupported.") => SupportId = supportId;

    /// <summary>Gets the safe support header, when returned.</summary>
    public string? SupportId { get; }
}

/// <summary>Loads one validated, mobile-safe Adventure overview.</summary>
public interface ICompanionAdventureDetailService
{
    /// <summary>Loads one Adventure detail projection.</summary>
    Task<CompanionAdventureDetailResult> LoadAsync(
        string adventureId,
        CancellationToken cancellationToken = default);
}

/// <summary>Provides the typed read-only Adventure-detail vertical.</summary>
public sealed class CompanionAdventureDetailService(ICompanionAdventureDetailTransport transport)
    : ICompanionAdventureDetailService
{
    /// <inheritdoc />
    public async Task<CompanionAdventureDetailResult> LoadAsync(
        string adventureId,
        CancellationToken cancellationToken = default)
    {
        if (!CompanionAdventureDetailValidation.IsOpaqueIdentity(adventureId))
        {
            return CompanionAdventureDetailResult.InvalidRequest();
        }

        try
        {
            var response = await transport.GetAsync(adventureId, cancellationToken).ConfigureAwait(false);
            if (!CompanionAdventureDetailValidation.TryMap(adventureId, response, out var detail))
            {
                return CompanionAdventureDetailResult.Malformed(response.HeaderSupportId);
            }

            return CompanionAdventureDetailResult.Success(detail!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CompanionAdventureDetailMalformedException exception)
        {
            return CompanionAdventureDetailResult.Malformed(exception.SupportId);
        }
        catch (CompanionAdventureDetailApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return CompanionAdventureDetailResult.NotFound(exception.Problem, exception.HeaderSupportId);
        }
        catch (CompanionAdventureDetailApiException exception)
            when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return CompanionAdventureDetailResult.Unauthorized(
                CompanionAdventureDetailValidation.SafeSupportId(exception.HeaderSupportId)
                ?? CompanionAdventureDetailValidation.SafeSupportId(exception.Problem?.SupportId));
        }
        catch (CompanionAdventureDetailApiException exception)
        {
            var problem = exception.Problem?.Status == (int)exception.StatusCode ? exception.Problem : null;
            return CompanionAdventureDetailResult.Error(problem, exception.HeaderSupportId);
        }
        catch (HttpRequestException)
        {
            return CompanionAdventureDetailResult.Unavailable();
        }
        catch (TaskCanceledException)
        {
            return CompanionAdventureDetailResult.Unavailable();
        }
    }
}

/// <summary>Identifies every expected Adventure-detail load outcome.</summary>
public enum CompanionAdventureDetailState
{
    /// <summary>A validated detail projection is available.</summary>
    Success,
    /// <summary>The supplied opaque identifier is invalid.</summary>
    InvalidRequest,
    /// <summary>The resource is unknown or inaccessible.</summary>
    NotFound,
    /// <summary>The caller must authenticate or regain access.</summary>
    Unauthorized,
    /// <summary>The API cannot currently be reached.</summary>
    Unavailable,
    /// <summary>The response is malformed or unsupported.</summary>
    MalformedOrUnsupported,
    /// <summary>The API returned another safe failure.</summary>
    Error
}

/// <summary>Contains one detail outcome and only allowlisted safe metadata.</summary>
public sealed record CompanionAdventureDetailResult(
    CompanionAdventureDetailState State,
    MobileCompanionAdventureDetail? Adventure = null,
    string? ErrorCode = null,
    string? ErrorTitle = null,
    string? SupportId = null,
    bool Retryable = false,
    int? RetryAfterSeconds = null)
{
    /// <summary>Creates a successful result.</summary>
    public static CompanionAdventureDetailResult Success(MobileCompanionAdventureDetail detail) =>
        new(CompanionAdventureDetailState.Success, detail);

    /// <summary>Creates an invalid-request result without making a request.</summary>
    public static CompanionAdventureDetailResult InvalidRequest() =>
        new(CompanionAdventureDetailState.InvalidRequest, ErrorCode: "invalid_request");

    /// <summary>Creates an enumeration-safe unavailable-resource result.</summary>
    public static CompanionAdventureDetailResult NotFound(CompanionProblemDto? problem, string? headerSupportId) =>
        FromProblem(
            CompanionAdventureDetailState.NotFound,
            problem is { Status: 404, Code: "resource_unavailable" } ? problem : null,
            headerSupportId,
            "resource_unavailable");

    /// <summary>Creates an authorization result without exposing a response body.</summary>
    public static CompanionAdventureDetailResult Unauthorized(string? supportId) =>
        new(CompanionAdventureDetailState.Unauthorized, SupportId: supportId);

    /// <summary>Creates a network-unavailable result.</summary>
    public static CompanionAdventureDetailResult Unavailable() =>
        new(CompanionAdventureDetailState.Unavailable);

    /// <summary>Creates a malformed-response result.</summary>
    public static CompanionAdventureDetailResult Malformed(string? supportId) =>
        new(CompanionAdventureDetailState.MalformedOrUnsupported,
            ErrorCode: "unsupported_projection",
            ErrorTitle: "The Adventure detail response is unavailable.",
            SupportId: CompanionAdventureDetailValidation.SafeSupportId(supportId));

    /// <summary>Creates a safe API-error result.</summary>
    public static CompanionAdventureDetailResult Error(CompanionProblemDto? problem, string? headerSupportId) =>
        FromProblem(CompanionAdventureDetailState.Error, problem, headerSupportId, "companion_request_failed");

    private static CompanionAdventureDetailResult FromProblem(
        CompanionAdventureDetailState state,
        CompanionProblemDto? problem,
        string? headerSupportId,
        string fallbackCode)
    {
        var safeProblem = CompanionAdventureDetailValidation.IsSafeProblem(problem) ? problem : null;
        return new(
            state,
            ErrorCode: safeProblem?.Code ?? fallbackCode,
            ErrorTitle: safeProblem?.Title,
            SupportId: CompanionAdventureDetailValidation.SafeSupportId(headerSupportId)
                ?? CompanionAdventureDetailValidation.SafeSupportId(safeProblem?.SupportId),
            Retryable: safeProblem?.Retryable ?? false,
            RetryAfterSeconds: safeProblem?.RetryAfterSeconds);
    }
}

/// <summary>Contains explicitly mapped mobile-safe Adventure overview fields.</summary>
public sealed record MobileCompanionAdventureDetail(
    string AdventureId,
    string Title,
    string? Subtitle,
    string Description,
    CompanionAdventureStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    string PrimaryTimeZone,
    MobileCompanionCountdown Countdown,
    IReadOnlyList<MobileCompanionDestination> Destinations,
    string? NextItemSummary,
    string ReadinessSummary,
    IReadOnlyDictionary<string, string> CapabilityLinks,
    string InformationProfileVersion,
    string SchemaVersion,
    string ProjectionVersion,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset FreshUntilUtc,
    string? SyncCursor,
    string SupportId,
    string? ETag);

/// <summary>Contains validated countdown inputs.</summary>
public sealed record MobileCompanionCountdown(
    DateOnly TargetDate,
    TimeOnly? TargetLocalTime,
    string TimeZone,
    DateTimeOffset EvaluatedAtUtc,
    CompanionCountdownState State);

/// <summary>Contains validated destination-list fields.</summary>
public sealed record MobileCompanionDestination(
    string DestinationVisitId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone,
    int Sequence,
    MobileCompanionResource? HeroResource);

/// <summary>Contains allowlisted protected-Resource display metadata, never protected bytes.</summary>
public sealed record MobileCompanionResource(
    string ResourceId,
    string MediaType,
    long? ByteLength,
    string Title,
    string? AlternativeText,
    string? Attribution,
    CompanionResourceAvailability Availability,
    bool OfflineEligible,
    DateTimeOffset? RetainUntilUtc,
    string? ContentPath);

internal static class CompanionAdventureDetailValidation
{
    public static bool IsOpaqueIdentity(string? value) =>
        value is { Length: >= 1 and <= CompanionContractLimits.MaximumIdentityLength }
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or ':' or '-');

    public static bool TryMap(
        string requestedAdventureId,
        CompanionAdventureDetailTransportResponse response,
        out MobileCompanionAdventureDetail? result)
    {
        result = null;
        var source = response.Adventure;
        if (!string.Equals(requestedAdventureId, source.AdventureId, StringComparison.Ordinal)
            || source.SchemaVersion != "1.0"
            || !IsBounded(source.ProjectionVersion, 64)
            || !IsUtc(source.GeneratedAtUtc)
            || !IsUtc(source.FreshUntilUtc)
            || source.FreshUntilUtc < source.GeneratedAtUtc
            || !IsOptionalBounded(source.SyncCursor, 2048)
            || SafeSupportId(source.SupportId) is null
            || SafeSupportId(response.HeaderSupportId) is null && response.HeaderSupportId is not null
            || response.HeaderSupportId is not null
                && !string.Equals(response.HeaderSupportId, source.SupportId, StringComparison.Ordinal)
            || response.ETag is not null && !IsSafeETag(response.ETag)
            || !IsBounded(source.Title, 200)
            || !IsOptionalBounded(source.Subtitle, 300)
            || !IsBounded(source.Description, 2000)
            || !Enum.IsDefined(source.Status)
            || source.EndDate < source.StartDate
            || !IsIanaTimeZone(source.PrimaryTimeZone)
            || !TryMapCountdown(source.Countdown, out var countdown)
            || countdown!.TargetDate != source.StartDate
            || source.Destinations is null
            || source.Destinations.Count > 100
            || !TryMapDestinations(source.Destinations, source.StartDate, source.EndDate, out var destinations)
            || !IsOptionalBounded(source.NextItemSummary, 300)
            || !IsBounded(source.ReadinessSummary, 300)
            || !IsBounded(source.InformationProfileVersion, 64)
            || !TryMapLinks(source.CapabilityLinks, out var capabilityLinks))
        {
            return false;
        }

        result = new(
            source.AdventureId,
            source.Title,
            source.Subtitle,
            source.Description,
            source.Status,
            source.StartDate,
            source.EndDate,
            source.PrimaryTimeZone,
            countdown!,
            destinations!,
            source.NextItemSummary,
            source.ReadinessSummary,
            capabilityLinks!,
            source.InformationProfileVersion,
            source.SchemaVersion,
            source.ProjectionVersion,
            source.GeneratedAtUtc,
            source.FreshUntilUtc,
            source.SyncCursor,
            source.SupportId,
            response.ETag);
        return true;
    }

    public static string? SafeSupportId(string? value) =>
        IsSafeAscii(value, 128) ? value : null;

    public static bool IsSafeProblem(CompanionProblemDto? problem) =>
        problem is not null
        && problem.Type.Scheme == Uri.UriSchemeHttps
        && IsBounded(problem.Title, 300)
        && problem.Status is >= 400 and <= 599
        && IsSafeProblemCode(problem.Code)
        && SafeSupportId(problem.SupportId) is not null
        && problem.RetryAfterSeconds is null or >= 1 and <= 86400;

    private static bool TryMapCountdown(CompanionCountdownDto? source, out MobileCompanionCountdown? result)
    {
        result = null;
        if (source is null
            || !IsIanaTimeZone(source.TimeZone)
            || !IsUtc(source.EvaluatedAtUtc)
            || !Enum.IsDefined(source.State))
        {
            return false;
        }

        result = new(source.TargetDate, source.TargetLocalTime, source.TimeZone, source.EvaluatedAtUtc, source.State);
        return true;
    }

    private static bool TryMapDestinations(
        IReadOnlyList<CompanionDestinationSummaryDto> sources,
        DateOnly adventureStartDate,
        DateOnly adventureEndDate,
        out IReadOnlyList<MobileCompanionDestination>? result)
    {
        result = null;
        var destinations = new List<MobileCompanionDestination>(sources.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var expectedSequence = 1;
        foreach (var source in sources)
        {
            if (source is null
                || !IsOpaqueIdentity(source.DestinationVisitId)
                || !identities.Add(source.DestinationVisitId)
                || !IsBounded(source.Name, 200)
                || source.EndDate < source.StartDate
                || source.StartDate < adventureStartDate
                || source.EndDate > adventureEndDate
                || !IsIanaTimeZone(source.TimeZone)
                || source.Sequence != expectedSequence++
                || !TryMapResource(source.HeroResource, out var heroResource))
            {
                return false;
            }

            destinations.Add(new(
                source.DestinationVisitId,
                source.Name,
                source.StartDate,
                source.EndDate,
                source.TimeZone,
                source.Sequence,
                heroResource));
        }

        result = destinations;
        return true;
    }

    private static bool TryMapResource(CompanionResourceSummaryDto? source, out MobileCompanionResource? result)
    {
        result = null;
        if (source is null)
        {
            return true;
        }

        if (!IsOpaqueIdentity(source.ResourceId)
            || !IsBounded(source.MediaType, 127)
            || source.ByteLength is < 0
            || !IsBounded(source.Title, 200)
            || !IsOptionalBounded(source.AlternativeText, 500)
            || !IsOptionalBounded(source.Attribution, 300)
            || !Enum.IsDefined(source.Availability)
            || source.RetainUntilUtc is not null && !IsUtc(source.RetainUntilUtc.Value)
            || source.ContentPath is not null && !IsSafeRelativeApiPath(source.ContentPath))
        {
            return false;
        }

        result = new(
            source.ResourceId,
            source.MediaType,
            source.ByteLength,
            source.Title,
            source.AlternativeText,
            source.Attribution,
            source.Availability,
            source.OfflineEligible,
            source.RetainUntilUtc,
            source.ContentPath);
        return true;
    }

    private static bool TryMapLinks(
        IReadOnlyDictionary<string, string>? sources,
        out IReadOnlyDictionary<string, string>? result)
    {
        result = null;
        if (sources is null || sources.Count > 20)
        {
            return false;
        }

        var links = new Dictionary<string, string>(sources.Count, StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (!IsSafeAscii(source.Key, 64) || !IsSafeRelativeApiPath(source.Value))
            {
                return false;
            }

            links.Add(source.Key, source.Value);
        }

        result = links;
        return true;
    }

    private static bool IsSafeRelativeApiPath(string value)
    {
        if (value.Length is < 1 or > 2048
            || !value.StartsWith("/v1/companion/", StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('#', StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var path = Uri.UnescapeDataString(value.Split('?', 2)[0]);
            return !path.Split('/').Any(segment => segment is "." or "..");
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool IsIanaTimeZone(string? value)
    {
        if (!IsBounded(value, 100)
            || value!.Contains('\\')
            || value.Any(character => !(char.IsAsciiLetterOrDigit(character)
                || character is '/' or '_' or '+' or '-'))
            || !value.Contains('/'))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value!);
            return !value!.StartsWith("Custom/", StringComparison.Ordinal);
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;

    private static bool IsSafeETag(string value) =>
        value.Length <= 256 && EntityTagHeaderValue.TryParse(value, out _);

    private static bool IsBounded(string? value, int maximumRunes) =>
        !string.IsNullOrWhiteSpace(value)
        && value.EnumerateRunes().Count() <= maximumRunes;

    private static bool IsOptionalBounded(string? value, int maximumRunes) =>
        value is null || value.EnumerateRunes().Count() <= maximumRunes;

    private static bool IsSafeAscii(string? value, int maximumLength) =>
        value is { Length: >= 1 } && value.Length <= maximumLength
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or ':' or '-');

    private static bool IsSafeProblemCode(string? value) =>
        value is { Length: >= 1 and <= 64 }
        && char.IsAsciiLetter(value[0]) && char.IsLower(value[0])
        && value.All(character => character is >= 'a' and <= 'z' || char.IsAsciiDigit(character) || character == '_');
}
