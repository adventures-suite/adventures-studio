using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Mobile.Services;

namespace AdventuresSuite.Companion.Mobile.Tests;

/// <summary>Verifies Itinerary presentation and provider isolation.</summary>
public sealed class CompanionItineraryPresentationTests
{
    /// <summary>Ensures API outcomes are mapped without invoking Demo content.</summary>
    [Fact]
    public async Task ApiProviderPreservesFailureWithoutFallback()
    {
        var provider = new ApiCompanionItineraryProvider(new StubService(CompanionItineraryResult.Unavailable()));
        var result = await provider.LoadAsync("adv_demo");
        Assert.Equal(CompanionItineraryResultState.Unavailable, result.State);
        Assert.Null(result.Itinerary);
    }

    /// <summary>Ensures stale completion from a prior selection cannot replace the current Journey.</summary>
    [Fact]
    public async Task SupersededLoadCannotChangeState()
    {
        var provider = new SequencedProvider();
        using var state = new CompanionItineraryPresentationState(provider);
        var first = state.LoadAsync("adv_one");
        var second = state.LoadAsync("adv_two");
        provider.Complete(1, "current");
        await second;
        provider.Complete(0, "stale");
        await first;
        Assert.Equal("current", state.Current?.ErrorTitle);
    }

    private sealed class StubService(CompanionItineraryResult result) : ICompanionItineraryService
    { public Task<CompanionItineraryResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default) => Task.FromResult(result); }

    private sealed class SequencedProvider : ICompanionItineraryProvider
    {
        private readonly List<TaskCompletionSource<CompanionItineraryPresentationResult>> _requests = [];
        public Task<CompanionItineraryPresentationResult> LoadAsync(string adventureId, CancellationToken cancellationToken = default)
        {
            var source = new TaskCompletionSource<CompanionItineraryPresentationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(source);
            return source.Task;
        }
        public void Complete(int index, string title) => _requests[index].SetResult(new(CompanionItineraryResultState.Error, ErrorTitle: title));
    }
}
