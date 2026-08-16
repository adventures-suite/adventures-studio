namespace AdventuresSuite.Companion.Mobile.Services;

/// <summary>Dispatches presentation callbacks onto the owning UI thread.</summary>
public interface ICompanionUiDispatcher
{
    /// <summary>Dispatches one asynchronous presentation callback.</summary>
    /// <param name="callback">The callback to dispatch.</param>
    void Dispatch(Func<Task> callback);
}

/// <summary>Coordinates the single transient layer that may consume platform Back.</summary>
public sealed class TransientBackNavigationService(ICompanionUiDispatcher dispatcher)
{
    private Func<Task>? _handler;

    /// <summary>Registers the currently open transient layer.</summary>
    /// <param name="handler">The idempotent dismissal callback.</param>
    /// <returns>A registration that removes only this callback.</returns>
    public IDisposable Register(Func<Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        return new Registration(this, handler);
    }

    /// <summary>Consumes Back when a transient layer is open.</summary>
    /// <returns><see langword="true"/> when Back was consumed.</returns>
    public bool TryHandleBack()
    {
        var handler = _handler;
        if (handler is null)
        {
            return false;
        }

        dispatcher.Dispatch(handler);
        return true;
    }

    private sealed class Registration(TransientBackNavigationService owner, Func<Task> handler) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(owner._handler, handler))
            {
                owner._handler = null;
            }
        }
    }
}
