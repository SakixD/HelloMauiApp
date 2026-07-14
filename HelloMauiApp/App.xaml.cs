using HelloMauiApp.Services;
using HelloMauiApp.Services.Api;

namespace HelloMauiApp;

public partial class App : Application
{
	readonly AppShell _shell;

	public App(AppShell shell, AppearanceService appearanceService, LocalApiServer apiServer)
	{
		InitializeComponent();
		appearanceService.Initialize();
		_shell = shell;

		// Lokale API sofort verfügbar machen (nur localhost, siehe LocalApiServer).
		// Ein Fehlstart (z.B. Port belegt) crasht die App nicht – die Startseite zeigt den Grund.
		apiServer.Start();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new NavigationPage(_shell)) { Title = "Label Printing SDK" };
	}
}
