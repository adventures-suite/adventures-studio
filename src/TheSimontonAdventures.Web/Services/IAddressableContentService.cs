using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Services;

/// <summary>
/// Defines the contract for resolving stable public addresses to published
/// AdventuresSuite content.
/// </summary>
/// <remarks>
/// This service provides an abstraction between public addressing and the
/// underlying content-storage implementation.
///
/// Consumers such as the QR Engine, redirect endpoints, search, navigation,
/// and future platform capabilities should depend on this interface rather
/// than reading JSON files or constructing internal routes directly.
/// </remarks>
public interface IAddressableContentService
{
    /// <summary>
    /// Resolves a stable public slug to its current published target.
    /// </summary>
    /// <param name="creatorId">The Creator that owns the public address.</param>
    /// <param name="slug">
    /// The creator-scoped public slug to resolve.
    /// The value should not include route prefixes such as <c>/go/</c>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result is the resolved
    /// <see cref="AddressableContentRoute"/> when a published target exists;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// Implementations should resolve both primary slugs and registered aliases.
    ///
    /// Unknown, invalid, archived, or unpublished targets should return
    /// <see langword="null"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="creatorId"/> is the default identity.
    /// </exception>
    Task<AddressableContentRoute?> ResolveAsync(
        CreatorId creatorId,
        string slug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all published content routes owned by one Creator.
    /// </summary>
    /// <param name="creatorId">The Creator whose routes should be returned.</param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result is a read-only collection of published
    /// <see cref="AddressableContentRoute"/> instances.
    /// </returns>
    /// <remarks>
    /// This method may later support administrative tools, QR manifests,
    /// validation, diagnostics, navigation, search indexing, and bulk QR
    /// generation.
    ///
    /// Implementations should not include unpublished content in the returned
    /// collection.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="creatorId"/> is the default identity.
    /// </exception>
    Task<IReadOnlyList<AddressableContentRoute>> GetAllAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);
}
