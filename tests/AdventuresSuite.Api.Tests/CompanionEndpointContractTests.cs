using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Api.Tests;

/// <summary>Exercises the first deterministic Companion HTTP and JSON vertical slice.</summary>
public sealed class CompanionEndpointContractTests(CompanionApiFactory factory)
    : IClassFixture<CompanionApiFactory>
{
    private const string Route = "/v1/companion/adventures";
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>Ensures only the JSON collection slice is active and media delivery remains separate.</summary>
    [Fact]
    public async Task OnlyAdventureCollectionIsExposed()
    {
        using var response = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("ETag"));
        Assert.True(response.Headers.Contains("X-Support-Id"));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.InRange(bytes.Length, 1, CompanionContractLimits.MaximumJsonResponseBytes);

        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/v1/companion/adventures/adv_demo_italy_2026")).StatusCode);
        using var media = await _client.GetAsync("/v1/companion/resources/res_demo/content");
        Assert.Equal(HttpStatusCode.NotFound, media.StatusCode);
        Assert.Empty(await media.Content.ReadAsByteArrayAsync());
    }

    /// <summary>Ensures explicit source-generated JSON is deterministic and bounded.</summary>
    [Fact]
    public async Task AdventureCollectionIsDeterministicAndBounded()
    {
        var json = await _client.GetStringAsync(Route);
        var dto = JsonSerializer.Deserialize(json, CompanionJsonSerializerContext.Default.CompanionAdventureCollectionDto);
        Assert.NotNull(dto);
        Assert.Equal("1.0", dto.SchemaVersion);
        Assert.Equal(3, dto.Adventures.Count);
        Assert.Equal(CompanionAdventureStatus.InProgress, dto.Adventures[0].Status);
        Assert.DoesNotContain(dto.Adventures, value => value.Status == CompanionAdventureStatus.Completed);

        var history = await _client.GetStringAsync($"{Route}?includeCompleted=true&limit=100");
        var historyDto = JsonSerializer.Deserialize(history, CompanionJsonSerializerContext.Default.CompanionAdventureCollectionDto);
        Assert.Contains(historyDto!.Adventures, value => value.Status == CompanionAdventureStatus.Completed);

        using var invalid = await _client.GetAsync($"{Route}?limit=101");
        await AssertSafeProblemAsync(invalid, HttpStatusCode.BadRequest, "invalid_request");
    }

    /// <summary>Ensures conditional reads avoid retransmitting unchanged projections.</summary>
    [Fact]
    public async Task MatchingEtagReturnsBodylessNotModified()
    {
        using var initial = await _client.GetAsync(Route);
        var etag = Assert.Single(initial.Headers.GetValues("ETag"));
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.MustRevalidate);
        Assert.Equal(TimeSpan.Zero, response.Headers.CacheControl?.MaxAge);
    }

    /// <summary>Ensures anonymous and wrong-scope failures are safe and stable.</summary>
    [Fact]
    public async Task AuthenticationAndScopeFailuresUseSafeProblems()
    {
        using var anonymousRequest = RequestWith("X-Companion-Test-Anonymous", "true");
        using var anonymous = await _client.SendAsync(anonymousRequest);
        await AssertSafeProblemAsync(anonymous, HttpStatusCode.Unauthorized, "authentication_required");

        using var scopeRequest = RequestWith("X-Companion-Test-Scope", "Wrong.Scope");
        using var wrongScope = await _client.SendAsync(scopeRequest);
        await AssertSafeProblemAsync(wrongScope, HttpStatusCode.Forbidden, "insufficient_scope");
    }

    /// <summary>Ensures identity, Creator, current membership, and revocation failures are indistinguishable.</summary>
    [Fact]
    public async Task AuthorizationFailuresAreEnumerationSafe()
    {
        var requests = new[]
        {
            RequestWith("X-Companion-Test-User", "usr_demo_other"),
            RequestWith("X-Companion-Test-Creator", "creator_demo_other"),
            RequestWith("X-Companion-Test-Traveler", "trav_demo_other"),
            RequestWith("X-Companion-Test-Membership-Version", "6"),
            RequestWith("X-Companion-Test-Revoked", "true")
        };
        foreach (var request in requests)
        {
            using var ownedRequest = request;
            using var response = await _client.SendAsync(request);
            var problem = await ReadProblemAsync(response);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("resource_unavailable", problem.Code);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("creator", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("traveler", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("membership", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Ensures traces and metrics use only stable, low-cardinality dimensions.</summary>
    [Fact]
    public async Task OperationalSignalsExcludeIdentityAndResourceDimensions()
    {
        Activity? stopped = null;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CompanionTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped = activity
        };
        ActivitySource.AddActivityListener(activityListener);

        var measurements = new List<(string Name, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == CompanionTelemetry.MeterName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            measurements.Add((instrument.Name, tags.ToArray())));
        meterListener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            measurements.Add((instrument.Name, tags.ToArray())));
        meterListener.Start();

        using var response = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(stopped);
        Assert.Equal("companion.adventures.list", stopped.DisplayName);
        Assert.NotEmpty(measurements);
        var signalTags = stopped.Tags.Select(tag => tag.Key)
            .Concat(measurements.SelectMany(value => value.Tags.Select(tag => tag.Key)))
            .ToArray();
        Assert.Contains("operation", signalTags);
        Assert.Contains("outcome", signalTags);
        Assert.DoesNotContain(signalTags, IsSensitiveDimension);
    }

    private static bool IsSensitiveDimension(string name) =>
        name.Contains("creator", StringComparison.OrdinalIgnoreCase)
        || name.Contains("user", StringComparison.OrdinalIgnoreCase)
        || name.Contains("traveler", StringComparison.OrdinalIgnoreCase)
        || name.Contains("resource", StringComparison.OrdinalIgnoreCase)
        || name.Contains("host", StringComparison.OrdinalIgnoreCase);

    private static HttpRequestMessage RequestWith(string header, string value)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add(header, value);
        return request;
    }

    private static async Task AssertSafeProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(code, problem.Code);
        Assert.Equal((int)status, problem.Status);
        Assert.StartsWith("req_", problem.SupportId, StringComparison.Ordinal);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("exception", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<CompanionProblemDto> ReadProblemAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize(
            await response.Content.ReadAsStringAsync(),
            CompanionJsonSerializerContext.Default.CompanionProblemDto)!;
}
