using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Contracts;
using AdventuresSuite.Companion.Poc.Models;
using AdventuresSuite.Companion.Poc.Services;

namespace AdventuresSuite.Companion.Poc.Tests;

public sealed class CompanionContentProviderTests
{
    [Fact]
    public async Task ApiSuccessMapsOnlyListFieldsWithoutClaimingDetail()
    {
        var source = new CompanionAdventureListItem(
            "adv_demo", "Fictional Adventure", "Safe context", CompanionAdventureStatus.InProgress,
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20), "Europe/Rome",
            new DateOnly(2026, 8, 10), null, "Europe/Rome", DateTimeOffset.Parse("2026-08-11T16:00:00Z"),
            CompanionCountdownState.InProgress, CompanionOfflineState.Available);
        var client = new StubClient(new CompanionAdventureListResult(
            CompanionAdventureListState.Success, [source]));

        var result = await new ApiCompanionContentProvider(client).LoadAsync();

        Assert.Equal(CompanionAdventureListState.Success, result.State);
        Assert.False(result.HasDetailedContent);
        var adventure = Assert.Single(result.Adventures);
        Assert.True(adventure.IsCurrent);
        Assert.Empty(adventure.Segments);
        Assert.Null(adventure.HeroImagePath);
    }

    [Fact]
    public async Task ApiOutcomesRemainDistinctWithoutDemoFallback()
    {
        var states = new[]
        {
            CompanionAdventureListState.Empty,
            CompanionAdventureListState.Unavailable,
            CompanionAdventureListState.Unauthorized,
            CompanionAdventureListState.Error
        };

        foreach (var state in states)
        {
            var clientResult = new CompanionAdventureListResult(
                state, [], ErrorTitle: "Safe error", SupportId: "support_demo");
            var result = await new ApiCompanionContentProvider(new StubClient(clientResult)).LoadAsync();

            Assert.Equal(state, result.State);
            Assert.Empty(result.Adventures);
            Assert.False(result.HasDetailedContent);
            Assert.Equal("Safe error", result.ErrorTitle);
            Assert.Equal("support_demo", result.SupportId);
        }
    }

    [Fact]
    public void PackagedDemoMustBeSelectedExplicitly()
    {
        var settings = CompanionProviderConfiguration.Resolve(null, null, "Demo", null);

        Assert.Equal(CompanionContentProviderKind.Demo, settings.Provider);
        Assert.Null(settings.ApiBaseAddress);
        Assert.Throws<InvalidOperationException>(() =>
            CompanionProviderConfiguration.Resolve(null, null, null, null));
    }

    [Fact]
    public void PackagedApiRequiresNonCredentialedHttpsOrigin()
    {
        var settings = CompanionProviderConfiguration.Resolve(
            null, null, "Api", "https://api.example.invalid/");

        Assert.Equal(CompanionContentProviderKind.Api, settings.Provider);
        Assert.Equal("https://api.example.invalid/", settings.ApiBaseAddress?.AbsoluteUri);
        Assert.Throws<InvalidOperationException>(() =>
            CompanionProviderConfiguration.Resolve(null, null, "Api", "http://api.example.invalid/"));
        Assert.Throws<InvalidOperationException>(() =>
            CompanionProviderConfiguration.Resolve(null, null, "Api", "https://user:password@api.example.invalid/"));
    }

    [Fact]
    public void LocalConfigurationExplicitlyOverridesPackagedConfiguration()
    {
        var settings = CompanionProviderConfiguration.Resolve(
            "Api", "https://local.example.invalid/", "Demo", null);

        Assert.Equal(CompanionContentProviderKind.Api, settings.Provider);
        Assert.Equal("https://local.example.invalid/", settings.ApiBaseAddress?.AbsoluteUri);
    }

    [Fact]
    public async Task CancellationPropagatesThroughProvider()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ApiCompanionContentProvider(new CancellingClient()).LoadAsync(cancellation.Token));
    }

    private sealed class StubClient(CompanionAdventureListResult result) : ICompanionAdventureListService
    {
        public Task<CompanionAdventureListResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class CancellingClient : ICompanionAdventureListService
    {
        public Task<CompanionAdventureListResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromCanceled<CompanionAdventureListResult>(cancellationToken);
    }
}
