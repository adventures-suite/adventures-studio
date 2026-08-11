using AdventuresSuite.Companion.Client;
using AdventuresSuite.Companion.Poc.Models;

namespace AdventuresSuite.Companion.Poc.Services;

/// <summary>Loads Adventures from the single provider selected during application composition.</summary>
public interface ICompanionContentProvider
{
    /// <summary>Loads the current presentation-safe Adventure collection.</summary>
    /// <param name="cancellationToken">Stops the load.</param>
    /// <returns>The explicit presentation outcome.</returns>
    Task<CompanionContentResult> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Contains a provider result without transport or server implementation details.</summary>
public sealed record CompanionContentResult(
    CompanionAdventureListState State,
    IReadOnlyList<CompanionAdventure> Adventures,
    bool HasDetailedContent,
    string? ErrorTitle = null,
    string? SupportId = null)
{
    /// <summary>Creates a successful demo or API presentation result.</summary>
    public static CompanionContentResult Success(IReadOnlyList<CompanionAdventure> adventures, bool hasDetailedContent) =>
        new(adventures.Count == 0 ? CompanionAdventureListState.Empty : CompanionAdventureListState.Success,
            adventures, hasDetailedContent);
}

/// <summary>Maps the typed Adventure-list client onto the POC presentation boundary.</summary>
public sealed class ApiCompanionContentProvider(ICompanionAdventureListService client) : ICompanionContentProvider
{
    /// <inheritdoc />
    public async Task<CompanionContentResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await client.LoadAsync(cancellationToken).ConfigureAwait(false);
        return new(
            result.State,
            result.Adventures.Select(Map).ToArray(),
            HasDetailedContent: false,
            result.ErrorTitle,
            result.SupportId);
    }

    private static CompanionAdventure Map(CompanionAdventureListItem source) => new(
        source.AdventureId,
        source.Title,
        source.Subtitle ?? string.Empty,
        source.Status switch
        {
            AdventuresSuite.Companion.Contracts.CompanionAdventureStatus.InProgress => "Current",
            AdventuresSuite.Companion.Contracts.CompanionAdventureStatus.Committed => "Committed",
            AdventuresSuite.Companion.Contracts.CompanionAdventureStatus.Completed => "Completed",
            _ => "Planned"
        },
        $"{source.StartDate:MMM d, yyyy} – {source.EndDate:MMM d, yyyy}",
        null,
        null,
        source.StartDate,
        source.EndDate,
        []);
}
