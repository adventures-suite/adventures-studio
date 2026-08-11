namespace AdventuresSuite.Companion.Poc.Services;

/// <summary>Owns the lifecycle of one transient selector and its Back registration.</summary>
/// <typeparam name="TItem">The selector item type.</typeparam>
public sealed class TransientSelectorState<TItem>(TransientBackNavigationService backNavigation) : IDisposable
{
	private IDisposable? _backRegistration;

	/// <summary>Gets whether the selector's visual and hit-test layers are open.</summary>
	public bool IsOpen { get; private set; }

	/// <summary>Opens the selector and registers its shared dismissal operation for Back.</summary>
	/// <param name="dismissAsync">The selector's idempotent dismissal operation.</param>
	public void Open(Func<Task> dismissAsync)
	{
		if (IsOpen)
		{
			return;
		}

		IsOpen = true;
		_backRegistration = backNavigation.Register(dismissAsync);
	}

	/// <summary>Commits one selection exactly once and then dismisses the selector.</summary>
	public async Task SelectAsync(
		TItem item,
		Func<TItem, Task> commitAsync,
		Func<Task> renderClosedAsync,
		Func<Task> restoreFocusAsync)
	{
		if (!IsOpen)
		{
			return;
		}

		await commitAsync(item);
		await DismissAsync(renderClosedAsync, restoreFocusAsync);
	}

	/// <summary>Atomically closes the selector, removes Back handling, renders, and restores focus.</summary>
	public async Task DismissAsync(Func<Task> renderClosedAsync, Func<Task> restoreFocusAsync)
	{
		if (!IsOpen)
		{
			return;
		}

		IsOpen = false;
		_backRegistration?.Dispose();
		_backRegistration = null;
		await renderClosedAsync();
		await restoreFocusAsync();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		IsOpen = false;
		_backRegistration?.Dispose();
		_backRegistration = null;
	}
}
