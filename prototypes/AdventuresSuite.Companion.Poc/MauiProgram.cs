using Microsoft.Extensions.Logging;

namespace AdventuresSuite.Companion.Poc;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddSingleton<Services.CompanionContentService>();
		builder.Services.AddSingleton<Services.PlaybookContentService>();
		builder.Services.AddSingleton<Services.IAppearancePreferenceStore, Services.MauiAppearancePreferenceStore>();
		builder.Services.AddSingleton<Services.ISystemAppearanceSource, Services.MauiSystemAppearanceSource>();
		builder.Services.AddSingleton<Services.ICompanionAppearanceService, Services.CompanionAppearanceService>();
		builder.Services.AddSingleton<Services.ICompanionUiDispatcher, Services.MauiCompanionUiDispatcher>();
		builder.Services.AddSingleton<Services.TransientBackNavigationService>();
		builder.Services.AddSingleton<MainPage>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
