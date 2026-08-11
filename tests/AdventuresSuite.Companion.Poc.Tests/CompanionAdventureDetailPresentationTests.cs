using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Contracts;
using AdventuresSuite.Companion.Poc.Models;
using AdventuresSuite.Companion.Poc.Services;

namespace AdventuresSuite.Companion.Poc.Tests;

public sealed class CompanionAdventureDetailPresentationTests
{
    [Fact]
    public async Task DemoProviderResolvesOnlyItsBundledEditorialProjection()
    {
        var demoAdventure = new CompanionAdventure(
            "1", "Bundled Fiction", "Editorial POC", "Current", "Jan 1 – Jan 2, 2027",
            null, null, new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 2),
            [new("Phoenix", "Rome", "Flight", "Fictional route", "January 2, 2027", "Europe/Rome", [])]);
        var plannedAdventure = demoAdventure with { Id = "2", Title = "Bundled Planned", Status = "Planned" };
        var content = new StubContentProvider(
            CompanionContentResult.Success([demoAdventure, plannedAdventure], hasDetailedContent: true));
        var provider = new DemoCompanionAdventureDetailProvider(content);

        var found = await provider.LoadAsync("1");
        var planned = await provider.LoadAsync("2");
        var missing = await provider.LoadAsync("adv_api_only");

        Assert.Equal("Bundled Fiction", found.Adventure?.Title);
        Assert.Equal("Bundled Planned", planned.Adventure?.Title);
        Assert.Equal("Rome", Assert.Single(found.Adventure!.Destinations).Name);
        Assert.Equal(CompanionAdventureDetailState.NotFound, missing.State);
        Assert.Null(missing.Adventure);
        Assert.Equal(3, content.RequestCount);
    }

    [Fact]
    public async Task ApiProviderExplicitlyMapsOnlyPresentationSafeFields()
    {
        var provider = new ApiCompanionAdventureDetailProvider(
            new StubDetailClient(CompanionAdventureDetailResult.Success(Detail())));

        var result = await provider.LoadAsync("adv_demo");

        Assert.Equal(CompanionAdventureDetailState.Success, result.State);
        var presentation = Assert.IsType<CompanionAdventureDetailPresentation>(result.Adventure);
        Assert.Equal("Fictional Italy", presentation.Title);
        Assert.Equal("Europe/Rome", presentation.PrimaryTimeZone);
        Assert.Equal(["Today"], presentation.AvailableCapabilities);
        var resource = Assert.IsType<CompanionResourceReferencePresentation>(
            Assert.Single(presentation.Destinations).HeroResource);
        Assert.DoesNotContain(resource.GetType().GetProperties(), property =>
            property.Name.Contains("Id", StringComparison.Ordinal) ||
            property.Name.Contains("Path", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(CompanionAdventureDetailState.NotFound)]
    [InlineData(CompanionAdventureDetailState.Unauthorized)]
    [InlineData(CompanionAdventureDetailState.Unavailable)]
    [InlineData(CompanionAdventureDetailState.MalformedOrUnsupported)]
    [InlineData(CompanionAdventureDetailState.Error)]
    public async Task ApiProviderPreservesEveryFailureWithoutDemoFallback(CompanionAdventureDetailState state)
    {
        var clientResult = new CompanionAdventureDetailResult(
            state, ErrorTitle: "Safe title", SupportId: "support_demo", Retryable: true);

        var result = await new ApiCompanionAdventureDetailProvider(new StubDetailClient(clientResult))
            .LoadAsync("adv_demo");

        Assert.Equal(state, result.State);
        Assert.Null(result.Adventure);
        Assert.Equal("support_demo", result.SupportId);
        Assert.True(result.Retryable);
    }

    [Fact]
    public async Task LoadingAndRetryUseTheSameConfiguredProviderAndIdentity()
    {
        var provider = new SequencedProvider();
        using var state = new CompanionAdventureDetailPresentationState(provider);

        var firstLoad = state.OpenAsync("adv_demo");
        Assert.True(state.IsLoading);
        provider.CompleteNext(Result("First"));
        await firstLoad;

        var retry = state.RetryAsync();
        Assert.True(state.IsLoading);
        provider.CompleteNext(Result("Retried"));
        await retry;

        Assert.Equal(["adv_demo", "adv_demo"], provider.Requests);
        Assert.Equal("Retried", state.Current?.Adventure?.Title);
    }

    [Fact]
    public async Task RapidSelectionCannotApplyAStaleResponse()
    {
        var provider = new SequencedProvider();
        using var state = new CompanionAdventureDetailPresentationState(provider);

        var oldLoad = state.OpenAsync("adv_old");
        var newLoad = state.OpenAsync("adv_new");
        provider.Complete(1, Result("New"));
        await newLoad;
        provider.Complete(0, Result("Old"));
        await oldLoad;

        Assert.Equal("New", state.Current?.Adventure?.Title);
    }

    [Fact]
    public async Task BackCancelsActiveDetailAndRetainsClosedListState()
    {
        var provider = new CancellationProvider();
        using var state = new CompanionAdventureDetailPresentationState(provider);

        var load = state.OpenAsync("adv_demo");
        state.Close();
        await load;

        Assert.True(provider.CancellationObserved);
        Assert.False(state.IsOpen);
        Assert.False(state.IsLoading);
        Assert.Null(state.Current);
    }

    [Fact]
    public async Task CallerCancellationRemainsObservable()
    {
        using var cancellation = new CancellationTokenSource();
        using var state = new CompanionAdventureDetailPresentationState(new CancellationProvider());
        var load = state.OpenAsync("adv_demo", cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
        Assert.False(state.IsLoading);
    }

    [Fact]
    public async Task RetrySupersedesThePriorAttempt()
    {
        var provider = new SequencedProvider();
        using var state = new CompanionAdventureDetailPresentationState(provider);
        var first = state.OpenAsync("adv_demo");
        var retry = state.RetryAsync();

        provider.Complete(1, Result("Retry"));
        await retry;
        provider.Complete(0, Result("Stale first attempt"));
        await first;

        Assert.Equal("Retry", state.Current?.Adventure?.Title);
    }

    [Fact]
    public async Task DisposalCancelsAndPreventsLatePresentation()
    {
        var provider = new CancellationProvider();
        var state = new CompanionAdventureDetailPresentationState(provider);
        var load = state.OpenAsync("adv_demo");

        state.Dispose();
        await load;

        Assert.True(provider.CancellationObserved);
        Assert.False(state.IsOpen);
        Assert.Null(state.Current);
    }

    [Fact]
    public void EveryFailureHasAStableAccessibleHeadingAndAnnouncement()
    {
        var states = new[]
        {
            CompanionAdventureDetailState.NotFound,
            CompanionAdventureDetailState.Unauthorized,
            CompanionAdventureDetailState.Unavailable,
            CompanionAdventureDetailState.MalformedOrUnsupported,
            CompanionAdventureDetailState.Error
        };

        foreach (var state in states)
        {
            var result = new CompanionAdventureDetailPresentationResult(state);
            Assert.False(string.IsNullOrWhiteSpace(CompanionAdventureDetailPresentationText.Heading(result)));
            Assert.False(string.IsNullOrWhiteSpace(CompanionAdventureDetailPresentationText.Message(result)));
        }
    }

    private static CompanionAdventureDetailPresentationResult Result(string title) => new(
        CompanionAdventureDetailState.Success,
        new(title, null, null, "Planned", new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 2),
            "Europe/Rome", null, null, [], null, null, []));

    private static MobileCompanionAdventureDetail Detail() => new(
        "adv_demo",
        "Fictional Italy",
        "Rome",
        "A safe overview.",
        CompanionAdventureStatus.Planned,
        new DateOnly(2027, 1, 1),
        new DateOnly(2027, 1, 10),
        "Europe/Rome",
        new(new DateOnly(2027, 1, 1), null, "Europe/Rome", DateTimeOffset.Parse("2026-08-11T16:00:00Z"),
            CompanionCountdownState.Future),
        [new("dest_demo", "Rome", new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 4), "Europe/Rome", 1,
            new("res_demo", "image/jpeg", 1024, "Rome skyline", "Fictional skyline", null,
                CompanionResourceAvailability.Available, false, null,
                "/v1/companion/resources/res_demo/content"))],
        "Arrive in Rome",
        "Review travel documents.",
        new Dictionary<string, string> { ["today"] = "/v1/companion/adventures/adv_demo/today" },
        "info_demo",
        "1.0",
        "pv_demo",
        DateTimeOffset.Parse("2026-08-11T16:00:00Z"),
        DateTimeOffset.Parse("2026-08-11T16:15:00Z"),
        null,
        "support_demo",
        "\"pv_demo\"");

    private sealed class StubDetailClient(CompanionAdventureDetailResult result) : ICompanionAdventureDetailService
    {
        public Task<CompanionAdventureDetailResult> LoadAsync(
            string adventureId,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class StubContentProvider(CompanionContentResult result) : ICompanionContentProvider
    {
        public int RequestCount { get; private set; }

        public Task<CompanionContentResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class SequencedProvider : ICompanionAdventureDetailProvider
    {
        private readonly List<TaskCompletionSource<CompanionAdventureDetailPresentationResult>> _pending = [];

        public List<string> Requests { get; } = [];

        public Task<CompanionAdventureDetailPresentationResult> LoadAsync(
            string adventureId,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(adventureId);
            var completion = new TaskCompletionSource<CompanionAdventureDetailPresentationResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Add(completion);
            return completion.Task;
        }

        public void CompleteNext(CompanionAdventureDetailPresentationResult result) =>
            _pending.First(completion => !completion.Task.IsCompleted).SetResult(result);

        public void Complete(int index, CompanionAdventureDetailPresentationResult result) =>
            _pending[index].SetResult(result);
    }

    private sealed class CancellationProvider : ICompanionAdventureDetailProvider
    {
        public bool CancellationObserved { get; private set; }

        public async Task<CompanionAdventureDetailPresentationResult> LoadAsync(
            string adventureId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The cancellation test unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }
    }
}
