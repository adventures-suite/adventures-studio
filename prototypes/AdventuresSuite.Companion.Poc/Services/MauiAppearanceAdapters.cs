namespace AdventuresSuite.Companion.Poc.Services;

/// <summary>Stores appearance through the platform's ordinary preferences facility.</summary>
public sealed class MauiAppearancePreferenceStore : IAppearancePreferenceStore
{
	private const string PreferenceKey = "companion.appearance.v1";

	/// <inheritdoc />
	public string? Get() => Preferences.Default.Get<string?>(PreferenceKey, null);

	/// <inheritdoc />
	public void Set(string value) => Preferences.Default.Set(PreferenceKey, value);
}

/// <summary>Adapts MAUI requested-theme notifications to the host-independent appearance service.</summary>
public sealed class MauiSystemAppearanceSource : ISystemAppearanceSource, IDisposable
{
	/// <summary>Initializes the adapter and begins observing live platform changes.</summary>
	public MauiSystemAppearanceSource()
	{
		if (Application.Current is not null)
		{
			Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
		}
	}

	/// <inheritdoc />
	public event EventHandler? Changed;

	/// <inheritdoc />
	public CompanionPalette CurrentPalette => Application.Current?.RequestedTheme == AppTheme.Dark
		? CompanionPalette.Dark
		: CompanionPalette.Light;

	/// <inheritdoc />
	public void Dispose()
	{
		if (Application.Current is not null)
		{
			Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
		}
	}

	private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args) => Changed?.Invoke(this, EventArgs.Empty);
}
