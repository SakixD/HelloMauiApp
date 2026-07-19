using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloMauiApp.Services;
using HelloMauiApp.Services.Api;
using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp.ViewModels;

/// <summary>Startseiten-ViewModel (Rail-Ziel "Start"): Druckerauswahl/-status, Bibliotheks-Statistik, Schnellzugriff.</summary>
public partial class MainPageViewModel : ViewModelBase
{
	readonly IPrinterService _printerService;
	readonly IPrinterProfileStore _profileStore;
	readonly ILabelTemplateStore _templateStore;
	readonly IPrintMediaStore _mediaStore;
	readonly IAlertService _alertService;
	readonly LocalApiServer _apiServer;

	/// <summary>Von <see cref="AppShell"/> nach dem Auflösen aus DI gesetzt (Rail-Navigation ist Shell-Orchestrierung, kein Konstruktor-Abhängigkeit).</summary>
	public Action<string>? NavigateToSection { get; set; }

	/// <summary>Alle Profile für den Drucker-Picker; die Auswahl macht das Profil zum app-weiten Default.</summary>
	public ObservableCollection<PrinterProfile> Profiles { get; } = [];

	[ObservableProperty] PrinterProfile? selectedProfile;
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

	/// <summary>Während RefreshAsync die Auswahl setzt, darf OnSelectedProfileChanged nicht zurück in den Store schreiben.</summary>
	bool _suppressProfileSelection;

	public MainPageViewModel(
		IPrinterService printerService,
		IPrinterProfileStore profileStore,
		ILabelTemplateStore templateStore,
		IPrintMediaStore mediaStore,
		IAlertService alertService,
		LocalApiServer apiServer)
	{
		_printerService = printerService;
		_profileStore = profileStore;
		_templateStore = templateStore;
		_mediaStore = mediaStore;
		_alertService = alertService;
		_apiServer = apiServer;
	}

	public async Task RefreshAsync()
	{
		var profiles = _profileStore.GetAll();

		_suppressProfileSelection = true;
		Profiles.Clear();
		foreach (var profile in profiles)
			Profiles.Add(profile);
		SelectedProfile = Profiles.FirstOrDefault(p => p.IsDefault) ?? Profiles.FirstOrDefault();
		_suppressProfileSelection = false;

		UpdatePrinterCard();

		ApiValue = _apiServer.IsRunning
			? $"{_apiServer.BaseUrl}api"
			: $"Nicht aktiv ({_apiServer.LastError ?? "unbekannter Grund"})";

		if (SelectedProfile is null)
		{
			StatusText = "Kein Drucker konfiguriert";
			StatusBrush = new SolidColorBrush((Color)Application.Current!.Resources["ColorText3"]);
		}

		var templateNames = await _templateStore.ListTemplateNamesAsync();
		var mediaList = await _mediaStore.ListAsync();

		// Bewusste Entscheidung (CLEAN-06): Für die Platzhalterzahl wird jede Vorlage voll geladen
		// (inkl. eingebetteter Bilder) — linearer Datei-I/O pro Dashboard-Aktivierung. Bei der
		// erwarteten Vorlagenzahl unkritisch; ein Cache müsste Änderungen aus Designer, API und
		// Dateisystem invalidieren und stünde in keinem Verhältnis zum Nutzen. Erst optimieren,
		// wenn Vorlagenlisten real groß werden (dann: Zählung im Store cachen).
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

	partial void OnSelectedProfileChanged(PrinterProfile? value)
	{
		if (_suppressProfileSelection || value is null)
			return;

		// Die Picker-Auswahl IST die app-weite Druckerwahl: als Default persistieren, damit
		// alle anderen Seiten und die lokale API denselben aktiven Drucker sehen.
		_profileStore.SetDefault(value.Id);
		UpdatePrinterCard();
	}

	void UpdatePrinterCard()
	{
		if (SelectedProfile is not { } profile)
		{
			IpValue = "Nicht konfiguriert";
			DpiValue = string.Empty;
			MediaValue = string.Empty;
			return;
		}

		IpValue = profile.ConnectionSummary;
		DpiValue = $"{profile.Dpi} dpi · {profile.Dpi / 25.4:0.#} Dots/mm";
		MediaValue = $"{profile.LabelWidthMm:0.#} × {profile.LabelHeightMm:0.#} mm";
	}

	/// <summary>Liefert das aktive Profil oder zeigt den einheitlichen "kein Drucker"-Hinweis.</summary>
	async Task<PrinterProfile?> RequireProfileAsync()
	{
		if (SelectedProfile is { } profile)
			return profile;

		// Fallback auf den Store: Der Picker kann (z.B. direkt nach einem ItemsSource-Wechsel auf
		// Windows) kurzzeitig keine Auswahl haben, obwohl ein Default-Profil existiert.
		if (_profileStore.GetDefault() is { } fallback)
			return fallback;

		await _alertService.ShowAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ ein Druckerprofil anlegen.", "OK");
		return null;
	}

	[RelayCommand]
	async Task TestConnectionAsync()
	{
		if (await RequireProfileAsync() is not { } profile)
			return;

		IsBusy = true;
		var result = await _printerService.TestConnectionAsync(profile);
		IsBusy = false;

		StatusText = result.Success ? "Verbunden" : "Nicht erreichbar";
		StatusBrush = new SolidColorBrush((Color)Application.Current!.Resources[result.Success ? "ColorSuccess" : "ColorDanger"]);

		await _alertService.ShowAsync(
			result.Success ? "Verbindung OK" : "Verbindung fehlgeschlagen",
			result.Success ? $"Drucker „{profile.Name}“ ({profile.ConnectionSummary}) ist erreichbar." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}

	[RelayCommand]
	async Task CalibrateAsync()
	{
		if (await RequireProfileAsync() is not { } profile)
			return;

		bool confirmed = await _alertService.ConfirmAsync(
			"Medium kalibrieren",
			"Der Drucker zieht dabei mehrere Etiketten durch, um Etikettenlänge sowie Lücken-/Schwarzmarkenposition automatisch zu erkennen. Fortfahren?",
			"Ja, kalibrieren",
			"Abbrechen");
		if (!confirmed)
			return;

		IsBusy = true;
		var result = await _printerService.CalibrateMediaAsync(profile);
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
		if (await RequireProfileAsync() is not { } profile)
			return;

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

			string label = LabelSamples.CreateImageLabelZpl(profile, ms.ToArray());
			var result = await _printerService.SendZplAsync(profile, label);

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
