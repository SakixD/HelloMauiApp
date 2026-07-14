using HelloMauiApp.Services;
using HelloMauiApp.ViewModels;

namespace HelloMauiApp;

/// <summary>Eigenständige Platzhalterübersicht (Rail-Ziel "placeholders"). Logik siehe <see cref="PlaceholderLibraryViewModel"/>.</summary>
public partial class PlaceholderLibraryPage : ContentView, IShellSectionView
{
	public PlaceholderLibraryViewModel ViewModel { get; }

	public PlaceholderLibraryPage(PlaceholderLibraryViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
	}

	public Task OnActivatedAsync() => ViewModel.OnActivatedAsync();
}
