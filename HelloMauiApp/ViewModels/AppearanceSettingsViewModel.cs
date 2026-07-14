using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloMauiApp.Services;

namespace HelloMauiApp.ViewModels;

public partial class AppearanceSettingsViewModel : ViewModelBase
{
	readonly AppearanceService _appearanceService;
	readonly INavigationService _navigationService;

	[ObservableProperty]
	AppThemePreference selectedTheme;

	public ObservableCollection<AccentSwatch> AccentSwatches { get; }

	public AppearanceSettingsViewModel(AppearanceService appearanceService, INavigationService navigationService)
	{
		_appearanceService = appearanceService;
		_navigationService = navigationService;

		// Direkt ins Feld statt in die Property, damit der Konstruktor nicht selbst schon
		// OnSelectedThemeChanged (und damit einen redundanten SetTheme-Aufruf) auslöst.
		selectedTheme = appearanceService.Theme;

		AccentSwatches = new ObservableCollection<AccentSwatch>(
			AppearanceService.AccentPresets.Select((color, index) => new AccentSwatch(color, index, index == appearanceService.AccentIndex)));
	}

	partial void OnSelectedThemeChanged(AppThemePreference value) => _appearanceService.SetTheme(value);

	[RelayCommand]
	void SelectAccent(AccentSwatch swatch)
	{
		_appearanceService.SetAccent(swatch.Index);
		foreach (var candidate in AccentSwatches)
			candidate.IsSelected = candidate.Index == swatch.Index;
	}

	[RelayCommand]
	Task OpenPrinterSettingsAsync() => _navigationService.PushAsync(new PrinterSettingsPage());

	[RelayCommand]
	Task OpenDeviceSettingsAsync() => _navigationService.PushAsync(new PrinterDeviceSettingsPage());
}
