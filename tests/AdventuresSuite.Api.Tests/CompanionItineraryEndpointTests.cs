using System.Net;
using System.Text.Json;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Api.Tests;

/// <summary>Exercises the authorized deterministic Itinerary endpoint.</summary>
public sealed class CompanionItineraryEndpointTests(CompanionApiFactory factory) : IClassFixture<CompanionApiFactory>
{
    private const string Route = "/v1/companion/adventures/adv_demo_italy_2026/itinerary";
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>Ensures ordered local Journey data is minimized and cache-safe.</summary>
    [Fact]
    public async Task ItineraryIsExplicitlyMappedOrderedAndMinimized()
    {
        using var response = await _client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.MustRevalidate);
        Assert.True(response.Headers.Contains("ETag"));
        var body = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize(body, CompanionJsonSerializerContext.Default.CompanionItineraryDto);
        Assert.NotNull(dto);
        Assert.Equal("1.0", dto.SchemaVersion);
        Assert.Equal("adv_demo_italy_2026", dto.AdventureId);
        Assert.NotEmpty(dto.Days);
        Assert.Equal(Enumerable.Range(1, dto.Days.Count), dto.Days.Select(day => day.DayNumber));
        Assert.Equal(dto.Days.OrderBy(day => day.LocalDate).Select(day => day.LocalDate), dto.Days.Select(day => day.LocalDate));
        Assert.All(dto.Days, day => Assert.Contains('/', day.TimeZone));
        foreach (var prohibited in new[] { "confirmationNumber", "bookingReference", "privateNote", "providerToken", "accessToken", "refreshToken", "latitude", "longitude", "https://" })
            Assert.DoesNotContain(prohibited, body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ensures ETags return bodyless 304 responses only after authorization.</summary>
    [Fact]
    public async Task EtagAndAuthorizationRemainBoundTogether()
    {
        using var initial = await _client.GetAsync(Route);
        var etag = Assert.Single(initial.Headers.GetValues("ETag"));
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());

        using var revoked = RequestWith("X-Companion-Test-Revoked", "true", etag);
        using var denied = await _client.SendAsync(revoked);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    /// <summary>Ensures malformed, anonymous, scope, unknown, cross-scope, stale, and revoked requests fail closed.</summary>
    [Fact]
    public async Task InvalidAndUnauthorizedRequestsFailClosed()
    {
        using var malformed = await _client.GetAsync("/v1/companion/adventures/%20invalid/itinerary");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var unknown = await _client.GetAsync("/v1/companion/adventures/adv_demo_unknown/itinerary");
        var unknownProblem = JsonSerializer.Deserialize(
            await unknown.Content.ReadAsStringAsync(), CompanionJsonSerializerContext.Default.CompanionProblemDto)!;
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var cases = new[]
        {
            ("X-Companion-Test-User", "usr_demo_other", HttpStatusCode.NotFound),
            ("X-Companion-Test-Creator", "creator_demo_other", HttpStatusCode.NotFound),
            ("X-Companion-Test-Traveler", "trav_demo_other", HttpStatusCode.NotFound),
            ("X-Companion-Test-Membership-Version", "6", HttpStatusCode.NotFound),
            ("X-Companion-Test-Revoked", "true", HttpStatusCode.NotFound),
            ("X-Companion-Test-Anonymous", "true", HttpStatusCode.Unauthorized),
            ("X-Companion-Test-Scope", "Wrong.Scope", HttpStatusCode.Forbidden)
        };
        foreach (var (header, value, expected) in cases)
        {
            using var request = RequestWith(header, value);
            using var response = await _client.SendAsync(request);
            Assert.Equal(expected, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("traveler", body, StringComparison.OrdinalIgnoreCase);
            if (expected == HttpStatusCode.NotFound)
            {
                var problem = JsonSerializer.Deserialize(body, CompanionJsonSerializerContext.Default.CompanionProblemDto)!;
                Assert.Equal(unknownProblem.Code, problem.Code);
                Assert.Equal(unknownProblem.Title, problem.Title);
            }
        }
    }

    private static HttpRequestMessage RequestWith(string header, string value, string? etag = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add(header, value);
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        return request;
    }
}
