using HelloMauiApp.Services;
using HelloMauiApp.ViewModels;

namespace HelloMauiApp;

/// <summary>Eigenständige Medienverwaltung (Rail-Ziel "media"). Logik siehe <see cref="MediaLibraryViewModel"/>.</summary>
public partial class MediaLibraryPage : ContentView, IShellSectionView
{
	readonly MediaLibraryViewModel _vm;

	public MediaLibraryPage(MediaLibraryViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _vm = viewModel;
	}

	public Task OnActivatedAsync() => _vm.RefreshAsync();
}
