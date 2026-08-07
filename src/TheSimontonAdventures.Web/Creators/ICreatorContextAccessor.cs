namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Exposes the immutable Creator Context already established for the current
/// request scope.
/// </summary>
public interface ICreatorContextAccessor
{
    /// <summary>
    /// Gets the Creator Context established by host-resolution middleware.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Creator Context has not been established for the current request.
    /// </exception>
    CreatorContext Current { get; }
}
