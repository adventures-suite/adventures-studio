namespace AdventuresSuite.Companion.Mobile.Services;

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
	private Application? _application;

	/// <summary>Attaches the adapter after MAUI has constructed its application instance.</summary>
	/// <param name="application">The active MAUI application.</param>
	public void Attach(Application application)
	{
		ArgumentNullException.ThrowIfNull(application);
		if (ReferenceEquals(_application, application))
		{
			return;
		}

		if (_application is not null)
		{
			_application.RequestedThemeChanged -= OnRequestedThemeChanged;
		}

		_application = application;
		_application.RequestedThemeChanged += OnRequestedThemeChanged;
		Changed?.Invoke(this, EventArgs.Empty);
	}

	/// <inheritdoc />
	public event EventHandler? Changed;

	/// <inheritdoc />
	public CompanionPalette CurrentPalette => (_application ?? Application.Current)?.RequestedTheme == AppTheme.Dark
		? CompanionPalette.Dark
		: CompanionPalette.Light;

	/// <inheritdoc />
	public void Dispose()
	{
		if (_application is not null)
		{
			_application.RequestedThemeChanged -= OnRequestedThemeChanged;
			_application = null;
		}
	}

	private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args) => Changed?.Invoke(this, EventArgs.Empty);
}
