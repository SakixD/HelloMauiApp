using HelloMauiApp.ViewModels;

namespace HelloMauiApp;

/// <summary>Startseite ("Start" in der Rail) – reine View, alle Logik/Zustand steckt in <see cref="ViewModel"/>.</summary>
public partial class MainPage : ContentView
{
	public MainPageViewModel ViewModel { get; }

	public MainPage(MainPageViewModel viewModel)
	{
		InitializeComponent();
		ViewModel = viewModel;
		BindingContext = viewModel;
	}
}
