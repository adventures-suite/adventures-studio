using System.Net;
using System.Text;
using System.Text.Json;
using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Client.Tests;

/// <summary>Exercises the typed, read-only Today transport and mobile-safe mapping.</summary>
public sealed class CompanionTodayServiceTests
{
    /// <summary>Ensures transport calls only the encoded versioned Today endpoint.</summary>
    [Fact]
    public async Task TransportUsesOnlyTodayEndpointAndSourceGeneratedJson()
    {
        HttpRequestMessage? observed = null;
        using var client = Client(request =>
        {
            observed = request;
            var response = JsonResponse(Today());
            response.Headers.TryAddWithoutValidation("ETag", "\"pv_today\"");
            response.Headers.TryAddWithoutValidation("X-Support-Id", "support_today");
            return response;
        });

        var response = await new HttpCompanionTodayTransport(client).GetAsync("adv:demo");

        Assert.Equal(HttpMethod.Get, observed?.Method);
        Assert.Equal("https://companion.example/v1/companion/adventures/adv%3Ademo/today", observed?.RequestUri?.AbsoluteUri);
        Assert.Equal("adv:demo", response.Today.AdventureId);
        Assert.Equal("\"pv_today\"", response.ETag);
    }

    /// <summary>Ensures v1 wire-array order is preserved for mixed time semantics.</summary>
    [Fact]
    public async Task MappingPreservesAuthoritativeWireOrderExactly()
    {
        var source = Today() with
        {
            TodayItems =
            [
                Item("all_day", CompanionTimeStatus.AllDay, null, null),
                Item("tbc", CompanionTimeStatus.ToBeConfirmed, null, null),
                Item("timed", CompanionTimeStatus.Scheduled, new TimeOnly(8, 0), new TimeOnly(9, 0)) with
                {
                    OperationalStatus = CompanionOperationalStatus.Changed,
                    RequiresAcknowledgment = true,
                    ActionLabel = "Review change"
                },
                Item("cancelled", CompanionTimeStatus.Cancelled, null, null)
            ]
        };

        var result = await new CompanionTodayService(new StubTransport(Response(source))).LoadAsync("adv:demo");

        Assert.Equal(CompanionTodayResultState.Success, result.State);
        Assert.Equal(
            new[] { "all_day", "tbc", "timed", "cancelled" },
            result.Today!.TodayItems.Select(value => value.ItemId));
    }

    /// <summary>Ensures duplicate identities and inconsistent Today/Next dates fail closed.</summary>
    [Fact]
    public async Task DuplicateAndInconsistentDatesFailClosed()
    {
        var valid = Today();
        var malformed = new[]
        {
            valid with { TodayItems = [Item("same"), Item("same")] },
            valid with { TodayItems = [Item("wrong") with { LocalDate = new DateOnly(2026, 8, 11) }] },
            valid with { NextItem = Item("past") with { LocalDate = new DateOnly(2026, 8, 9) } },
            valid with { NextItem = Item("item_one") with { LocalDate = new DateOnly(2026, 8, 11) } }
        };
        foreach (var source in malformed)
        {
            var result = await new CompanionTodayService(new StubTransport(Response(source))).LoadAsync("adv:demo");
            Assert.Equal(CompanionTodayResultState.MalformedOrUnsupported, result.State);
            Assert.Null(result.Today);
        }
    }

    /// <summary>Ensures unknown states, zones, timestamps, times, identities, and bounds fail closed.</summary>
    [Fact]
    public async Task MalformedOrContradictoryProjectionFailsClosed()
    {
        var valid = Today();
        var malformed = new[]
        {
            valid with { AdventureId = "different" },
            valid with { State = (CompanionTodayState)999 },
            valid with { TimeZone = "Not/AZone" },
            valid with { GeneratedAtUtc = valid.GeneratedAtUtc.ToOffset(TimeSpan.FromHours(1)) },
            valid with { FreshUntilUtc = valid.GeneratedAtUtc.AddMinutes(-1) },
            valid with { TodayItems = [Item("bad") with { TimeStatus = (CompanionTimeStatus)999 }] },
            valid with { TodayItems = [Item("bad") with { StartLocalTime = null }] },
            valid with { TodayItems = [Item("bad", CompanionTimeStatus.AllDay, new TimeOnly(8, 0), null)] },
            valid with { TodayItems = [Item("bad", CompanionTimeStatus.Cancelled, null, null) with { OperationalStatus = CompanionOperationalStatus.Proposed }] },
            valid with { TodayItems = Enumerable.Range(1, 251).Select(index => Item($"item_{index}")).ToArray() }
        };
        foreach (var source in malformed)
        {
            var result = await new CompanionTodayService(new StubTransport(Response(source))).LoadAsync("adv:demo");
            Assert.Equal(CompanionTodayResultState.MalformedOrUnsupported, result.State);
        }
    }

    /// <summary>Ensures transport metadata and protected-Resource references cannot inject or escape origin.</summary>
    [Fact]
    public async Task UnsafeEtagAndResourcePathFailClosed()
    {
        var source = Today() with
        {
            TodayItems =
            [
                Item("resource_item") with
                {
                    Resources =
                    [
                        new CompanionResourceSummaryDto
                        {
                            ResourceId = "res_demo", MediaType = "image/jpeg", Title = "Fictional image",
                            Availability = CompanionResourceAvailability.Available, OfflineEligible = false,
                            ContentPath = "https://outside.example/private"
                        }
                    ]
                }
            ]
        };
        var unsafeResource = await new CompanionTodayService(new StubTransport(Response(source))).LoadAsync("adv:demo");
        Assert.Equal(CompanionTodayResultState.MalformedOrUnsupported, unsafeResource.State);

        var unsafeEtag = await new CompanionTodayService(
            new StubTransport(Response(Today()) with { ETag = "\"safe\"\r\nInjected: value" })).LoadAsync("adv:demo");
        Assert.Equal(CompanionTodayResultState.MalformedOrUnsupported, unsafeEtag.State);
    }

    /// <summary>Ensures invalid identifiers make no request and caller cancellation propagates.</summary>
    [Fact]
    public async Task IdentityValidationAndCancellationRemainDistinct()
    {
        var counting = new CountingTransport();
        var invalid = await new CompanionTodayService(counting).LoadAsync("../private");
        Assert.Equal(CompanionTodayResultState.InvalidRequest, invalid.State);
        Assert.Equal(0, counting.Calls);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CompanionTodayService(new CancellingTransport()).LoadAsync("adv_demo", cancellation.Token));
    }

    /// <summary>Ensures response bounds apply before unbounded buffering.</summary>
    [Fact]
    public async Task OversizedResponseBecomesMalformedWithoutRetainingBody()
    {
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('x', CompanionContractLimits.MaximumJsonResponseBytes + 1))
        });
        var result = await new CompanionTodayService(new HttpCompanionTodayTransport(client)).LoadAsync("adv_demo");
        Assert.Equal(CompanionTodayResultState.MalformedOrUnsupported, result.State);
    }

    /// <summary>Ensures not-found, unauthorized, unavailable, and safe-error outcomes stay distinct.</summary>
    [Fact]
    public async Task FailureOutcomesRemainDistinctAndSafe()
    {
        var problem = new CompanionProblemDto
        {
            Type = new Uri("https://errors.example/problem"),
            Title = "Temporarily unavailable.",
            Status = 503,
            Code = "temporarily_unavailable",
            SupportId = "support_today",
            Retryable = true,
            RetryAfterSeconds = 30
        };
        (Exception Exception, CompanionTodayResultState State)[] cases =
        {
            (new CompanionTodayApiException(HttpStatusCode.NotFound, null, "support_today"), CompanionTodayResultState.NotFound),
            (new CompanionTodayApiException(HttpStatusCode.Unauthorized, null, "support_today"), CompanionTodayResultState.Unauthorized),
            (new HttpRequestException("private network detail"), CompanionTodayResultState.Unavailable),
            (new CompanionTodayApiException(HttpStatusCode.ServiceUnavailable, problem, "support_today"), CompanionTodayResultState.Error)
        };
        foreach (var item in cases)
        {
            var result = await new CompanionTodayService(new ThrowingTransport(item.Item1)).LoadAsync("adv_demo");
            Assert.Equal(item.Item2, result.State);
        }
    }

    private static CompanionTodayDto Today() => new()
    {
        SchemaVersion = "1.0",
        ProjectionVersion = "pv_today",
        GeneratedAtUtc = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero),
        FreshUntilUtc = new(2026, 8, 10, 10, 5, 0, TimeSpan.Zero),
        SupportId = "support_today",
        AdventureId = "adv:demo",
        LocalDate = new(2026, 8, 10),
        TimeZone = "Europe/Rome",
        State = CompanionTodayState.Active,
        TodayItems = [Item("item_one")],
        NextItem = Item("item_next") with { LocalDate = new DateOnly(2026, 8, 11) },
        Notice = "Fictional schedule."
    };

    private static CompanionScheduleItemDto Item(
        string id, CompanionTimeStatus timeStatus = CompanionTimeStatus.Scheduled,
        TimeOnly? start = null, TimeOnly? end = null) => new()
        {
            ItemId = id,
            ItemType = "activity",
            Title = "Fictional item",
            LocalDate = new(2026, 8, 10),
            StartLocalTime = timeStatus == CompanionTimeStatus.Scheduled ? start ?? new TimeOnly(9, 0) : start,
            EndLocalTime = timeStatus == CompanionTimeStatus.Scheduled ? end ?? new TimeOnly(10, 0) : end,
            TimeZone = "Europe/Rome",
            TimeStatus = timeStatus,
            OperationalStatus = timeStatus == CompanionTimeStatus.Cancelled
            ? CompanionOperationalStatus.Cancelled : CompanionOperationalStatus.Proposed,
            Resources = [],
            RequiresAcknowledgment = false
        };

    private static CompanionTodayTransportResponse Response(CompanionTodayDto dto) =>
        new(dto, "\"pv_today\"", "support_today");

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new Handler(respond)) { BaseAddress = new Uri("https://companion.example/") };

    private static HttpResponseMessage JsonResponse(CompanionTodayDto dto) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(dto, CompanionJsonSerializerContext.Default.CompanionTodayDto), Encoding.UTF8, "application/json")
    };

    private sealed class StubTransport(CompanionTodayTransportResponse response) : ICompanionTodayTransport
    { public Task<CompanionTodayTransportResponse> GetAsync(string adventureId, CancellationToken cancellationToken = default) => Task.FromResult(response); }
    private sealed class CountingTransport : ICompanionTodayTransport
    { public int Calls { get; private set; } public Task<CompanionTodayTransportResponse> GetAsync(string adventureId, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(Response(Today())); } }
    private sealed class CancellingTransport : ICompanionTodayTransport
    { public Task<CompanionTodayTransportResponse> GetAsync(string adventureId, CancellationToken cancellationToken = default) => Task.FromCanceled<CompanionTodayTransportResponse>(cancellationToken); }
    private sealed class ThrowingTransport(Exception exception) : ICompanionTodayTransport
    { public Task<CompanionTodayTransportResponse> GetAsync(string adventureId, CancellationToken cancellationToken = default) => Task.FromException<CompanionTodayTransportResponse>(exception); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request)); }
}
