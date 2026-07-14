using HelloMauiApp.Services;

namespace HelloMauiApp;

public partial class App : Application
{
	readonly AppShell _shell;

	public App(AppShell shell, AppearanceService appearanceService)
	{
		InitializeComponent();
		appearanceService.Initialize();
		_shell = shell;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new NavigationPage(_shell));
	}
}
