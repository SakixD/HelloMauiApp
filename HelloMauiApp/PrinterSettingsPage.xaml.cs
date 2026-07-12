using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp;

public partial class PrinterSettingsPage : ContentPage
{
	readonly IPrinterService _printerService = new ZplPrinterService();
	readonly PrinterSettingsStore _settingsStore = new();
	readonly PrintMediaStore _mediaStore = new();

	List<PrintMedia> _mediaList = [];
	bool _suppressMediaPickerChanged;

	public PrinterSettingsPage()
	{
		InitializeComponent();
		LoadSettings();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshMediaPickerAsync();
	}

	async Task RefreshMediaPickerAsync()
	{
		_mediaList = await _mediaStore.ListAsync();

		_suppressMediaPickerChanged = true;
		MediaPicker.ItemsSource = _mediaList.Select(m => $"{m.Name} ({m.WidthMm:0.#}×{m.HeightMm:0.#} mm)").ToList();
		MediaPicker.SelectedIndex = -1;
		_suppressMediaPickerChanged = false;
	}

	void OnMediaPickerChanged(object? sender, EventArgs e)
	{
		if (_suppressMediaPickerChanged || MediaPicker.SelectedIndex < 0 || MediaPicker.SelectedIndex >= _mediaList.Count)
			return;

		var media = _mediaList[MediaPicker.SelectedIndex];
		WidthEntry.Text = media.WidthMm.ToString("0.##");
		HeightEntry.Text = media.HeightMm.ToString("0.##");
	}

	void LoadSettings()
	{
		var settings = _settingsStore.Load();
		IpEntry.Text = settings.IpAddress;
		PortEntry.Text = settings.Port.ToString();
		WidthEntry.Text = settings.LabelWidthMm.ToString("0.##");
		HeightEntry.Text = settings.LabelHeightMm.ToString("0.##");
		DpiEntry.Text = settings.Dpi.ToString();
	}

	bool TryReadSettings(out PrinterSettings settings, out string? error)
	{
		settings = new PrinterSettings();
		error = null;

		if (string.IsNullOrWhiteSpace(IpEntry.Text))
		{
			error = "Bitte eine IP-Adresse eingeben.";
			return false;
		}

		if (!int.TryParse(PortEntry.Text, out int port) || port is <= 0 or > 65535)
		{
			error = "Bitte einen gültigen Port (1-65535) eingeben.";
			return false;
		}

		if (!double.TryParse(WidthEntry.Text, out double width) || width <= 0)
		{
			error = "Bitte eine gültige Labelbreite eingeben.";
			return false;
		}

		if (!double.TryParse(HeightEntry.Text, out double height) || height <= 0)
		{
			error = "Bitte eine gültige Labelhöhe eingeben.";
			return false;
		}

		if (!int.TryParse(DpiEntry.Text, out int dpi) || dpi <= 0)
		{
			error = "Bitte eine gültige Auflösung (DPI) eingeben.";
			return false;
		}

		settings = new PrinterSettings
		{
			IpAddress = IpEntry.Text.Trim(),
			Port = port,
			LabelWidthMm = width,
			LabelHeightMm = height,
			Dpi = dpi,
		};
		return true;
	}

	async void OnSaveClicked(object? sender, EventArgs e)
	{
		if (!TryReadSettings(out var settings, out var error))
		{
			await DisplayAlertAsync("Ungültige Eingabe", error!, "OK");
			return;
		}

		_settingsStore.Save(settings);
		StatusLabel.Text = "Gespeichert.";
	}

	async void OnTestClicked(object? sender, EventArgs e)
	{
		if (!TryReadSettings(out var settings, out var error))
		{
			await DisplayAlertAsync("Ungültige Eingabe", error!, "OK");
			return;
		}

		TestBtn.IsEnabled = false;
		BusyIndicator.IsVisible = true;
		BusyIndicator.IsRunning = true;
		StatusLabel.Text = "Teste Verbindung...";

		var result = await _printerService.TestConnectionAsync(settings.IpAddress, settings.Port);

		BusyIndicator.IsVisible = false;
		BusyIndicator.IsRunning = false;
		TestBtn.IsEnabled = true;
		StatusLabel.Text = result.Success
			? $"Verbindung zu {settings.IpAddress}:{settings.Port} erfolgreich."
			: result.ErrorMessage;
	}
}
