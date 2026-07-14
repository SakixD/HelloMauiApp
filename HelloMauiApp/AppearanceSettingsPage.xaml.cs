using HelloMauiApp.Services;

namespace HelloMauiApp;

public partial class AppearanceSettingsPage : ContentPage
{
	bool _suppressThemeChanged;
	readonly List<Border> _accentSwatches = [];

	public AppearanceSettingsPage()
	{
		InitializeComponent();
		LoadTheme();
		BuildAccentSwatches();
	}

	void LoadTheme()
	{
		_suppressThemeChanged = true;
		switch (AppearanceService.Instance.Theme)
		{
			case AppThemePreference.Light:
				ThemeLightRadio.IsChecked = true;
				break;
			case AppThemePreference.Dark:
				ThemeDarkRadio.IsChecked = true;
				break;
			default:
				ThemeSystemRadio.IsChecked = true;
				break;
		}
		_suppressThemeChanged = false;
	}

	void OnThemeRadioChanged(object? sender, CheckedChangedEventArgs e)
	{
		if (_suppressThemeChanged || !e.Value)
			return;

		var theme = sender switch
		{
			_ when ReferenceEquals(sender, ThemeLightRadio) => AppThemePreference.Light,
			_ when ReferenceEquals(sender, ThemeDarkRadio) => AppThemePreference.Dark,
			_ => AppThemePreference.System,
		};
		AppearanceService.Instance.SetTheme(theme);
	}

	void BuildAccentSwatches()
	{
		AccentSwatchLayout.Children.Clear();
		_accentSwatches.Clear();

		for (int i = 0; i < AppearanceService.AccentPresets.Length; i++)
		{
			int index = i;
			var swatch = new Border
			{
				WidthRequest = 36,
				HeightRequest = 36,
				StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
				BackgroundColor = AppearanceService.AccentPresets[i],
				StrokeThickness = index == AppearanceService.Instance.AccentIndex ? 3 : 0,
				Stroke = Colors.White,
			};
			swatch.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => OnAccentSelected(index)) });
			_accentSwatches.Add(swatch);
			AccentSwatchLayout.Children.Add(swatch);
		}
	}

	void OnAccentSelected(int index)
	{
		AppearanceService.Instance.SetAccent(index);
		for (int i = 0; i < _accentSwatches.Count; i++)
			_accentSwatches[i].StrokeThickness = i == index ? 3 : 0;
	}

	async void OnOpenPrinterSettingsClicked(object? sender, EventArgs e) => await Navigation.PushAsync(new PrinterSettingsPage());

	async void OnOpenDeviceSettingsClicked(object? sender, EventArgs e) => await Navigation.PushAsync(new PrinterDeviceSettingsPage());
}
