using HelloMauiApp.ViewModels;

namespace HelloMauiApp;

public partial class AppearanceSettingsPage : ContentPage
{
	public AppearanceSettingsPage(AppearanceSettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
