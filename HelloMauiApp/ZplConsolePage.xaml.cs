using HelloMauiApp.Services;
using HelloMauiApp.ViewModels;

namespace HelloMauiApp;

/// <summary>
/// Rohes ZPL senden/abfragen (Rail-Ziel "zpl"). Logik siehe <see cref="ZplConsoleViewModel"/>;
/// die Konsole selbst wurde 1:1 aus der alten <c>MainPage</c> hierher verlagert, als die
/// Startseite auf das neue Design umgebaut wurde (siehe AppShell).
/// </summary>
public partial class ZplConsolePage : ContentView, IShellSectionView
{
	public ZplConsolePage(ZplConsoleViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	public Task OnActivatedAsync() => Task.CompletedTask;
}
