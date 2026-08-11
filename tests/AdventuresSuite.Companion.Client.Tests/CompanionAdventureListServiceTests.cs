using System.Net;
using System.Text;
using System.Text.Json;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Client.Tests;

public sealed class CompanionAdventureListServiceTests
{
    [Fact]
    public async Task HttpTransportUsesVersionedListRouteAndTypedContract()
    {
        var expected = Collection(Summary());
        using var handler = new StubHttpHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.example.invalid/v1/companion/adventures", request.RequestUri?.AbsoluteUri);
            var json = JsonSerializer.Serialize(expected, CompanionJsonSerializerContext.Default.CompanionAdventureCollectionDto);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.invalid/") };

        var result = await new HttpCompanionAdventureListTransport(httpClient).ListAsync();

        Assert.Equal(expected.ProjectionVersion, result.ProjectionVersion);
        Assert.Equal("adv_demo", Assert.Single(result.Adventures).AdventureId);
    }

    [Fact]
    public async Task HttpTransportRetainsOnlyTypedProblemFields()
    {
        var problem = Problem("temporarily_unavailable", "The Adventure list is temporarily unavailable.");
        using var handler = new StubHttpHandler(_ =>
        {
            var json = JsonSerializer.Serialize(problem, CompanionJsonSerializerContext.Default.CompanionProblemDto);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/problem+json")
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.invalid/") };

        var exception = await Assert.ThrowsAsync<CompanionApiException>(() =>
            new HttpCompanionAdventureListTransport(httpClient).ListAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(problem, exception.Problem);
        Assert.DoesNotContain(problem.Title, exception.Message);
    }

    [Fact]
    public async Task MapsOnlyApprovedListFieldsAndFreshness()
    {
        var response = Collection(Summary());

        var result = await new CompanionAdventureListService(new StubTransport(response)).LoadAsync();

        Assert.Equal(CompanionAdventureListState.Success, result.State);
        var adventure = Assert.Single(result.Adventures);
        Assert.Equal("adv_demo", adventure.AdventureId);
        Assert.Equal(new DateOnly(2027, 10, 25), adventure.CountdownTargetDate);
        Assert.Equal(response.GeneratedAtUtc, result.GeneratedAtUtc);
        Assert.Equal(response.FreshUntilUtc, result.FreshUntilUtc);
        Assert.Equal(response.SupportId, result.SupportId);
    }

    [Fact]
    public async Task EmptyAuthorizedCollectionMapsToEmpty()
    {
        var result = await new CompanionAdventureListService(new StubTransport(Collection())).LoadAsync();

        Assert.Equal(CompanionAdventureListState.Empty, result.State);
        Assert.Empty(result.Adventures);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AuthorizationFailureDoesNotExposeProblem(HttpStatusCode statusCode)
    {
        var problem = Problem("private_detail", "Do not expose this title");

        var result = await new CompanionAdventureListService(new ThrowingTransport(new CompanionApiException(statusCode, problem))).LoadAsync();

        Assert.Equal(CompanionAdventureListState.Unauthorized, result.State);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorTitle);
        Assert.Null(result.SupportId);
    }

    [Fact]
    public async Task SafeProblemFieldsArePreserved()
    {
        var problem = Problem("temporarily_unavailable", "The Adventure list is temporarily unavailable.");

        var result = await new CompanionAdventureListService(new ThrowingTransport(new CompanionApiException(HttpStatusCode.ServiceUnavailable, problem))).LoadAsync();

        Assert.Equal(CompanionAdventureListState.Error, result.State);
        Assert.Equal(problem.Code, result.ErrorCode);
        Assert.Equal(problem.Title, result.ErrorTitle);
        Assert.Equal(problem.SupportId, result.SupportId);
        Assert.True(result.Retryable);
    }

    [Fact]
    public async Task NetworkFailureMapsToUnavailable()
    {
        var result = await new CompanionAdventureListService(new ThrowingTransport(new HttpRequestException("private network detail"))).LoadAsync();

        Assert.Equal(CompanionAdventureListState.Unavailable, result.State);
        Assert.Null(result.ErrorTitle);
    }

    [Fact]
    public async Task CallerCancellationIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CompanionAdventureListService(new CancellingTransport()).LoadAsync(cancellation.Token));
    }

    private static CompanionAdventureCollectionDto Collection(params CompanionAdventureSummaryDto[] adventures) => new()
    {
        SchemaVersion = "1.0",
        ProjectionVersion = "pv_demo",
        GeneratedAtUtc = DateTimeOffset.Parse("2026-08-11T16:00:00Z"),
        FreshUntilUtc = DateTimeOffset.Parse("2026-08-11T16:15:00Z"),
        SupportId = "support_demo",
        Adventures = adventures
    };

    private static CompanionAdventureSummaryDto Summary() => new()
    {
        AdventureId = "adv_demo",
        Title = "Fictional Adventure",
        Subtitle = "A safe subtitle",
        Status = CompanionAdventureStatus.Planned,
        StartDate = new DateOnly(2027, 10, 25),
        EndDate = new DateOnly(2027, 11, 15),
        PrimaryTimeZone = "Europe/Madrid",
        Countdown = new CompanionCountdownDto
        {
            TargetDate = new DateOnly(2027, 10, 25),
            TimeZone = "Europe/Madrid",
            EvaluatedAtUtc = DateTimeOffset.Parse("2026-08-11T16:00:00Z"),
            State = CompanionCountdownState.Future
        },
        OfflineState = CompanionOfflineState.Available
    };

    private static CompanionProblemDto Problem(string code, string title) => new()
    {
        Type = new Uri("https://errors.example.invalid/problem"),
        Title = title,
        Status = 503,
        Code = code,
        SupportId = "support_problem",
        Retryable = true
    };

    private sealed class StubTransport(CompanionAdventureCollectionDto response) : ICompanionAdventureListTransport
    {
        public Task<CompanionAdventureCollectionDto> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult(response);
    }

    private sealed class ThrowingTransport(Exception exception) : ICompanionAdventureListTransport
    {
        public Task<CompanionAdventureCollectionDto> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<CompanionAdventureCollectionDto>(exception);
    }

    private sealed class CancellingTransport : ICompanionAdventureListTransport
    {
        public Task<CompanionAdventureCollectionDto> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromCanceled<CompanionAdventureCollectionDto>(cancellationToken);
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
