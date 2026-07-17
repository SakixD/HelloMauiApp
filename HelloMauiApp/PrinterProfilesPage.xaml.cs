using HelloMauiApp.ViewModels;

namespace HelloMauiApp;

/// <summary>
/// Drill-down "Druckerprofile" (aus den Einstellungen). Wird pro Navigation frisch erzeugt; das
/// ViewModel kommt vom Aufrufer (AppearanceSettingsViewModel reicht die DI-Services durch).
/// </summary>
public partial class PrinterProfilesPage : ContentPage
{
	readonly PrinterProfilesViewModel _viewModel;

	public PrinterProfilesPage(PrinterProfilesViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.RefreshAsync();
	}
}
