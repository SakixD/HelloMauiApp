using LabelPrinting.Services;

namespace HelloMauiApp;

/// <summary>
/// Startseite ("Start" in der Rail) nach dem importierten Design. Läuft als <see cref="ContentView"/>
/// im Inhaltsbereich von <see cref="AppShell"/> statt als eigene, gepushte Seite – Navigation zu den
/// anderen Bereichen läuft daher über den <paramref name="navigate"/>-Rückruf statt über eigene
/// <c>Navigation.PushAsync</c>-Aufrufe (die einer <see cref="ContentView"/> nicht zur Verfügung stehen).
/// </summary>
public partial class MainPage : ContentView
{
	readonly IPrinterService _printerService = new ZplPrinterService();
	readonly PrinterSettingsStore _settingsStore = new();
	readonly LabelTemplateStore _templateStore = new();
	readonly PrintMediaStore _mediaStore = new();
	readonly ContentPage _host;
	readonly Action<string> _navigate;

	public MainPage(ContentPage host, Action<string> navigate)
	{
		InitializeComponent();
		_host = host;
		_navigate = navigate;
	}

	public async Task RefreshAsync()
	{
		var settings = _settingsStore.Load();
		bool configured = !string.IsNullOrWhiteSpace(settings.IpAddress);

		IpValueLabel.Text = configured ? $"{settings.IpAddress}:{settings.Port}" : "Nicht konfiguriert";
		DpiValueLabel.Text = $"{settings.Dpi} dpi · {settings.Dpi / 25.4:0.#} Dots/mm";
		MediaValueLabel.Text = $"{settings.LabelWidthMm:0.#} × {settings.LabelHeightMm:0.#} mm";

		if (!configured)
		{
			StatusLabel.Text = "Kein Drucker konfiguriert";
			StatusDot.Fill = new SolidColorBrush((Color)Application.Current!.Resources["ColorText3"]);
		}

		var templateNames = await _templateStore.ListTemplateNamesAsync();
		var mediaList = await _mediaStore.ListAsync();

		int placeholderCount = 0;
		foreach (var name in templateNames)
		{
			var template = await _templateStore.LoadAsync(name);
			if (template is not null)
				placeholderCount += template.Placeholders.Count;
		}

		TemplateCountLabel.Text = templateNames.Count.ToString();
		MediaCountLabel.Text = mediaList.Count.ToString();
		PlaceholderCountLabel.Text = placeholderCount.ToString();
	}

	void SetBusy(bool busy)
	{
		BusyIndicator.IsVisible = busy;
		BusyIndicator.IsRunning = busy;
		TestConnectionBtn.IsEnabled = !busy;
		CalibrateBtn.IsEnabled = !busy;
	}

	bool RequirePrinterConfigured(LabelPrinting.Models.PrinterSettings settings)
		=> !string.IsNullOrWhiteSpace(settings.IpAddress);

	void OnOpenDesignerClicked(object? sender, EventArgs e) => _navigate("designer");

	void OnOpenZplConsoleClicked(object? sender, EventArgs e) => _navigate("zpl");

	void OnOpenTemplatesClicked(object? sender, EventArgs e) => _navigate("templates");

	void OnDesignerTileTapped(object? sender, TappedEventArgs e) => _navigate("designer");

	void OnTemplateTestTileTapped(object? sender, TappedEventArgs e) => _navigate("templatetest");

	void OnMediaTileTapped(object? sender, TappedEventArgs e) => _navigate("media");

	async void OnTestConnectionClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await _host.DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		SetBusy(true);
		var result = await _printerService.TestConnectionAsync(settings.IpAddress, settings.Port);
		SetBusy(false);

		StatusLabel.Text = result.Success ? "Verbunden" : "Nicht erreichbar";
		StatusDot.Fill = new SolidColorBrush((Color)Application.Current!.Resources[result.Success ? "ColorSuccess" : "ColorDanger"]);

		await _host.DisplayAlertAsync(
			result.Success ? "Verbindung OK" : "Verbindung fehlgeschlagen",
			result.Success ? $"Drucker unter {settings.IpAddress}:{settings.Port} ist erreichbar." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}

	async void OnCalibrateClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await _host.DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		bool confirmed = await _host.DisplayAlertAsync(
			"Medium kalibrieren",
			"Der Drucker zieht dabei mehrere Etiketten durch, um Etikettenlänge sowie Lücken-/Schwarzmarkenposition automatisch zu erkennen. Fortfahren?",
			"Ja, kalibrieren",
			"Abbrechen");
		if (!confirmed)
			return;

		SetBusy(true);
		var result = await _printerService.CalibrateMediaAsync(settings.IpAddress, settings.Port);
		SetBusy(false);

		if (result.Success)
			CalibratedValueLabel.Text = DateTime.Now.ToString("HH:mm");

		await _host.DisplayAlertAsync(
			result.Success ? "Gesendet" : "Fehler",
			result.Success ? "Kalibrierbefehl wurde gesendet. Der Drucker sollte jetzt Etiketten durchziehen." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}

	async void OnPickImageTileTapped(object? sender, TappedEventArgs e)
	{
		var settings = _settingsStore.Load();
		if (!RequirePrinterConfigured(settings))
		{
			await _host.DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ die IP-Adresse eintragen.", "OK");
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
			await _host.DisplayAlertAsync("Fehler", $"Bild konnte nicht ausgewählt werden: {ex.Message}", "OK");
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

			await _host.DisplayAlertAsync(
				result.Success ? "Gesendet" : "Fehler",
				result.Success ? "Bild wurde als Label an den Drucker gesendet." : result.ErrorMessage ?? "Unbekannter Fehler",
				"OK");
		}
		catch (Exception ex)
		{
			await _host.DisplayAlertAsync("Fehler", $"Bild konnte nicht gedruckt werden: {ex.Message}", "OK");
		}
		finally
		{
			SetBusy(false);
		}
	}
}
