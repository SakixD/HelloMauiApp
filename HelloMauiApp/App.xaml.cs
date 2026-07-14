using HelloMauiApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HelloMauiApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		AppearanceService.Instance.Initialize();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new NavigationPage(new AppShell()));
	}
}