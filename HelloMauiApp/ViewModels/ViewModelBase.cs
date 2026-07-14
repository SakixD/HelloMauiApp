using CommunityToolkit.Mvvm.ComponentModel;

namespace HelloMauiApp.ViewModels;

/// <summary>Gemeinsame Basis für Seiten-ViewModels (Quellgenerator-basiert über CommunityToolkit.Mvvm).</summary>
public abstract partial class ViewModelBase : ObservableObject
{
	[ObservableProperty]
	bool isBusy;
}
