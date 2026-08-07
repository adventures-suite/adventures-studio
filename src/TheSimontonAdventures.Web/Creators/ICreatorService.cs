namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Defines storage-independent retrieval of validated Creator records.
/// </summary>
public interface ICreatorService
{
    /// <summary>Gets every validated Creator registered with the platform.</summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>An immutable snapshot of registered Creators.</returns>
    Task<IReadOnlyList<Creator>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Finds a Creator by its stable identity.</summary>
    /// <param name="creatorId">The stable Creator identity to retrieve.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching Creator, or <see langword="null"/> when absent.</returns>
    Task<Creator?> GetByIdAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Finds an active Creator by an approved normalized host.</summary>
    /// <param name="host">The host name without a scheme, path, or port.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The active Creator registered for the host, or <see langword="null"/>
    /// when the host is unknown or its Creator is inactive.
    /// </returns>
    Task<Creator?> GetByHostAsync(
        string host,
        CancellationToken cancellationToken = default);
}
