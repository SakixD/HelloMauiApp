using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp;

public partial class PrinterSettingsPage : ContentPage
{
	readonly IPrinterService _printerService = new ZplPrinterService();
	readonly PrinterSettingsStore _settingsStore = new();

	public PrinterSettingsPage()
	{
		InitializeComponent();
		LoadSettings();
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
