namespace AdventuresSuite.Companion.Poc;

public partial class App : Application
{
	private readonly MainPage _mainPage;

	public App(
		MainPage mainPage,
		Services.ICompanionAppearanceService appearance,
		Services.MauiSystemAppearanceSource systemAppearance)
	{
		InitializeComponent();
		_mainPage = mainPage;
		systemAppearance.Attach(this);
		ApplyNativeAppearance(appearance);
		appearance.Changed += (_, _) => ApplyNativeAppearance(appearance);
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_mainPage) { Title = "AdventuresCompanion" };
	}

	private void ApplyNativeAppearance(Services.ICompanionAppearanceService appearance)
	{
		UserAppTheme = appearance.Preference switch
		{
			Services.CompanionAppearancePreference.Light => AppTheme.Light,
			Services.CompanionAppearancePreference.Dark => AppTheme.Dark,
			_ => AppTheme.Unspecified
		};
	}
}
