using HelloMauiApp.Services;
using HelloMauiApp.ViewModels;

namespace HelloMauiApp;

/// <summary>Vorlagenübersicht (Rail-Ziel "templates"). Logik siehe <see cref="TemplateManagerViewModel"/>.</summary>
public partial class TemplateManagerPage : ContentView, IShellSectionView
{
	public TemplateManagerViewModel ViewModel { get; }

	public TemplateManagerPage(TemplateManagerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
	}

	public Task OnActivatedAsync() => ViewModel.OnActivatedAsync();
}
