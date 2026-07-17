using HelloMauiApp.Services;
using LabelPrinting.Services;

namespace HelloMauiApp;

/// <summary>
/// Rohes ZPL senden/abfragen – 1:1 aus der alten <c>MainPage</c> hierher verlagert, als die Startseite
/// auf das neue Design umgebaut wurde (siehe AppShell). Noch nicht auf das neue Design gestylt
/// (folgt in einer späteren Phase); Funktionalität bleibt unverändert erhalten.
/// </summary>
public partial class ZplConsolePage : ContentView, IShellSectionView
{
	readonly IPrinterService _printerService;
	readonly IPrinterProfileStore _profileStore;
	readonly IAlertService _alertService;

	public ZplConsolePage(IPrinterService printerService, IPrinterProfileStore profileStore, IAlertService alertService)
	{
		InitializeComponent();
		_printerService = printerService;
		_profileStore = profileStore;
		_alertService = alertService;
	}

	public Task OnActivatedAsync() => Task.CompletedTask;

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

	/// <summary>Aktiver Drucker = Default-Profil; ohne Profil gibt es den einheitlichen Hinweis.</summary>
	async Task<LabelPrinting.Models.PrinterProfile?> RequireProfileAsync()
	{
		if (_profileStore.GetDefault() is { } profile)
			return profile;

		await _alertService.ShowAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ ein Druckerprofil anlegen.", "OK");
		return null;
	}

	async void OnSendZplClicked(object? sender, EventArgs e)
	{
		if (await RequireProfileAsync() is not { } profile)
			return;
		if (string.IsNullOrWhiteSpace(ZplEditor.Text))
		{
			await _alertService.ShowAsync("Kein Inhalt", "Bitte ZPL-Code einfügen (z.B. von der Versanddienstleister-API).", "OK");
			return;
		}

		SetBusy(true);
		var result = await _printerService.SendZplAsync(profile, ZplEditor.Text);
		SetBusy(false);

		await _alertService.ShowAsync(
			result.Success ? "Gesendet" : "Fehler",
			result.Success ? "ZPL wurde an den Drucker gesendet." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}

	async void OnSendZplQueryClicked(object? sender, EventArgs e)
	{
		if (await RequireProfileAsync() is not { } profile)
			return;
		if (string.IsNullOrWhiteSpace(ZplEditor.Text))
		{
			await _alertService.ShowAsync("Kein Inhalt", "Bitte einen Befehl eingeben, z.B. ~HS oder ! U1 getvar \"media.type\"", "OK");
			return;
		}

		SetBusy(true);
		var result = await _printerService.QueryAsync(profile, ZplEditor.Text);
		SetBusy(false);

		ResponseEditor.Text = result.Success ? FormatQueryResponse(result.ResponseText) : $"Fehler: {result.ErrorMessage}";
	}

	async void OnQueryStatusClicked(object? sender, EventArgs e)
	{
		if (await RequireProfileAsync() is not { } profile)
			return;

		SetBusy(true);
		var result = await _printerService.GetStatusAsync(profile);
		SetBusy(false);

		ResponseEditor.Text = result.Success ? FormatQueryResponse(result.ResponseText) : $"Fehler: {result.ErrorMessage}";
	}

	async void OnPrintTestLabelClicked(object? sender, EventArgs e)
	{
		if (await RequireProfileAsync() is not { } profile)
			return;

		string label = LabelSamples.CreateTestLabelZpl(profile);

		SetBusy(true);
		var result = await _printerService.SendZplAsync(profile, label);
		SetBusy(false);

		await _alertService.ShowAsync(
			result.Success ? "Gesendet" : "Fehler",
			result.Success ? "Testlabel wurde an den Drucker gesendet." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}
}
