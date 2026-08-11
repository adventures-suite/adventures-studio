namespace AdventuresSuite.Companion.Poc.Services;

/// <summary>Dispatches callbacks through the MAUI main thread.</summary>
public sealed class MauiCompanionUiDispatcher : ICompanionUiDispatcher
{
	/// <inheritdoc />
	public void Dispatch(Func<Task> callback) => _ = MainThread.InvokeOnMainThreadAsync(callback);
}
