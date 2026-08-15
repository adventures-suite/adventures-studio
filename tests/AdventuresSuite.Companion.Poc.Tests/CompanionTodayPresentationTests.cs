using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Contracts;
using AdventuresSuite.Companion.Poc.Models;
using AdventuresSuite.Companion.Poc.Services;

namespace AdventuresSuite.Companion.Poc.Tests;

public sealed class CompanionTodayPresentationTests
{
    [Fact]
    public async Task ApiProviderPreservesAuthoritativeWireOrderAndSafeFields()
    {
        var today = new MobileCompanionToday(
            "adv_demo", new(2026, 8, 10), "Europe/Rome", CompanionTodayState.Active,
            [Item("All day", CompanionTimeStatus.AllDay), Item("TBC", CompanionTimeStatus.ToBeConfirmed), Item("Timed", CompanionTimeStatus.Scheduled)],
            Item("Next", CompanionTimeStatus.Cancelled) with { LocalDate = new DateOnly(2026, 8, 11) },
            "Safe notice", "1.0", "pv", DateTimeOffset.Parse("2026-08-10T10:00:00Z"),
            DateTimeOffset.Parse("2026-08-10T10:05:00Z"), null, "support", "\"pv\"");

        var result = await new ApiCompanionTodayProvider(
            new StubClient(CompanionTodayResult.Success(today))).LoadAsync("adv_demo");

        Assert.Equal(new[] { "All day", "TBC", "Timed" }, result.Today!.TodayItems.Select(value => value.Title));
        Assert.Equal("Next", result.Today.NextItem?.Title);
        Assert.DoesNotContain(result.Today.TodayItems.SelectMany(value => value.Resources)
            .SelectMany(value => value.GetType().GetProperties()), property =>
                property.Name.Contains("Id", StringComparison.Ordinal) || property.Name.Contains("Path", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(CompanionTodayResultState.NotFound)]
    [InlineData(CompanionTodayResultState.Unauthorized)]
    [InlineData(CompanionTodayResultState.Unavailable)]
    [InlineData(CompanionTodayResultState.MalformedOrUnsupported)]
    [InlineData(CompanionTodayResultState.Error)]
    public async Task ApiProviderNeverFallsBackToDemo(CompanionTodayResultState state)
    {
        var result = await new ApiCompanionTodayProvider(new StubClient(new(state, ErrorTitle: "Safe")))
            .LoadAsync("adv_demo");
        Assert.Equal(state, result.State);
        Assert.Null(result.Today);
    }

    [Fact]
    public async Task DemoProviderUsesOnlyBundledAdventureOrder()
    {
        var adventure = new CompanionAdventure("1", "Demo", "Fiction", "Current", "Aug 1 – Aug 3",
            null, null, new(2026, 8, 1), new(2026, 8, 3),
            [
                new("A", "B", "Rail", "First", "August 1, 2026", "Europe/Rome", []),
                new("B", "C", "Walk", "Second", "August 2, 2026", "Europe/Rome", [])
            ]);
        var provider = new DemoCompanionTodayProvider(new StubContent(
            CompanionContentResult.Success([adventure], hasDetailedContent: true)));

        var result = await provider.LoadAsync("1");
        var missing = await provider.LoadAsync("api_only");

        Assert.Equal("A to B", Assert.Single(result.Today!.TodayItems).Title);
        Assert.Equal("B to C", result.Today.NextItem?.Title);
        Assert.Equal(CompanionTodayResultState.NotFound, missing.State);
    }

    [Fact]
    public async Task RetryAndRapidSelectionUseSameProviderAndRejectStaleResponse()
    {
        var provider = new SequencedProvider();
        using var state = new CompanionTodayPresentationState(provider);
        var oldLoad = state.LoadAsync("old");
        var newLoad = state.LoadAsync("new");
        provider.Complete(1, Result("New")); await newLoad;
        provider.Complete(0, Result("Old")); await oldLoad;
        var retry = state.RetryAsync(); provider.Complete(2, Result("Retry")); await retry;

        Assert.Equal(["old", "new", "new"], provider.Requests);
        Assert.Equal("Retry", state.Current?.Today?.Notice);
    }

    [Fact]
    public async Task DisposalCancelsAndCallerCancellationPropagates()
    {
        var provider = new CancellationProvider();
        var state = new CompanionTodayPresentationState(provider);
        var load = state.LoadAsync("adv_demo"); state.Dispose(); await load;
        Assert.True(provider.Observed);

        using var cancellation = new CancellationTokenSource();
        using var callerState = new CompanionTodayPresentationState(new CancellationProvider());
        var callerLoad = callerState.LoadAsync("adv_demo", cancellation.Token); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callerLoad);
    }

    private static MobileCompanionScheduleItem Item(string title, CompanionTimeStatus status) => new(
        $"item_{title.Replace(" ", "_", StringComparison.Ordinal)}", "activity", title, null,
        new(2026, 8, 10), status == CompanionTimeStatus.Scheduled ? new TimeOnly(9, 0) : null,
        status == CompanionTimeStatus.Scheduled ? new TimeOnly(10, 0) : null, "Europe/Rome", status,
        status == CompanionTimeStatus.Cancelled ? CompanionOperationalStatus.Cancelled : CompanionOperationalStatus.Proposed,
        null, null, [], false, null);

    private static CompanionTodayPresentationResult Result(string notice) => new(
        CompanionTodayResultState.Success, new(new(2026, 8, 10), "Europe/Rome", "Active", [], null, notice, null));
    private sealed class StubClient(CompanionTodayResult result) : ICompanionTodayService
    { public Task<CompanionTodayResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default) => Task.FromResult(result); }
    private sealed class StubContent(CompanionContentResult result) : ICompanionContentProvider
    { public Task<CompanionContentResult> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(result); }
    private sealed class SequencedProvider : ICompanionTodayProvider
    {
        private readonly List<TaskCompletionSource<CompanionTodayPresentationResult>> _pending = [];
        public List<string> Requests { get; } = [];
        public Task<CompanionTodayPresentationResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default)
        { Requests.Add(adventureId); var source = new TaskCompletionSource<CompanionTodayPresentationResult>(TaskCreationOptions.RunContinuationsAsynchronously); _pending.Add(source); return source.Task; }
        public void Complete(int index, CompanionTodayPresentationResult result) => _pending[index].SetResult(result);
    }
    private sealed class CancellationProvider : ICompanionTodayProvider
    {
        public bool Observed { get; private set; }
        public async Task<CompanionTodayPresentationResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default)
        { try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); throw new InvalidOperationException(); } catch (OperationCanceledException) { Observed = true; throw; } }
    }
}
