using HelloMauiApp.Services;
using HelloMauiApp.ViewModels;
using LabelPrinting.Services;
using Microsoft.Extensions.Logging;

namespace HelloMauiApp;

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
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// ---------- SDK-Services (LabelPrinting) ----------
		builder.Services.AddSingleton<IPrinterSettingsStore, PrinterSettingsStore>();
		builder.Services.AddSingleton<IPrintMediaStore, PrintMediaStore>();
		builder.Services.AddSingleton<ILabelTemplateStore, LabelTemplateStore>();
		builder.Services.AddSingleton<IPrinterService, ZplPrinterService>();

		// ---------- App-Infrastruktur ----------
		builder.Services.AddSingleton<AppearanceService>();
		builder.Services.AddSingleton<INavigationService, NavigationService>();
		builder.Services.AddSingleton<IAlertService, AlertService>();

		// ---------- Shell + Start (dauerhaft, ein Exemplar für die App-Laufzeit) ----------
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddSingleton<MainPageViewModel>();
		builder.Services.AddSingleton<MainPage>();

		// ---------- Einstellungen (frisch pro Navigation) ----------
		builder.Services.AddTransient<AppearanceSettingsViewModel>();
		builder.Services.AddTransient<AppearanceSettingsPage>();

		return builder.Build();
	}
}
