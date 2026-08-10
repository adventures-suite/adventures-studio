using System.Net;
using System.Text.Json;
using AdventuresSuite.Companion.Application;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Api.Tests;

/// <summary>Exercises the deterministic HTTP and JSON contract.</summary>
public sealed class CompanionEndpointContractTests(CompanionApiFactory factory)
    : IClassFixture<CompanionApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>Ensures all six JSON projections and the closed Resource operation are reachable.</summary>
    [Fact]
    public async Task SevenReadOperationsExposeExpectedFoundationBehavior()
    {
        var routes = new[]
        {
            "/api/v1/companion/adventures",
            $"/api/v1/companion/adventures/{DeterministicCompanionProjectionService.ItalyAdventureId}",
            $"/api/v1/companion/adventures/{DeterministicCompanionProjectionService.ItalyAdventureId}/today",
            $"/api/v1/companion/adventures/{DeterministicCompanionProjectionService.ItalyAdventureId}/itinerary",
            $"/api/v1/companion/adventures/{DeterministicCompanionProjectionService.ItalyAdventureId}/readiness",
            $"/api/v1/companion/adventures/{DeterministicCompanionProjectionService.ItalyAdventureId}/playbook"
        };
        foreach (var route in routes)
        {
            using var response = await _client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.Contains("ETag"));
            Assert.True(response.Headers.Contains("X-Support-Id"));
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.InRange(bytes.Length, 1, CompanionContractLimits.MaximumJsonResponseBytes);
        }

        using var resource = await _client.GetAsync("/api/v1/companion/resources/res_demo_spain_hero/content");
        Assert.Equal(HttpStatusCode.NotFound, resource.StatusCode);
        Assert.Equal("application/problem+json", resource.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Ensures list ordering, status mapping, and completed-history behavior are deterministic.</summary>
    [Fact]
    public async Task AdventureCollectionIsDeterministicAndBounded()
    {
        var json = await _client.GetStringAsync("/api/v1/companion/adventures");
        var dto = JsonSerializer.Deserialize(json, CompanionJsonSerializerContext.Default.CompanionAdventureCollectionDto);
        Assert.NotNull(dto);
        Assert.Equal("1.0", dto.SchemaVersion);
        Assert.Equal(3, dto.Adventures.Count);
        Assert.Equal(CompanionAdventureStatus.InProgress, dto.Adventures[0].Status);
        Assert.DoesNotContain(dto.Adventures, value => value.Status == CompanionAdventureStatus.Completed);

        var history = await _client.GetStringAsync("/api/v1/companion/adventures?includeCompleted=true&limit=100");
        var historyDto = JsonSerializer.Deserialize(history, CompanionJsonSerializerContext.Default.CompanionAdventureCollectionDto);
        Assert.Contains(historyDto!.Adventures, value => value.Status == CompanionAdventureStatus.Completed);

        using var invalid = await _client.GetAsync("/api/v1/companion/adventures?limit=101");
        await AssertSafeProblemAsync(invalid, HttpStatusCode.BadRequest, "invalid_request");
    }

    /// <summary>Ensures conditional reads avoid retransmitting unchanged projections.</summary>
    [Fact]
    public async Task MatchingEtagReturnsBodylessNotModified()
    {
        using var initial = await _client.GetAsync("/api/v1/companion/adventures");
        var etag = Assert.Single(initial.Headers.GetValues("ETag"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/companion/adventures");
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
        using var anonymousRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/companion/adventures");
        anonymousRequest.Headers.Add("X-Companion-Test-Anonymous", "true");
        using var anonymous = await _client.SendAsync(anonymousRequest);
        await AssertSafeProblemAsync(anonymous, HttpStatusCode.Unauthorized, "authentication_required");

        using var scopeRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/companion/adventures");
        scopeRequest.Headers.Add("X-Companion-Test-Scope", "Wrong.Scope");
        using var wrongScope = await _client.SendAsync(scopeRequest);
        await AssertSafeProblemAsync(wrongScope, HttpStatusCode.Forbidden, "insufficient_scope");
    }

    /// <summary>Ensures IDOR, isolation, revocation, and unknown identities are indistinguishable.</summary>
    [Fact]
    public async Task IsolationFailuresAreEnumerationSafe()
    {
        var requests = new[]
        {
            RequestWith("X-Companion-Test-Creator", "creator_demo_other"),
            RequestWith("X-Companion-Test-Traveler", "trav_demo_other"),
            RequestWith("X-Companion-Test-Revoked", "true"),
            new HttpRequestMessage(HttpMethod.Get, "/api/v1/companion/adventures/adv_demo_unknown")
        };
        foreach (var request in requests)
        {
            using var ownedRequest = request;
            using var response = await _client.SendAsync(request);
            var problem = await ReadProblemAsync(response);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("resource_unavailable", problem.Code);
            Assert.DoesNotContain("creator", problem.Title, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("traveler", problem.Title, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("adv_demo", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>Ensures malformed opaque identities fail before provider evaluation.</summary>
    [Fact]
    public async Task MalformedIdentityReturnsBoundedValidationProblem()
    {
        using var response = await _client.GetAsync("/api/v1/companion/adventures/not%20valid");
        await AssertSafeProblemAsync(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    private static HttpRequestMessage RequestWith(string header, string value)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/companion/adventures/{DeterministicCompanionProjectionService.ItalyAdventureId}");
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
