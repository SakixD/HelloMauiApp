using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp;

public partial class MainPage : ContentPage
{
	readonly IPrinterService _printerService = new ZplPrinterService();
	readonly PrinterSettingsStore _settingsStore = new();

	public MainPage()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		UpdatePrinterStatusLabel();
	}

	void UpdatePrinterStatusLabel()
	{
		var settings = _settingsStore.Load();
		PrinterStatusLabel.Text = string.IsNullOrWhiteSpace(settings.IpAddress)
			? "Kein Drucker konfiguriert."
			: $"Drucker: {settings.IpAddress}:{settings.Port}  •  Label {settings.LabelWidthMm}×{settings.LabelHeightMm} mm @ {settings.Dpi} dpi";
	}

	bool RequirePrinterConfigured(PrinterSettings settings)
		=> !string.IsNullOrWhiteSpace(settings.IpAddress);

	void SetBusy(bool busy)
	{
		BusyIndicator.IsVisible = busy;
		BusyIndicator.IsRunning = busy;
		SettingsBtn.IsEnabled = !busy;
		TestConnectionBtn.IsEnabled = !busy;
		PrintTestLabelBtn.IsEnabled = !busy;
		PickImageBtn.IsEnabled = !busy;
		SendZplBtn.IsEnabled = !busy;
		SendZplQueryBtn.IsEnabled = !busy;
		QueryStatusBtn.IsEnabled = !busy;
		CalibrateMediaBtn.IsEnabled = !busy;
	}

	static string FormatQueryResponse(string raw)
	{
		if (string.IsNullOrEmpty(raw))
			return "(keine Antwort empfangen)";

		// Steuerzeichen (STX/ETX etc.) sichtbar machen, damit die Rohantwort les- und kopierbar bleibt.
		return raw.Replace((char)0x02, '⟨').Replace((char)0x03, '⟩');
	}

	async void OnSettingsClicked(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new PrinterSettingsPage());
		UpdatePrinterStatusLabel();
	}

	async void OnOpenDesignerClicked(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new DesignerPage());
	}

	async void OnOpenTemplateTestClicked(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new TemplateTestPage());
	}

	async void OnOpenTemplateManagerClicked(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new TemplateManagerPage());
	}

	async void OnTestConnectionClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Drucker-Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		SetBusy(true);
		var result = await _printerService.TestConnectionAsync(settings.IpAddress, settings.Port);
		SetBusy(false);

		await DisplayAlertAsync(
			result.Success ? "Verbindung OK" : "Verbindung fehlgeschlagen",
			result.Success ? $"Drucker unter {settings.IpAddress}:{settings.Port} ist erreichbar." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}

	async void OnPrintTestLabelClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Drucker-Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		string label = LabelSamples.CreateTestLabelZpl(settings);

		SetBusy(true);
		var result = await _printerService.SendZplAsync(settings.IpAddress, settings.Port, label);
		SetBusy(false);

		await DisplayAlertAsync(
			result.Success ? "Gesendet" : "Fehler",
			result.Success ? "Testlabel wurde an den Drucker gesendet." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}

	async void OnPickImageClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Drucker-Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		FileResult? file;
		try
		{
			file = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "Bild für Label auswählen",
				FileTypes = FilePickerFileType.Images,
			});
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Fehler", $"Bild konnte nicht ausgewählt werden: {ex.Message}", "OK");
			return;
		}

		if (file is null)
			return;

		SetBusy(true);
		try
		{
			using var stream = await file.OpenReadAsync();
			using var ms = new MemoryStream();
			await stream.CopyToAsync(ms);

			string label = LabelSamples.CreateImageLabelZpl(settings, ms.ToArray());

			var result = await _printerService.SendZplAsync(settings.IpAddress, settings.Port, label);

			await DisplayAlertAsync(
				result.Success ? "Gesendet" : "Fehler",
				result.Success ? "Bild wurde als Label an den Drucker gesendet." : result.ErrorMessage ?? "Unbekannter Fehler",
				"OK");
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Fehler", $"Bild konnte nicht gedruckt werden: {ex.Message}", "OK");
		}
		finally
		{
			SetBusy(false);
		}
	}

	async void OnSendZplClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Drucker-Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		if (string.IsNullOrWhiteSpace(ZplEditor.Text))
		{
			await DisplayAlertAsync("Kein Inhalt", "Bitte ZPL-Code einfügen (z.B. von der Versanddienstleister-API).", "OK");
			return;
		}

		SetBusy(true);
		var result = await _printerService.SendZplAsync(settings.IpAddress, settings.Port, ZplEditor.Text);
		SetBusy(false);

		await DisplayAlertAsync(
			result.Success ? "Gesendet" : "Fehler",
			result.Success ? "ZPL wurde an den Drucker gesendet." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}

	async void OnSendZplQueryClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Drucker-Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		if (string.IsNullOrWhiteSpace(ZplEditor.Text))
		{
			await DisplayAlertAsync("Kein Inhalt", "Bitte einen Befehl eingeben, z.B. ~HS oder ! U1 getvar \"media.type\"", "OK");
			return;
		}

		SetBusy(true);
		var result = await _printerService.QueryAsync(settings.IpAddress, settings.Port, ZplEditor.Text);
		SetBusy(false);

		ResponseEditor.Text = result.Success
			? FormatQueryResponse(result.ResponseText)
			: $"Fehler: {result.ErrorMessage}";
	}

	async void OnQueryStatusClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Drucker-Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		SetBusy(true);
		var result = await _printerService.GetStatusAsync(settings.IpAddress, settings.Port);
		SetBusy(false);

		ResponseEditor.Text = result.Success
			? FormatQueryResponse(result.ResponseText)
			: $"Fehler: {result.ErrorMessage}";
	}

	async void OnCalibrateMediaClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Drucker-Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		bool confirmed = await DisplayAlertAsync(
			"Medium kalibrieren",
			"Der Drucker zieht dabei mehrere Etiketten durch, um Etikettenlänge sowie Lücken-/Schwarzmarkenposition automatisch zu erkennen. Fortfahren?",
			"Ja, kalibrieren",
			"Abbrechen");

		if (!confirmed)
			return;

		SetBusy(true);
		var result = await _printerService.CalibrateMediaAsync(settings.IpAddress, settings.Port);
		SetBusy(false);

		await DisplayAlertAsync(
			result.Success ? "Gesendet" : "Fehler",
			result.Success ? "Kalibrierbefehl wurde gesendet. Der Drucker sollte jetzt Etiketten durchziehen." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}
}
