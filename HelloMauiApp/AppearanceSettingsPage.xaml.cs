using HelloMauiApp.Services;
using HelloMauiApp.ViewModels;

namespace HelloMauiApp;

public partial class AppearanceSettingsPage : ContentView, IShellSectionView
{
	readonly AppearanceSettingsViewModel _viewModel;

	public AppearanceSettingsPage(AppearanceSettingsViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
	}

	public Task OnActivatedAsync()
	{
		_viewModel.Resync();
		return Task.CompletedTask;
	}
}
