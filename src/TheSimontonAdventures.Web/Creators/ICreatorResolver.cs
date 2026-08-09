using Microsoft.AspNetCore.Http;

namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Resolves an incoming approved host to the immutable Creator Context used by
/// request-facing platform capabilities.
/// </summary>
public interface ICreatorResolver
{
    /// <summary>Resolves a request host within the configured environment.</summary>
    /// <param name="host">The incoming HTTP host, optionally including a port.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The resolved active Creator Context, or <see langword="null"/> when the
    /// host is invalid, unknown, inactive, or unapproved.
    /// </returns>
    Task<CreatorContext?> ResolveAsync(
        HostString host,
        CancellationToken cancellationToken = default);
}
