using AdventuresSuite.Companion.Poc.Services;

namespace AdventuresSuite.Companion.Poc.Tests;

public sealed class TransientSelectorStateTests
{
	[Fact]
	public async Task DismissalClosesRemovesBackAndRestoresFocusExactlyOnce()
	{
		var back = CreateBackService();
		var selector = new TransientSelectorState<string>(back);
		var renders = 0;
		var focusRestorations = 0;
		Task Dismiss() => selector.DismissAsync(
			() => { renders++; return Task.CompletedTask; },
			() => { focusRestorations++; return Task.CompletedTask; });
		selector.Open(Dismiss);

		await Dismiss();
		await Dismiss();

		Assert.False(selector.IsOpen);
		Assert.False(back.TryHandleBack());
		Assert.Equal(1, renders);
		Assert.Equal(1, focusRestorations);
	}

	[Fact]
	public async Task SelectionCommitsOnceBeforeCloseAndFocusRestoration()
	{
		var selector = new TransientSelectorState<string>(CreateBackService());
		var events = new List<string>();
		Task Dismiss() => selector.DismissAsync(
			() => { events.Add("render"); return Task.CompletedTask; },
			() => { events.Add("focus"); return Task.CompletedTask; });
		selector.Open(Dismiss);

		await selector.SelectAsync(
			"adventure-two",
			item => { events.Add($"select:{item}"); return Task.CompletedTask; },
			() => { events.Add("render"); return Task.CompletedTask; },
			() => { events.Add("focus"); return Task.CompletedTask; });
		await selector.SelectAsync("ignored", _ => throw new InvalidOperationException(), () => Task.CompletedTask, () => Task.CompletedTask);

		Assert.Equal(["select:adventure-two", "render", "focus"], events);
		Assert.False(selector.IsOpen);
	}

	[Fact]
	public void BackConsumesOnlyWhileOpenAndUsesSharedDismissal()
	{
		var back = CreateBackService();
		var selector = new TransientSelectorState<string>(back);
		var dismissals = 0;
		selector.Open(() => selector.DismissAsync(
			() => { dismissals++; return Task.CompletedTask; },
			() => Task.CompletedTask));

		Assert.True(back.TryHandleBack());
		Assert.False(back.TryHandleBack());
		Assert.Equal(1, dismissals);
		Assert.False(selector.IsOpen);
	}

	[Fact]
	public void DisposalRemovesBackWithoutRenderingOrRestoringFocus()
	{
		var back = CreateBackService();
		var selector = new TransientSelectorState<string>(back);
		var dismissals = 0;
		selector.Open(() => { dismissals++; return Task.CompletedTask; });

		selector.Dispose();
		selector.Dispose();

		Assert.False(selector.IsOpen);
		Assert.False(back.TryHandleBack());
		Assert.Equal(0, dismissals);
	}

	[Fact]
	public void ReopeningDoesNotCreateDuplicateBackRegistration()
	{
		var back = CreateBackService();
		var selector = new TransientSelectorState<string>(back);
		var dismissals = 0;
		Task Dismiss() => selector.DismissAsync(
			() => { dismissals++; return Task.CompletedTask; },
			() => Task.CompletedTask);

		selector.Open(Dismiss);
		selector.Open(Dismiss);
		Assert.True(back.TryHandleBack());

		Assert.Equal(1, dismissals);
		Assert.False(back.TryHandleBack());
	}

	private static TransientBackNavigationService CreateBackService() => new(new ImmediateDispatcher());

	private sealed class ImmediateDispatcher : ICompanionUiDispatcher
	{
		public void Dispatch(Func<Task> callback) => callback().GetAwaiter().GetResult();
	}
}
