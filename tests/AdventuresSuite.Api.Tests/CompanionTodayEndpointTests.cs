using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdventuresSuite.Companion.Application;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Api.Tests;

/// <summary>Exercises the deterministic traveler-specific Today and Next endpoint.</summary>
public sealed class CompanionTodayEndpointTests(CompanionApiFactory factory)
    : IClassFixture<CompanionApiFactory>
{
    private const string CurrentRoute = "/v1/companion/adventures/adv_demo_italy_2026/today";
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>Ensures current Today and Next data preserves local-time semantics and safe metadata.</summary>
    [Fact]
    public async Task CurrentTodayIsExplicitlyMappedVersionedAndMinimized()
    {
        using var response = await _client.GetAsync(CurrentRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.MustRevalidate);
        Assert.Equal(TimeSpan.Zero, response.Headers.CacheControl?.MaxAge);
        Assert.True(response.Headers.Contains("ETag"));
        Assert.True(response.Headers.Contains("X-Support-Id"));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.InRange(bytes.Length, 1, CompanionContractLimits.MaximumJsonResponseBytes);

        var dto = JsonSerializer.Deserialize(bytes, CompanionJsonSerializerContext.Default.CompanionTodayDto);
        Assert.NotNull(dto);
        Assert.Equal("1.0", dto.SchemaVersion);
        Assert.Equal("adv_demo_italy_2026", dto.AdventureId);
        Assert.Equal(new DateOnly(2026, 8, 10), dto.LocalDate);
        Assert.Equal("Europe/Rome", dto.TimeZone);
        Assert.Equal(CompanionTodayState.Active, dto.State);
        Assert.Equal(TimeSpan.Zero, dto.GeneratedAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, dto.FreshUntilUtc.Offset);
        Assert.Equal(TimeSpan.FromMinutes(5), dto.FreshUntilUtc - dto.GeneratedAtUtc);

        Assert.Collection(
            dto.TodayItems,
            changed =>
            {
                Assert.Equal(CompanionTimeStatus.Scheduled, changed.TimeStatus);
                Assert.Equal(CompanionOperationalStatus.Changed, changed.OperationalStatus);
                Assert.Equal(new TimeOnly(9, 0), changed.StartLocalTime);
                Assert.Equal(new TimeOnly(11, 0), changed.EndLocalTime);
                Assert.True(changed.RequiresAcknowledgment);
                Assert.Equal("Review change", changed.ActionLabel);
                Assert.Null(changed.ActionPath);
            },
            allDay =>
            {
                Assert.Equal(CompanionTimeStatus.AllDay, allDay.TimeStatus);
                Assert.Equal(CompanionOperationalStatus.Confirmed, allDay.OperationalStatus);
                Assert.Null(allDay.StartLocalTime);
                Assert.Null(allDay.EndLocalTime);
            });
        var next = Assert.IsType<CompanionScheduleItemDto>(dto.NextItem);
        Assert.Equal("Rail to Florence", next.Title);
        Assert.Equal(new DateOnly(2026, 8, 12), next.LocalDate);
        Assert.Equal(new TimeOnly(10, 30), next.StartLocalTime);
        Assert.Equal(CompanionOperationalStatus.Confirmed, next.OperationalStatus);
        Assert.Empty(next.Resources);

        var json = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"localDate\":\"2026-08-10\"", json, StringComparison.Ordinal);
        Assert.Contains("\"startLocalTime\":\"09:00:00\"", json, StringComparison.Ordinal);
        Assert.Contains("\"generatedAtUtc\":\"2026-08-10T10:00:00+00:00\"", json, StringComparison.Ordinal);
        foreach (var prohibited in new[]
        {
            "confirmationNumber", "bookingReference", "ticket", "privateNote", "providerToken",
            "accessToken", "refreshToken", "contentPath", "https://", "latitude", "longitude"
        })
        {
            Assert.DoesNotContain(prohibited, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Ensures future TBC and cancelled schedule states remain distinct without invented times.</summary>
    [Fact]
    public async Task FutureTodayPreservesTbcAndCancelledSemantics()
    {
        var cases = new[]
        {
            ("adv_demo_spain_2027", CompanionTimeStatus.ToBeConfirmed, CompanionOperationalStatus.Proposed),
            ("adv_demo_phoenix_coast_2027", CompanionTimeStatus.Cancelled, CompanionOperationalStatus.Cancelled)
        };

        foreach (var item in cases)
        {
            var dto = await _client.GetFromJsonAsync(
                $"/v1/companion/adventures/{item.Item1}/today",
                CompanionJsonSerializerContext.Default.CompanionTodayDto);

            Assert.NotNull(dto);
            Assert.Equal(CompanionTodayState.BeforeAdventure, dto.State);
            Assert.Empty(dto.TodayItems);
            Assert.Equal(item.Item2, dto.NextItem?.TimeStatus);
            Assert.Equal(item.Item3, dto.NextItem?.OperationalStatus);
            Assert.Null(dto.NextItem?.StartLocalTime);
            Assert.Null(dto.NextItem?.EndLocalTime);
        }
    }

    /// <summary>Ensures a matching ETag returns no body after the endpoint reauthorizes the request.</summary>
    [Fact]
    public async Task MatchingEtagReturnsBodylessNotModified()
    {
        using var initial = await _client.GetAsync(CurrentRoute);
        var etag = Assert.Single(initial.Headers.GetValues("ETag"));
        using var request = new HttpRequestMessage(HttpMethod.Get, CurrentRoute);
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.MustRevalidate);
    }

    /// <summary>Ensures malformed identities are rejected before querying and case changes remain unavailable.</summary>
    [Fact]
    public async Task IdentifiersAreValidatedAndCaseSensitive()
    {
        using var malformed = await _client.GetAsync("/v1/companion/adventures/%20invalid/today");
        await AssertSafeProblemAsync(malformed, HttpStatusCode.BadRequest, "invalid_request");

        using var caseAltered = await _client.GetAsync(
            "/v1/companion/adventures/ADV_demo_italy_2026/today");
        await AssertSafeProblemAsync(caseAltered, HttpStatusCode.NotFound, "resource_unavailable");
    }

    /// <summary>Ensures authentication, scope, ownership, participation, and revocation fail safely.</summary>
    [Fact]
    public async Task AuthorizationAndIsolationFailuresAreEnumerationSafe()
    {
        using var unknown = await _client.GetAsync("/v1/companion/adventures/adv_demo_unknown/today");
        var unknownProblem = await ReadProblemAsync(unknown);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var unavailableRequests = new[]
        {
            RequestWith("X-Companion-Test-User", "usr_demo_other"),
            RequestWith("X-Companion-Test-Creator", "creator_demo_other"),
            RequestWith("X-Companion-Test-Traveler", "trav_demo_other"),
            RequestWith("X-Companion-Test-Membership-Version", "6"),
            RequestWith("X-Companion-Test-Revoked", "true")
        };
        foreach (var request in unavailableRequests)
        {
            using var ownedRequest = request;
            using var response = await _client.SendAsync(request);
            var problem = await ReadProblemAsync(response);
            Assert.Equal(unknown.StatusCode, response.StatusCode);
            Assert.Equal(unknownProblem.Code, problem.Code);
            Assert.Equal(unknownProblem.Title, problem.Title);
        }

        using var anonymousRequest = RequestWith("X-Companion-Test-Anonymous", "true");
        using var anonymous = await _client.SendAsync(anonymousRequest);
        await AssertSafeProblemAsync(anonymous, HttpStatusCode.Unauthorized, "authentication_required");

        using var scopeRequest = RequestWith("X-Companion-Test-Scope", "Wrong.Scope");
        using var wrongScope = await _client.SendAsync(scopeRequest);
        await AssertSafeProblemAsync(wrongScope, HttpStatusCode.Forbidden, "insufficient_scope");
    }

    private static HttpRequestMessage RequestWith(string header, string value)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, CurrentRoute);
        request.Headers.Add(header, value);
        return request;
    }

    private static async Task AssertSafeProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(code, problem.Code);
        Assert.Equal((int)status, problem.Status);
        Assert.StartsWith("req_", problem.SupportId, StringComparison.Ordinal);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("traveler", body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<CompanionProblemDto> ReadProblemAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize(
            await response.Content.ReadAsStringAsync(),
            CompanionJsonSerializerContext.Default.CompanionProblemDto)!;
}

/// <summary>Verifies fail-closed explicit mapping of provider-neutral Today projections.</summary>
public sealed class CompanionTodayMappingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Ensures unknown closed states, invalid zones, and inconsistent dates fail closed.</summary>
    [Fact]
    public void UnknownAndInconsistentProjectionDataFailsClosed()
    {
        var valid = Projection();
        var invalid = new[]
        {
            valid with { State = (CompanionTodayProjectionState)999 },
            valid with { TimeZone = "Not/AZone" },
            valid with { LocalDate = new DateOnly(2026, 8, 11) },
            valid with { Adventure = valid.Adventure with { Lifecycle = (CompanionAdventureLifecycle)999 } },
            valid with { Adventure = valid.Adventure with { EndDate = new DateOnly(2026, 8, 8) } },
            valid with { Adventure = valid.Adventure with { UpdatedAtUtc = Now.ToOffset(TimeSpan.FromHours(2)) } },
            valid with { TodayItems = [Item() with { TimeState = (CompanionScheduleTimeState)999 }] },
            valid with { TodayItems = [Item() with { OperationalState = (CompanionScheduleOperationalState)999 }] },
            valid with { TodayItems = [Item() with { StartLocalTime = null }] },
            valid with { TodayItems = [Item() with { EndLocalTime = new TimeOnly(8, 0) }] },
            valid with { TodayItems = [Item() with { OperationalState = CompanionScheduleOperationalState.Changed, RequiresAcknowledgment = false }] },
            valid with { TodayItems = [Item(), Item() with { Sequence = 2 }] },
            valid with { NextItem = Item() with { LocalDate = new DateOnly(2026, 8, 9) } }
        };

        foreach (var source in invalid)
        {
            Assert.False(CompanionDtoMapper.TryMapToday(
                source, "adv_demo", Now, "support_demo", out var result));
            Assert.Null(result);
        }
    }

    /// <summary>Ensures collection bounds are enforced before DTO construction.</summary>
    [Fact]
    public void OversizedTodayCollectionFailsClosed()
    {
        var items = Enumerable.Range(1, 251)
            .Select(index => Item() with { ItemId = $"item_{index}", Sequence = index })
            .ToArray();
        var source = Projection() with { TodayItems = items };

        Assert.False(CompanionDtoMapper.TryMapToday(
            source, "adv_demo", Now, "support_demo", out var result));
        Assert.Null(result);
    }

    private static CompanionTodayProjection Projection() => new(
        new CompanionAdventureSummaryProjection(
            "adv_demo",
            "trav_demo",
            "Fictional Adventure",
            CompanionAdventureLifecycle.InProgress,
            new DateOnly(2026, 8, 9),
            new DateOnly(2026, 8, 16),
            "Europe/Rome",
            7,
            3,
            new DateTimeOffset(2026, 8, 9, 18, 0, 0, TimeSpan.Zero)),
        "info_demo_01",
        new DateOnly(2026, 8, 10),
        "Europe/Rome",
        CompanionTodayProjectionState.Active,
        [Item()],
        Item() with { ItemId = "item_next", LocalDate = new DateOnly(2026, 8, 11), Sequence = 2 },
        "A fictional safe notice.");

    private static CompanionScheduleItemProjection Item() => new(
        "item_demo",
        "activity",
        "Fictional activity",
        "A safe summary.",
        new DateOnly(2026, 8, 10),
        new TimeOnly(9, 0),
        new TimeOnly(10, 0),
        "Europe/Rome",
        CompanionScheduleTimeState.Scheduled,
        CompanionScheduleOperationalState.Confirmed,
        "Central Rome",
        null,
        1,
        false);
}
