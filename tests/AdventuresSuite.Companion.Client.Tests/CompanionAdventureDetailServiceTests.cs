using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Client.Tests;

public sealed class CompanionAdventureDetailServiceTests
{
    [Fact]
    public async Task TransportUsesOnlyVersionedDetailRouteAndPreservesHeaders()
    {
        var expected = Detail();
        using var handler = new StubHttpHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.example.invalid/v1/companion/adventures/adv_demo", request.RequestUri?.AbsoluteUri);
            Assert.Empty(request.Headers.Authorization?.Parameter ?? string.Empty);
            var response = JsonResponse(expected);
            response.Headers.ETag = new EntityTagHeaderValue("\"pv_detail\"");
            response.Headers.Add("X-Support-Id", "support_detail");
            return response;
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.invalid/") };

        var result = await new HttpCompanionAdventureDetailTransport(client).GetAsync("adv_demo");

        Assert.Equal(expected.AdventureId, result.Adventure.AdventureId);
        Assert.Equal(expected.ProjectionVersion, result.Adventure.ProjectionVersion);
        Assert.Equal(expected.Destinations.Count, result.Adventure.Destinations.Count);
        Assert.Equal("\"pv_detail\"", result.ETag);
        Assert.Equal("support_detail", result.HeaderSupportId);
    }

    [Fact]
    public async Task InvalidIdentifierIsRejectedBeforeRequestConstruction()
    {
        var handlerCalled = false;
        using var handler = new StubHttpHandler(_ =>
        {
            handlerCalled = true;
            return JsonResponse(Detail());
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.invalid/") };
        var transport = new HttpCompanionAdventureDetailTransport(client);

        await Assert.ThrowsAsync<ArgumentException>(() => transport.GetAsync("../private?value"));
        Assert.False(handlerCalled);

        var countingTransport = new CountingTransport(TransportResponse());
        var result = await new CompanionAdventureDetailService(countingTransport).LoadAsync("bad/value");
        Assert.Equal(CompanionAdventureDetailState.InvalidRequest, result.State);
        Assert.Equal(0, countingTransport.RequestCount);
    }

    [Fact]
    public void TransportRequiresExistingHttpsBaseAddress()
    {
        using var missingBaseAddress = new HttpClient();
        using var insecureBaseAddress = new HttpClient { BaseAddress = new Uri("http://api.example.invalid/") };

        Assert.Throws<ArgumentException>(() => new HttpCompanionAdventureDetailTransport(missingBaseAddress));
        Assert.Throws<ArgumentException>(() => new HttpCompanionAdventureDetailTransport(insecureBaseAddress));
    }

    [Fact]
    public async Task ExplicitMappingPreservesApprovedProjectionFreshnessAndMetadata()
    {
        var result = await new CompanionAdventureDetailService(new CountingTransport(TransportResponse()))
            .LoadAsync("adv_demo");

        Assert.Equal(CompanionAdventureDetailState.Success, result.State);
        var detail = Assert.IsType<MobileCompanionAdventureDetail>(result.Adventure);
        Assert.Equal("adv_demo", detail.AdventureId);
        Assert.Equal("pv_detail", detail.ProjectionVersion);
        Assert.Equal("\"pv_detail\"", detail.ETag);
        Assert.Equal("support_detail", detail.SupportId);
        Assert.Equal(DateTimeOffset.Parse("2026-08-11T16:15:00Z"), detail.FreshUntilUtc);
        Assert.Equal("Europe/Rome", detail.PrimaryTimeZone);
        Assert.Equal([1, 2], detail.Destinations.Select(destination => destination.Sequence));
        Assert.Equal("/v1/companion/adventures/adv_demo/today", detail.CapabilityLinks["today"]);
    }

    [Fact]
    public async Task UnknownEnumsInvalidDatesAndMalformedJsonFailSafely()
    {
        var validJson = Serialize(Detail());
        var payloads = new[]
        {
            "{not-json",
            validJson.Replace("\"inProgress\"", "\"unknownStatus\"", StringComparison.Ordinal),
            validJson.Replace("\"2026-08-10\"", "\"not-a-date\"", StringComparison.Ordinal)
        };

        foreach (var payload in payloads)
        {
            using var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                Headers = { { "X-Support-Id", "support_detail" } }
            });
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.invalid/") };
            var service = new CompanionAdventureDetailService(new HttpCompanionAdventureDetailTransport(client));

            var result = await service.LoadAsync("adv_demo");

            Assert.Equal(CompanionAdventureDetailState.MalformedOrUnsupported, result.State);
            Assert.Equal("unsupported_projection", result.ErrorCode);
            Assert.DoesNotContain(payload, result.ErrorTitle);
            Assert.Equal("support_detail", result.SupportId);
        }
    }

    [Fact]
    public async Task InvalidTimeZonesLinksNestedDataAndResponseIdentityFailSafely()
    {
        var valid = Detail();
        var invalidDetails = new[]
        {
            valid with { PrimaryTimeZone = "Not/AZone" },
            valid with { CapabilityLinks = new Dictionary<string, string> { ["today"] = "https://evil.example/steal" } },
            valid with { Destinations = [Destination(2), Destination(1) with { DestinationVisitId = "dest_two" }] },
            valid with { Destinations = [null!] },
            valid with
            {
                Destinations =
                [
                    Destination(1) with
                    {
                        HeroResource = Resource() with { ContentPath = "//evil.example/protected" }
                    }
                ]
            },
            valid with { Status = (CompanionAdventureStatus)999 },
            valid with { AdventureId = "adv_other" }
        };

        foreach (var invalid in invalidDetails)
        {
            var result = await new CompanionAdventureDetailService(
                new CountingTransport(TransportResponse(invalid))).LoadAsync("adv_demo");

            Assert.Equal(CompanionAdventureDetailState.MalformedOrUnsupported, result.State);
            Assert.Null(result.Adventure);
            Assert.Equal("support_detail", result.SupportId);
        }
    }

    [Fact]
    public async Task NotFoundUnauthorizedUnavailableAndSafeErrorRemainDistinct()
    {
        var problem = Problem(HttpStatusCode.NotFound, "resource_unavailable");
        (Exception Exception, CompanionAdventureDetailState State)[] cases =
        {
            (new CompanionAdventureDetailApiException(HttpStatusCode.NotFound, problem, "support_detail"), CompanionAdventureDetailState.NotFound),
            (new CompanionAdventureDetailApiException(HttpStatusCode.Unauthorized, Problem(HttpStatusCode.Unauthorized, "authentication_required"), "support_detail"), CompanionAdventureDetailState.Unauthorized),
            (new HttpRequestException("private network detail"), CompanionAdventureDetailState.Unavailable),
            (new CompanionAdventureDetailApiException(HttpStatusCode.ServiceUnavailable, Problem(HttpStatusCode.ServiceUnavailable, "temporarily_unavailable"), "support_detail"), CompanionAdventureDetailState.Error)
        };

        foreach (var item in cases)
        {
            var result = await new CompanionAdventureDetailService(new ThrowingTransport(item.Exception))
                .LoadAsync("adv_demo");
            Assert.Equal(item.State, result.State);
        }

        var notFound = await new CompanionAdventureDetailService(new ThrowingTransport(cases[0].Exception))
            .LoadAsync("adv_demo");
        Assert.Equal("resource_unavailable", notFound.ErrorCode);
        Assert.Equal("support_detail", notFound.SupportId);

        var unauthorized = await new CompanionAdventureDetailService(new ThrowingTransport(cases[1].Exception))
            .LoadAsync("adv_demo");
        Assert.Null(unauthorized.ErrorCode);
        Assert.Null(unauthorized.ErrorTitle);

        var safeError = await new CompanionAdventureDetailService(new ThrowingTransport(cases[3].Exception))
            .LoadAsync("adv_demo");
        Assert.Equal("temporarily_unavailable", safeError.ErrorCode);
        Assert.True(safeError.Retryable);
        Assert.Equal(30, safeError.RetryAfterSeconds);
    }

    [Fact]
    public async Task TransportReadsOnlyAllowlistedSafeProblemFields()
    {
        var problem = Problem(HttpStatusCode.ServiceUnavailable, "temporarily_unavailable");
        using var handler = new StubHttpHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(problem, CompanionJsonSerializerContext.Default.CompanionProblemDto),
                    Encoding.UTF8,
                    "application/problem+json")
            };
            response.Headers.Add("X-Support-Id", "support_detail");
            return response;
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.invalid/") };

        var exception = await Assert.ThrowsAsync<CompanionAdventureDetailApiException>(() =>
            new HttpCompanionAdventureDetailTransport(client).GetAsync("adv_demo"));

        Assert.Equal(problem, exception.Problem);
        Assert.Equal("support_detail", exception.HeaderSupportId);
        Assert.DoesNotContain(problem.Title, exception.Message);
    }

    [Fact]
    public async Task CallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CompanionAdventureDetailService(new CancellingTransport()).LoadAsync("adv_demo", cancellation.Token));
    }

    private static CompanionAdventureDetailTransportResponse TransportResponse(CompanionAdventureDto? detail = null) =>
        new(detail ?? Detail(), "\"pv_detail\"", "support_detail");

    private static CompanionAdventureDto Detail() => new()
    {
        SchemaVersion = "1.0",
        ProjectionVersion = "pv_detail",
        GeneratedAtUtc = DateTimeOffset.Parse("2026-08-11T16:00:00Z"),
        FreshUntilUtc = DateTimeOffset.Parse("2026-08-11T16:15:00Z"),
        SyncCursor = "cursor_detail",
        SupportId = "support_detail",
        AdventureId = "adv_demo",
        Title = "Fictional Italian Cities",
        Subtitle = "Rome and Florence",
        Description = "A fictional traveler-safe overview.",
        Status = CompanionAdventureStatus.InProgress,
        StartDate = new DateOnly(2026, 8, 10),
        EndDate = new DateOnly(2026, 8, 20),
        PrimaryTimeZone = "Europe/Rome",
        Countdown = new CompanionCountdownDto
        {
            TargetDate = new DateOnly(2026, 8, 10),
            TimeZone = "Europe/Rome",
            EvaluatedAtUtc = DateTimeOffset.Parse("2026-08-11T16:00:00Z"),
            State = CompanionCountdownState.InProgress
        },
        Destinations = [Destination(1), Destination(2) with { DestinationVisitId = "dest_two", Name = "Florence" }],
        NextItemSummary = "Fictional train to Florence",
        ReadinessSummary = "One safe action needs attention.",
        CapabilityLinks = new Dictionary<string, string>
        {
            ["today"] = "/v1/companion/adventures/adv_demo/today"
        },
        InformationProfileVersion = "info_demo_01"
    };

    private static CompanionDestinationSummaryDto Destination(int sequence) => new()
    {
        DestinationVisitId = $"dest_{sequence}",
        Name = "Rome",
        StartDate = new DateOnly(2026, 8, 10 + sequence - 1),
        EndDate = new DateOnly(2026, 8, 12 + sequence - 1),
        TimeZone = "Europe/Rome",
        Sequence = sequence
    };

    private static CompanionProblemDto Problem(HttpStatusCode status, string code) => new()
    {
        Type = new Uri("https://errors.example.invalid/problem"),
        Title = "A safe problem title.",
        Status = (int)status,
        Code = code,
        SupportId = "support_detail",
        Retryable = status == HttpStatusCode.ServiceUnavailable,
        RetryAfterSeconds = status == HttpStatusCode.ServiceUnavailable ? 30 : null
    };

    private static CompanionResourceSummaryDto Resource() => new()
    {
        ResourceId = "res_demo",
        MediaType = "image/jpeg",
        ByteLength = 1024,
        Title = "Fictional image",
        AlternativeText = "Fictional skyline",
        Availability = CompanionResourceAvailability.Available,
        OfflineEligible = false,
        ContentPath = "/v1/companion/resources/res_demo/content"
    };

    private static HttpResponseMessage JsonResponse(CompanionAdventureDto detail) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(Serialize(detail), Encoding.UTF8, "application/json")
    };

    private static string Serialize(CompanionAdventureDto detail) =>
        JsonSerializer.Serialize(detail, CompanionJsonSerializerContext.Default.CompanionAdventureDto);

    private sealed class CountingTransport(CompanionAdventureDetailTransportResponse response)
        : ICompanionAdventureDetailTransport
    {
        public int RequestCount { get; private set; }

        public Task<CompanionAdventureDetailTransportResponse> GetAsync(
            string adventureId,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingTransport(Exception exception) : ICompanionAdventureDetailTransport
    {
        public Task<CompanionAdventureDetailTransportResponse> GetAsync(
            string adventureId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CompanionAdventureDetailTransportResponse>(exception);
    }

    private sealed class CancellingTransport : ICompanionAdventureDetailTransport
    {
        public Task<CompanionAdventureDetailTransportResponse> GetAsync(
            string adventureId,
            CancellationToken cancellationToken = default) =>
            Task.FromCanceled<CompanionAdventureDetailTransportResponse>(cancellationToken);
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
