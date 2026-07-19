using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloMauiApp.Services;
using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp.ViewModels;

/// <summary>
/// ViewModel der ZPL-Konsole (Rail-Ziel "zpl"): rohes ZPL senden, Befehle mit Antwort ausführen,
/// Status abfragen (~HS), Testlabel drucken. Aktiver Drucker ist immer das Default-Profil.
/// </summary>
public partial class ZplConsoleViewModel : ViewModelBase
{
	readonly IPrinterService _printerService;
	readonly IPrinterProfileStore _profileStore;
	readonly IAlertService _alertService;

	[ObservableProperty] string zplText = string.Empty;
	[ObservableProperty] string responseText = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsIdle))]
	bool isSending;

	/// <summary>Buttons sind nur bedienbar, solange keine Drucker-Operation läuft.</summary>
	public bool IsIdle => !IsSending;

	public ZplConsoleViewModel(IPrinterService printerService, IPrinterProfileStore profileStore, IAlertService alertService)
	{
		_printerService = printerService;
		_profileStore = profileStore;
		_alertService = alertService;
	}

	/// <summary>Aktiver Drucker = Default-Profil; ohne Profil gibt es den einheitlichen Hinweis.</summary>
	async Task<PrinterProfile?> RequireProfileAsync()
	{
		if (_profileStore.GetDefault() is { } profile)
			return profile;

		await _alertService.ShowAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ ein Druckerprofil anlegen.", "OK");
		return null;
	}

	static string FormatQueryResponse(string raw)
	{
		if (string.IsNullOrEmpty(raw))
			return "(keine Antwort empfangen)";

		return raw.Replace((char)0x02, '⟨').Replace((char)0x03, '⟩');
	}

	[RelayCommand]
	async Task SendZplAsync()
	{
		if (await RequireProfileAsync() is not { } profile)
			return;
		if (string.IsNullOrWhiteSpace(ZplText))
		{
			await _alertService.ShowAsync("Kein Inhalt", "Bitte ZPL-Code einfügen (z.B. von der Versanddienstleister-API).", "OK");
			return;
		}

		IsSending = true;
		var result = await _printerService.SendZplAsync(profile, ZplText);
		IsSending = false;

		await ShowSendResultAsync(result, "ZPL wurde an den Drucker gesendet.");
	}

	[RelayCommand]
	async Task SendZplQueryAsync()
	{
		if (await RequireProfileAsync() is not { } profile)
			return;
		if (string.IsNullOrWhiteSpace(ZplText))
		{
			await _alertService.ShowAsync("Kein Inhalt", "Bitte einen Befehl eingeben, z.B. ~HS oder ! U1 getvar \"media.type\"", "OK");
			return;
		}

		IsSending = true;
		var result = await _printerService.QueryAsync(profile, ZplText);
		IsSending = false;

		ResponseText = result.Success ? FormatQueryResponse(result.ResponseText) : $"Fehler: {result.ErrorMessage}";
	}

	[RelayCommand]
	async Task QueryStatusAsync()
	{
		if (await RequireProfileAsync() is not { } profile)
			return;

		IsSending = true;
		var result = await _printerService.GetStatusAsync(profile);
		IsSending = false;

		ResponseText = result.Success ? FormatQueryResponse(result.ResponseText) : $"Fehler: {result.ErrorMessage}";
	}

	[RelayCommand]
	async Task PrintTestLabelAsync()
	{
		if (await RequireProfileAsync() is not { } profile)
			return;

		string label = LabelSamples.CreateTestLabelZpl(profile);

		IsSending = true;
		var result = await _printerService.SendZplAsync(profile, label);
		IsSending = false;

		await ShowSendResultAsync(result, "Testlabel wurde an den Drucker gesendet.");
	}

	Task ShowSendResultAsync(PrinterResult result, string successMessage) =>
		_alertService.ShowAsync(
			result.Success ? "Gesendet" : "Fehler",
			result.Success ? successMessage : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
}
