namespace AdventuresSuite.Companion.Poc;

public partial class MainPage : ContentPage
{
	private readonly Services.TransientBackNavigationService _backNavigation;
	private readonly Services.ICompanionAppearanceService _appearance;

	public MainPage(
		Services.TransientBackNavigationService backNavigation,
		Services.ICompanionAppearanceService appearance)
	{
		InitializeComponent();
		_backNavigation = backNavigation;
		_appearance = appearance;
		ApplyBootstrapBackground();
		_appearance.Changed += OnAppearanceChanged;
	}

	protected override bool OnBackButtonPressed() => _backNavigation.TryHandleBack() || base.OnBackButtonPressed();

	private void OnAppearanceChanged(object? sender, EventArgs args) => ApplyBootstrapBackground();

	private void ApplyBootstrapBackground()
	{
		var color = _appearance.EffectivePalette == Services.CompanionPalette.Dark ? "#101815" : "#f4f1e9";
		BackgroundColor = Color.FromArgb(color);
		blazorWebView.BackgroundColor = BackgroundColor;
	}
}
