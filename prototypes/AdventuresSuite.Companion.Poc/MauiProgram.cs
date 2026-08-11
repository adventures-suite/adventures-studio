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

		var providerName = Environment.GetEnvironmentVariable("ADVENTURES_COMPANION_CONTENT_PROVIDER");
		if (!Enum.TryParse<Models.CompanionContentProviderKind>(providerName, ignoreCase: true, out var providerKind))
		{
			throw new InvalidOperationException(
				"ADVENTURES_COMPANION_CONTENT_PROVIDER must explicitly select Demo or Api.");
		}

		builder.Services.AddMauiBlazorWebView();
		if (providerKind == Models.CompanionContentProviderKind.Demo)
		{
			builder.Services.AddSingleton<Services.ICompanionContentProvider, Services.CompanionContentService>();
		}
		else
		{
			var baseAddressValue = Environment.GetEnvironmentVariable("ADVENTURES_COMPANION_API_BASE_ADDRESS");
			if (!Uri.TryCreate(baseAddressValue, UriKind.Absolute, out var baseAddress) || baseAddress.Scheme != Uri.UriSchemeHttps)
			{
				throw new InvalidOperationException(
					"ADVENTURES_COMPANION_API_BASE_ADDRESS must be an absolute HTTPS URI when Api is selected.");
			}

			builder.Services.AddSingleton(new HttpClient { BaseAddress = baseAddress });
			builder.Services.AddSingleton<AdventuresSuite.Companion.Client.ICompanionAdventureListTransport,
				AdventuresSuite.Companion.Client.HttpCompanionAdventureListTransport>();
			builder.Services.AddSingleton<AdventuresSuite.Companion.Client.ICompanionAdventureListService,
				AdventuresSuite.Companion.Client.CompanionAdventureListService>();
			builder.Services.AddSingleton<Services.ICompanionContentProvider, Services.ApiCompanionContentProvider>();
		}
		builder.Services.AddSingleton<Services.PlaybookContentService>();
		builder.Services.AddSingleton<Services.IAppearancePreferenceStore, Services.MauiAppearancePreferenceStore>();
		builder.Services.AddSingleton<Services.MauiSystemAppearanceSource>();
		builder.Services.AddSingleton<Services.ISystemAppearanceSource>(services => services.GetRequiredService<Services.MauiSystemAppearanceSource>());
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
