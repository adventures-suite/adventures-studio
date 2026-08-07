namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Stores one immutable Creator Context within a dependency-injection request
/// scope.
/// </summary>
public sealed class CreatorContextAccessor : ICreatorContextAccessor
{
    private CreatorContext? _current;

    /// <inheritdoc />
    public CreatorContext Current => _current ?? throw new InvalidOperationException(
        "Creator Context has not been established for the current request.");

    internal bool IsEstablished => _current is not null;

    internal void Establish(CreatorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_current is not null && _current != context)
        {
            throw new InvalidOperationException(
                "Creator Context cannot be replaced within the same request scope.");
        }

        _current = context;
    }
}
