using System.Reflection;
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

		var metadata = typeof(MauiProgram).Assembly
			.GetCustomAttributes<AssemblyMetadataAttribute>()
			.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
		var providerSettings = Services.CompanionProviderConfiguration.Resolve(
			Environment.GetEnvironmentVariable("ADVENTURES_COMPANION_CONTENT_PROVIDER"),
			Environment.GetEnvironmentVariable("ADVENTURES_COMPANION_API_BASE_ADDRESS"),
			metadata.GetValueOrDefault("AdventuresCompanion.ContentProvider"),
			metadata.GetValueOrDefault("AdventuresCompanion.ApiBaseAddress"));

		builder.Services.AddMauiBlazorWebView();
		if (providerSettings.Provider == Models.CompanionContentProviderKind.Demo)
		{
			builder.Services.AddSingleton<Services.CompanionContentService>();
			builder.Services.AddSingleton<Services.ICompanionContentProvider>(services =>
				services.GetRequiredService<Services.CompanionContentService>());
			builder.Services.AddSingleton<Services.ICompanionAdventureDetailProvider,
				Services.DemoCompanionAdventureDetailProvider>();
		}
		else
		{
			builder.Services.AddSingleton(new HttpClient { BaseAddress = providerSettings.ApiBaseAddress });
			builder.Services.AddSingleton<AdventuresSuite.Companion.Client.ICompanionAdventureListTransport,
				AdventuresSuite.Companion.Client.HttpCompanionAdventureListTransport>();
			builder.Services.AddSingleton<AdventuresSuite.Companion.Client.ICompanionAdventureListService,
				AdventuresSuite.Companion.Client.CompanionAdventureListService>();
			builder.Services.AddSingleton<Services.ICompanionContentProvider, Services.ApiCompanionContentProvider>();
			builder.Services.AddSingleton<AdventuresSuite.Companion.Client.ICompanionAdventureDetailTransport,
				AdventuresSuite.Companion.Client.HttpCompanionAdventureDetailTransport>();
			builder.Services.AddSingleton<AdventuresSuite.Companion.Client.ICompanionAdventureDetailService,
				AdventuresSuite.Companion.Client.CompanionAdventureDetailService>();
			builder.Services.AddSingleton<Services.ICompanionAdventureDetailProvider,
				Services.ApiCompanionAdventureDetailProvider>();
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
