using LabelPrinting.Services;

namespace HelloMauiApp;

/// <summary>
/// Rohes ZPL senden/abfragen – 1:1 aus der alten <c>MainPage</c> hierher verlagert, als die Startseite
/// auf das neue Design umgebaut wurde (siehe AppShell). Noch nicht auf das neue Design gestylt
/// (folgt in einer späteren Phase); Funktionalität bleibt unverändert erhalten.
/// </summary>
public partial class ZplConsolePage : ContentPage
{
	readonly IPrinterService _printerService = new ZplPrinterService();
	readonly PrinterSettingsStore _settingsStore = new();

	public ZplConsolePage()
	{
		InitializeComponent();
	}

	static string FormatQueryResponse(string raw)
	{
		if (string.IsNullOrEmpty(raw))
			return "(keine Antwort empfangen)";

		return raw.Replace((char)0x02, '⟨').Replace((char)0x03, '⟩');
	}

	void SetBusy(bool busy)
	{
		BusyIndicator.IsVisible = busy;
		BusyIndicator.IsRunning = busy;
		SendZplBtn.IsEnabled = !busy;
		SendZplQueryBtn.IsEnabled = !busy;
		QueryStatusBtn.IsEnabled = !busy;
		PrintTestLabelBtn.IsEnabled = !busy;
	}

	bool RequirePrinterConfigured(LabelPrinting.Models.PrinterSettings settings)
		=> !string.IsNullOrWhiteSpace(settings.IpAddress);

	async void OnSendZplClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ die IP-Adresse eintragen.", "OK");
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
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ die IP-Adresse eintragen.", "OK");
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

		ResponseEditor.Text = result.Success ? FormatQueryResponse(result.ResponseText) : $"Fehler: {result.ErrorMessage}";
	}

	async void OnQueryStatusClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		SetBusy(true);
		var result = await _printerService.GetStatusAsync(settings.IpAddress, settings.Port);
		SetBusy(false);

		ResponseEditor.Text = result.Success ? FormatQueryResponse(result.ResponseText) : $"Fehler: {result.ErrorMessage}";
	}

	async void OnPrintTestLabelClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ die IP-Adresse eintragen.", "OK");
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
}
