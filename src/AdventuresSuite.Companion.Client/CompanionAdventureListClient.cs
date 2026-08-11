using System.Net;
using System.Net.Http.Json;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Client;

/// <summary>Reads the authorized Adventure collection from the Companion API.</summary>
public interface ICompanionAdventureListTransport
{
    /// <summary>Gets the current traveler's authorized Adventure collection.</summary>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The typed API collection.</returns>
    Task<CompanionAdventureCollectionDto> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Uses the versioned Companion JSON contract to read Adventures over HTTP.</summary>
public sealed class HttpCompanionAdventureListTransport(HttpClient httpClient) : ICompanionAdventureListTransport
{
    private const string AdventureListPath = "v1/companion/adventures";

    /// <inheritdoc />
    public async Task<CompanionAdventureCollectionDto> ListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            AdventureListPath,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await TryReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
            throw new CompanionApiException(response.StatusCode, problem);
        }

        return await response.Content.ReadFromJsonAsync(
                CompanionJsonSerializerContext.Default.CompanionAdventureCollectionDto,
                cancellationToken).ConfigureAwait(false)
            ?? throw new CompanionApiException(HttpStatusCode.BadGateway, null);
    }

    private static async Task<CompanionProblemDto?> TryReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(
                CompanionJsonSerializerContext.Default.CompanionProblemDto,
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

/// <summary>Represents a safe Companion API failure without retaining response content.</summary>
public sealed class CompanionApiException : Exception
{
    /// <summary>Initializes a safe API failure.</summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="problem">The allowlisted problem, when readable.</param>
    public CompanionApiException(HttpStatusCode statusCode, CompanionProblemDto? problem)
        : base("The Companion API request failed.")
    {
        StatusCode = statusCode;
        Problem = problem;
    }

    /// <summary>Gets the HTTP status.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Gets the allowlisted problem fields, when supplied.</summary>
    public CompanionProblemDto? Problem { get; }
}

/// <summary>Loads presentation-safe Adventure summaries for a mobile consumer.</summary>
public interface ICompanionAdventureListService
{
    /// <summary>Loads the authorized Adventure list.</summary>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>A bounded, explicit load result.</returns>
    Task<CompanionAdventureListResult> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Provides the typed, read-only Adventure-list vertical.</summary>
public sealed class CompanionAdventureListService(ICompanionAdventureListTransport transport) : ICompanionAdventureListService
{
    /// <inheritdoc />
    public async Task<CompanionAdventureListResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await transport.ListAsync(cancellationToken).ConfigureAwait(false);
            var adventures = response.Adventures.Select(Map).ToArray();
            return new(
                adventures.Length == 0 ? CompanionAdventureListState.Empty : CompanionAdventureListState.Success,
                adventures,
                response.GeneratedAtUtc,
                response.FreshUntilUtc,
                response.SupportId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CompanionApiException exception) when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return CompanionAdventureListResult.Unauthorized();
        }
        catch (CompanionApiException exception)
        {
            return CompanionAdventureListResult.Error(exception.Problem);
        }
        catch (HttpRequestException)
        {
            return CompanionAdventureListResult.Unavailable();
        }
        catch (TaskCanceledException)
        {
            return CompanionAdventureListResult.Unavailable();
        }
    }

    private static CompanionAdventureListItem Map(CompanionAdventureSummaryDto source) => new(
        source.AdventureId,
        source.Title,
        source.Subtitle,
        source.Status,
        source.StartDate,
        source.EndDate,
        source.PrimaryTimeZone,
        source.Countdown.TargetDate,
        source.Countdown.TargetLocalTime,
        source.Countdown.TimeZone,
        source.Countdown.EvaluatedAtUtc,
        source.Countdown.State,
        source.OfflineState);
}

/// <summary>Contains the explicit fields the mobile Adventure list may present.</summary>
public sealed record CompanionAdventureListItem(
    string AdventureId,
    string Title,
    string? Subtitle,
    CompanionAdventureStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    string PrimaryTimeZone,
    DateOnly CountdownTargetDate,
    TimeOnly? CountdownTargetLocalTime,
    string CountdownTimeZone,
    DateTimeOffset CountdownEvaluatedAtUtc,
    CompanionCountdownState CountdownState,
    CompanionOfflineState OfflineState);

/// <summary>Identifies every expected Adventure-list outcome.</summary>
public enum CompanionAdventureListState
{
    /// <summary>The list contains one or more Adventures.</summary>
    Success,
    /// <summary>The authorized list is empty.</summary>
    Empty,
    /// <summary>The API cannot currently be reached.</summary>
    Unavailable,
    /// <summary>The caller must authenticate or regain access.</summary>
    Unauthorized,
    /// <summary>The API returned a safe failure.</summary>
    Error
}

/// <summary>Contains an Adventure-list result without transport or server internals.</summary>
public sealed record CompanionAdventureListResult(
    CompanionAdventureListState State,
    IReadOnlyList<CompanionAdventureListItem> Adventures,
    DateTimeOffset? GeneratedAtUtc = null,
    DateTimeOffset? FreshUntilUtc = null,
    string? SupportId = null,
    string? ErrorCode = null,
    string? ErrorTitle = null,
    bool Retryable = false)
{
    /// <summary>Creates an unavailable result.</summary>
    public static CompanionAdventureListResult Unavailable() => new(CompanionAdventureListState.Unavailable, []);

    /// <summary>Creates an authorization result without exposing a problem body.</summary>
    public static CompanionAdventureListResult Unauthorized() => new(CompanionAdventureListState.Unauthorized, []);

    /// <summary>Creates a safe error result from allowlisted problem fields.</summary>
    /// <param name="problem">The safe problem, when readable.</param>
    public static CompanionAdventureListResult Error(CompanionProblemDto? problem) => new(
        CompanionAdventureListState.Error,
        [],
        SupportId: problem?.SupportId,
        ErrorCode: problem?.Code ?? "companion_request_failed",
        ErrorTitle: problem?.Title ?? "The Adventure list could not be loaded.",
        Retryable: problem?.Retryable ?? false);
}
