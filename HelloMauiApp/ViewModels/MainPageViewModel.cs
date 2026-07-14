using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloMauiApp.Services;
using HelloMauiApp.Services.Api;
using LabelPrinting.Services;

namespace HelloMauiApp.ViewModels;

/// <summary>ViewModel der Startseite (Rail-Ziel "Start"): Druckerstatus, Bibliotheks-Statistik, Schnellzugriff.</summary>
public partial class MainPageViewModel : ViewModelBase
{
	readonly IPrinterService _printerService;
	readonly IPrinterSettingsStore _settingsStore;
	readonly ILabelTemplateStore _templateStore;
	readonly IPrintMediaStore _mediaStore;
	readonly IAlertService _alertService;
	readonly LocalApiServer _apiServer;

	/// <summary>Von <see cref="AppShell"/> nach dem Auflösen aus DI gesetzt (Rail-Navigation ist Shell-Orchestrierung, kein Konstruktor-Abhängigkeit).</summary>
	public Action<string>? NavigateToSection { get; set; }

	[ObservableProperty] string ipValue = "Nicht konfiguriert";
	[ObservableProperty] string dpiValue = string.Empty;
	[ObservableProperty] string mediaValue = string.Empty;
	[ObservableProperty] string calibratedValue = "—";
	[ObservableProperty] string statusText = "Nicht getestet";
	[ObservableProperty] Brush statusBrush = new SolidColorBrush(Colors.Gray);
	[ObservableProperty] string templateCount = "0";
	[ObservableProperty] string mediaCount = "0";
	[ObservableProperty] string placeholderCount = "0";
	[ObservableProperty] string apiValue = "—";

	public MainPageViewModel(
		IPrinterService printerService,
		IPrinterSettingsStore settingsStore,
		ILabelTemplateStore templateStore,
		IPrintMediaStore mediaStore,
		IAlertService alertService,
		LocalApiServer apiServer)
	{
		_printerService = printerService;
		_settingsStore = settingsStore;
		_templateStore = templateStore;
		_mediaStore = mediaStore;
		_alertService = alertService;
		_apiServer = apiServer;
	}

	public async Task RefreshAsync()
	{
		var settings = _settingsStore.Load();
		bool configured = !string.IsNullOrWhiteSpace(settings.IpAddress);

		IpValue = configured ? $"{settings.IpAddress}:{settings.Port}" : "Nicht konfiguriert";
		DpiValue = $"{settings.Dpi} dpi · {settings.Dpi / 25.4:0.#} Dots/mm";
		MediaValue = $"{settings.LabelWidthMm:0.#} × {settings.LabelHeightMm:0.#} mm";
		ApiValue = _apiServer.IsRunning
			? $"{_apiServer.BaseUrl}api"
			: $"Nicht aktiv ({_apiServer.LastError ?? "unbekannter Grund"})";

		if (!configured)
		{
			StatusText = "Kein Drucker konfiguriert";
			StatusBrush = new SolidColorBrush((Color)Application.Current!.Resources["ColorText3"]);
		}

		var templateNames = await _templateStore.ListTemplateNamesAsync();
		var mediaList = await _mediaStore.ListAsync();

		int placeholders = 0;
		foreach (var name in templateNames)
		{
			var template = await _templateStore.LoadAsync(name);
			if (template is not null)
				placeholders += template.Placeholders.Count;
		}

		TemplateCount = templateNames.Count.ToString();
		MediaCount = mediaList.Count.ToString();
		PlaceholderCount = placeholders.ToString();
	}

	bool RequirePrinterConfigured(LabelPrinting.Models.PrinterSettings settings) => !string.IsNullOrWhiteSpace(settings.IpAddress);

	[RelayCommand]
	async Task TestConnectionAsync()
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await _alertService.ShowAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		IsBusy = true;
		var result = await _printerService.TestConnectionAsync(settings.IpAddress, settings.Port);
		IsBusy = false;

		StatusText = result.Success ? "Verbunden" : "Nicht erreichbar";
		StatusBrush = new SolidColorBrush((Color)Application.Current!.Resources[result.Success ? "ColorSuccess" : "ColorDanger"]);

		await _alertService.ShowAsync(
			result.Success ? "Verbindung OK" : "Verbindung fehlgeschlagen",
			result.Success ? $"Drucker unter {settings.IpAddress}:{settings.Port} ist erreichbar." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}

	[RelayCommand]
	async Task CalibrateAsync()
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await _alertService.ShowAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		bool confirmed = await _alertService.ConfirmAsync(
			"Medium kalibrieren",
			"Der Drucker zieht dabei mehrere Etiketten durch, um Etikettenlänge sowie Lücken-/Schwarzmarkenposition automatisch zu erkennen. Fortfahren?",
			"Ja, kalibrieren",
			"Abbrechen");
		if (!confirmed)
			return;

		IsBusy = true;
		var result = await _printerService.CalibrateMediaAsync(settings.IpAddress, settings.Port);
		IsBusy = false;

		if (result.Success)
			CalibratedValue = DateTime.Now.ToString("HH:mm");

		await _alertService.ShowAsync(
			result.Success ? "Gesendet" : "Fehler",
			result.Success ? "Kalibrierbefehl wurde gesendet. Der Drucker sollte jetzt Etiketten durchziehen." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}

	[RelayCommand]
	async Task PickImageAsync()
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await _alertService.ShowAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ die IP-Adresse eintragen.", "OK");
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
			await _alertService.ShowAsync("Fehler", $"Bild konnte nicht ausgewählt werden: {ex.Message}", "OK");
			return;
		}

		if (file is null)
			return;

		IsBusy = true;
		try
		{
			using var stream = await file.OpenReadAsync();
			using var ms = new MemoryStream();
			await stream.CopyToAsync(ms);

			string label = LabelSamples.CreateImageLabelZpl(settings, ms.ToArray());
			var result = await _printerService.SendZplAsync(settings.IpAddress, settings.Port, label);

			await _alertService.ShowAsync(
				result.Success ? "Gesendet" : "Fehler",
				result.Success ? "Bild wurde als Label an den Drucker gesendet." : result.ErrorMessage ?? "Unbekannter Fehler",
				"OK");
		}
		catch (Exception ex)
		{
			await _alertService.ShowAsync("Fehler", $"Bild konnte nicht gedruckt werden: {ex.Message}", "OK");
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand] void OpenDesigner() => NavigateToSection?.Invoke("designer");
	[RelayCommand] void OpenZplConsole() => NavigateToSection?.Invoke("zpl");
	[RelayCommand] void OpenTemplates() => NavigateToSection?.Invoke("templates");
	[RelayCommand] void OpenTemplateTest() => NavigateToSection?.Invoke("templatetest");
	[RelayCommand] void OpenMedia() => NavigateToSection?.Invoke("media");
}
