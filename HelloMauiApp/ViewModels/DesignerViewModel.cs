using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloMauiApp.Services;
using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp.ViewModels;

/// <summary>
/// ViewModel des Label-Designers (Rail-Ziel "designer"). Besitzt die komplette Vorlagen-,
/// Auswahl- und Editor-Logik inkl. Live-ZPL-Vorschau. Die View (DesignerPage) bleibt für das
/// Canvas-Zeichnen und die Gesten zuständig (Zoom/Pixel sind reine Darstellungsfragen) und wird
/// über die Events unten benachrichtigt, statt selbst Zustand zu halten.
/// </summary>
public partial class DesignerViewModel : ViewModelBase
{
	readonly ILabelTemplateStore _store;
	readonly IPrinterService _printerService;
	readonly IPrinterProfileStore _profileStore;
	readonly INavigationService _navigationService;
	readonly IAlertService _alertService;
	readonly IFileDialogService _fileDialogs;

	/// <summary>Editor-Felder, deren Änderung sofort in das ausgewählte Element geschrieben wird (siehe OnPropertyChanged).</summary>
	static readonly HashSet<string> EditorProperties =
	[
		nameof(XText), nameof(YText),
		nameof(TextValue), nameof(TextIsPlaceholder), nameof(TextPlaceholderKey), nameof(FontSizeText), nameof(TextRotationIndex),
		nameof(SymbologyIndex), nameof(BarcodeData), nameof(BarcodeIsPlaceholder), nameof(BarcodePlaceholderKey),
		nameof(BarcodeHeightText), nameof(BarcodeHumanReadable), nameof(BarcodeMagnificationText), nameof(QrErrorCorrectionIndex),
		nameof(ImageWidthText), nameof(ImageHeightText),
		nameof(FrameWidthText), nameof(FrameHeightText), nameof(FrameThicknessText), nameof(FrameFilled),
		nameof(EllipseWidthText), nameof(EllipseHeightText), nameof(EllipseThicknessText), nameof(EllipseFilled),
		nameof(LineLengthText), nameof(LineThicknessText), nameof(LineOrientationIndex),
	];

	LabelTemplate _template = null!;
	bool _loadingSelection;
	string _fullZpl = string.Empty;
	CancellationTokenSource? _zplRefreshCts;

	public LabelTemplate Template => _template;

	public LabelElement? SelectedElement { get; private set; }

	// ---------- Benachrichtigungen an die View (Canvas ist View-Sache) ----------

	/// <summary>Vorlage komplett neu zeichnen (neue/geladene Vorlage, geänderte Maße).</summary>
	public event Action? CanvasResetRequested;

	public event Action<LabelElement>? ElementAdded;

	public event Action<LabelElement>? ElementRemoved;

	/// <summary>Darstellung des ausgewählten Elements hat sich geändert (Inhalt und/oder Position).</summary>
	public event Action<LabelElement>? SelectedElementVisualChanged;

	public event Action<LabelElement?>? SelectionChanged;

	// ---------- Kopf/Status ----------

	[ObservableProperty] string title = string.Empty;

	// ---------- Auswahl / gemeinsame Felder ----------

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNoSelection))]
	bool hasSelection;

	public bool HasNoSelection => !HasSelection;

	[ObservableProperty] string selectedElementTitle = string.Empty;
	[ObservableProperty] string xText = string.Empty;
	[ObservableProperty] string yText = string.Empty;

	// ---------- Text-Element ----------

	[ObservableProperty] bool isTextSelected;
	[ObservableProperty] string textValue = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(TextLiteralVisible))]
	bool textIsPlaceholder;

	public bool TextLiteralVisible => !TextIsPlaceholder;

	[ObservableProperty] string? textPlaceholderKey;
	[ObservableProperty] string fontSizeText = string.Empty;
	[ObservableProperty] int textRotationIndex;

	// ---------- Barcode-Element ----------

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsBarcode1DVisible))]
	[NotifyPropertyChangedFor(nameof(IsBarcode2DVisible))]
	[NotifyPropertyChangedFor(nameof(IsQrVisible))]
	bool isBarcodeSelected;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsBarcode1DVisible))]
	[NotifyPropertyChangedFor(nameof(IsBarcode2DVisible))]
	[NotifyPropertyChangedFor(nameof(IsQrVisible))]
	int symbologyIndex;

	public bool IsBarcode1DVisible => IsBarcodeSelected && SymbologyIndex >= 0 && !LabelCanvasRenderer.IsSymbology2D((BarcodeSymbology)SymbologyIndex);
	public bool IsBarcode2DVisible => IsBarcodeSelected && SymbologyIndex >= 0 && LabelCanvasRenderer.IsSymbology2D((BarcodeSymbology)SymbologyIndex);
	public bool IsQrVisible => IsBarcodeSelected && SymbologyIndex == (int)BarcodeSymbology.QrCode;

	[ObservableProperty] string barcodeData = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(BarcodeLiteralVisible))]
	bool barcodeIsPlaceholder;

	public bool BarcodeLiteralVisible => !BarcodeIsPlaceholder;

	[ObservableProperty] string? barcodePlaceholderKey;
	[ObservableProperty] string barcodeHeightText = string.Empty;
	[ObservableProperty] bool barcodeHumanReadable;
	[ObservableProperty] string barcodeMagnificationText = string.Empty;
	[ObservableProperty] int qrErrorCorrectionIndex;

	// ---------- Bild-Element ----------

	[ObservableProperty] bool isImageSelected;
	[ObservableProperty] string imageWidthText = string.Empty;
	[ObservableProperty] string imageHeightText = string.Empty;

	// ---------- Rahmen-Element ----------

	[ObservableProperty] bool isFrameSelected;
	[ObservableProperty] string frameWidthText = string.Empty;
	[ObservableProperty] string frameHeightText = string.Empty;
	[ObservableProperty] string frameThicknessText = string.Empty;
	[ObservableProperty] bool frameFilled;

	// ---------- Ellipse-Element ----------

	[ObservableProperty] bool isEllipseSelected;
	[ObservableProperty] string ellipseWidthText = string.Empty;
	[ObservableProperty] string ellipseHeightText = string.Empty;
	[ObservableProperty] string ellipseThicknessText = string.Empty;
	[ObservableProperty] bool ellipseFilled;

	// ---------- Linien-Element ----------

	[ObservableProperty] bool isLineSelected;
	[ObservableProperty] string lineLengthText = string.Empty;
	[ObservableProperty] string lineThicknessText = string.Empty;
	[ObservableProperty] int lineOrientationIndex;

	// ---------- Platzhalter & ZPL-Vorschau ----------

	[ObservableProperty] List<string> placeholderKeys = [];
	[ObservableProperty] string zplPreview = string.Empty;

	public DesignerViewModel(
		ILabelTemplateStore store,
		IPrinterService printerService,
		IPrinterProfileStore profileStore,
		INavigationService navigationService,
		IAlertService alertService,
		IFileDialogService fileDialogs)
	{
		_store = store;
		_printerService = printerService;
		_profileStore = profileStore;
		_navigationService = navigationService;
		_alertService = alertService;
		_fileDialogs = fileDialogs;

		InitializeTemplate(null);
	}

	// ---------- Vorlage laden / Lebenszyklus ----------

	/// <summary>Lädt eine bestehende Vorlage in den (dauerhaften) Designer, oder – bei <c>null</c> – eine neue leere Vorlage.</summary>
	public void LoadTemplate(LabelTemplate? template)
	{
		InitializeTemplate(template);
		SelectElement(null);
		CanvasResetRequested?.Invoke();
	}

	void InitializeTemplate(LabelTemplate? template)
	{
		if (template is not null)
		{
			_template = template;
		}
		else
		{
			// Geometrie-Vorgabe aus dem aktiven Profil; ohne Profile greifen die Modell-Defaults (100×150, 203 dpi).
			var profile = _profileStore.GetDefault() ?? new PrinterProfile();
			_template = new LabelTemplate
			{
				Name = "Neue Vorlage",
				WidthMm = profile.LabelWidthMm,
				HeightMm = profile.LabelHeightMm,
				Dpi = profile.Dpi,
			};
		}

		UpdateTitle();
		RefreshPlaceholderKeys();
		RequestZplRefresh();
	}

	/// <summary>Beim Aktivieren des Rail-Ziels: Labelgröße/Platzhalter können sich in Drill-downs geändert haben.</summary>
	public Task OnActivatedAsync()
	{
		UpdateTitle();
		RefreshPlaceholderKeys();
		CanvasResetRequested?.Invoke();
		if (SelectedElement is not null)
			LoadEditorFromElement(SelectedElement);
		RequestZplRefresh();
		return Task.CompletedTask;
	}

	void UpdateTitle() =>
		Title = $"{_template.Name} · {_template.WidthMm:0.#} × {_template.HeightMm:0.#} mm · {_template.Dpi} dpi";

	void RefreshPlaceholderKeys() =>
		PlaceholderKeys = _template.Placeholders.Select(p => p.Key).ToList();

	// ---------- Auswahl ----------

	/// <summary>Wird von der View bei Tap/Drag-Start aufgerufen; <c>null</c> hebt die Auswahl auf.</summary>
	public void SelectElement(LabelElement? element)
	{
		SelectedElement = element;
		HasSelection = element is not null;

		if (element is not null)
			LoadEditorFromElement(element);

		SelectionChanged?.Invoke(element);
	}

	/// <summary>Wird von der View beim Ziehen aufgerufen (Werte bereits in mm, gerastert/geklemmt).</summary>
	public void MoveElement(LabelElement element, double xMm, double yMm)
	{
		element.X = xMm;
		element.Y = yMm;

		if (ReferenceEquals(element, SelectedElement))
		{
			_loadingSelection = true;
			XText = xMm.ToString("0.#");
			YText = yMm.ToString("0.#");
			_loadingSelection = false;
		}

		RequestZplRefresh();
	}

	void LoadEditorFromElement(LabelElement element)
	{
		_loadingSelection = true;

		XText = element.X.ToString("0.#");
		YText = element.Y.ToString("0.#");

		IsTextSelected = element is TextElement;
		IsBarcodeSelected = element is BarcodeElement;
		IsImageSelected = element is ImageElement;
		IsFrameSelected = element is FrameElement;
		IsEllipseSelected = element is EllipseElement;
		IsLineSelected = element is LineElement;

		switch (element)
		{
			case TextElement text:
				SelectedElementTitle = "Text";
				TextValue = text.Text.LiteralValue;
				TextIsPlaceholder = text.Text.IsPlaceholder;
				TextPlaceholderKey = PlaceholderKeys.Contains(text.Text.PlaceholderKey) ? text.Text.PlaceholderKey : null;
				FontSizeText = text.FontSizeMm.ToString("0.#");
				TextRotationIndex = (int)text.Rotation;
				break;

			case BarcodeElement barcode:
				SelectedElementTitle = "Barcode";
				SymbologyIndex = (int)barcode.Symbology;
				BarcodeData = barcode.Data.LiteralValue;
				BarcodeIsPlaceholder = barcode.Data.IsPlaceholder;
				BarcodePlaceholderKey = PlaceholderKeys.Contains(barcode.Data.PlaceholderKey) ? barcode.Data.PlaceholderKey : null;
				BarcodeHeightText = barcode.HeightMm.ToString("0.#");
				BarcodeHumanReadable = barcode.PrintHumanReadable;
				BarcodeMagnificationText = barcode.Magnification.ToString();
				QrErrorCorrectionIndex = (int)barcode.QrErrorCorrection;
				break;

			case ImageElement image:
				SelectedElementTitle = "Bild";
				ImageWidthText = image.WidthMm.ToString("0.#");
				ImageHeightText = image.HeightMm.ToString("0.#");
				break;

			case FrameElement frame:
				SelectedElementTitle = "Rahmen";
				FrameWidthText = frame.WidthMm.ToString("0.#");
				FrameHeightText = frame.HeightMm.ToString("0.#");
				FrameThicknessText = frame.ThicknessMm.ToString("0.#");
				FrameFilled = frame.Filled;
				break;

			case EllipseElement ellipse:
				SelectedElementTitle = "Ellipse";
				EllipseWidthText = ellipse.WidthMm.ToString("0.#");
				EllipseHeightText = ellipse.HeightMm.ToString("0.#");
				EllipseThicknessText = ellipse.ThicknessMm.ToString("0.#");
				EllipseFilled = ellipse.Filled;
				break;

			case LineElement line:
				SelectedElementTitle = "Linie";
				LineLengthText = line.LengthMm.ToString("0.#");
				LineThicknessText = line.ThicknessMm.ToString("0.#");
				LineOrientationIndex = line.Orientation == LineOrientation.Horizontal ? 0 : 1;
				break;
		}

		_loadingSelection = false;
	}

	/// <summary>
	/// Schreibt Editor-Änderungen sofort ins Modell zurück (ein Handler für alle Felder statt
	/// dutzender einzelner Changed-Handler – gefiltert über <see cref="EditorProperties"/>).
	/// </summary>
	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);

		if (_loadingSelection || SelectedElement is null || e.PropertyName is null || !EditorProperties.Contains(e.PropertyName))
			return;

		ApplyEditorToElement(SelectedElement);
		SelectedElementVisualChanged?.Invoke(SelectedElement);
		RequestZplRefresh();
	}

	void ApplyEditorToElement(LabelElement element)
	{
		if (double.TryParse(XText, out double x))
			element.X = x;
		if (double.TryParse(YText, out double y))
			element.Y = y;

		switch (element)
		{
			case TextElement text:
				text.Text = TextIsPlaceholder
					? BindableValue.Placeholder(TextPlaceholderKey ?? string.Empty)
					: BindableValue.Literal(TextValue ?? string.Empty);
				if (double.TryParse(FontSizeText, out double fontSize))
					text.FontSizeMm = fontSize;
				if (TextRotationIndex >= 0)
					text.Rotation = (TextRotation)TextRotationIndex;
				break;

			case BarcodeElement barcode:
				if (SymbologyIndex >= 0)
					barcode.Symbology = (BarcodeSymbology)SymbologyIndex;
				barcode.Data = BarcodeIsPlaceholder
					? BindableValue.Placeholder(BarcodePlaceholderKey ?? string.Empty)
					: BindableValue.Literal(BarcodeData ?? string.Empty);
				if (double.TryParse(BarcodeHeightText, out double barcodeHeight))
					barcode.HeightMm = barcodeHeight;
				barcode.PrintHumanReadable = BarcodeHumanReadable;
				if (int.TryParse(BarcodeMagnificationText, out int magnification))
					barcode.Magnification = magnification;
				if (QrErrorCorrectionIndex >= 0)
					barcode.QrErrorCorrection = (QrErrorCorrection)QrErrorCorrectionIndex;
				break;

			case ImageElement image:
				if (double.TryParse(ImageWidthText, out double imageWidth))
					image.WidthMm = imageWidth;
				if (double.TryParse(ImageHeightText, out double imageHeight))
					image.HeightMm = imageHeight;
				break;

			case FrameElement frame:
				if (double.TryParse(FrameWidthText, out double frameWidth))
					frame.WidthMm = frameWidth;
				if (double.TryParse(FrameHeightText, out double frameHeight))
					frame.HeightMm = frameHeight;
				if (double.TryParse(FrameThicknessText, out double frameThickness))
					frame.ThicknessMm = frameThickness;
				frame.Filled = FrameFilled;
				break;

			case EllipseElement ellipse:
				if (double.TryParse(EllipseWidthText, out double ellipseWidth))
					ellipse.WidthMm = ellipseWidth;
				if (double.TryParse(EllipseHeightText, out double ellipseHeight))
					ellipse.HeightMm = ellipseHeight;
				if (double.TryParse(EllipseThicknessText, out double ellipseThickness))
					ellipse.ThicknessMm = ellipseThickness;
				ellipse.Filled = EllipseFilled;
				break;

			case LineElement line:
				if (double.TryParse(LineLengthText, out double lineLength))
					line.LengthMm = lineLength;
				if (double.TryParse(LineThicknessText, out double lineThickness))
					line.ThicknessMm = lineThickness;
				line.Orientation = LineOrientationIndex == 1 ? LineOrientation.Vertical : LineOrientation.Horizontal;
				break;
		}
	}

	// ---------- Elemente hinzufügen/löschen ----------

	void AddElement(LabelElement element)
	{
		_template.Elements.Add(element);
		ElementAdded?.Invoke(element);
		SelectElement(element);
		RequestZplRefresh();
	}

	[RelayCommand] void AddText() => AddElement(new TextElement { X = 10, Y = 10 });
	[RelayCommand] void AddBarcode() => AddElement(new BarcodeElement { X = 10, Y = 40 });
	[RelayCommand] void AddFrame() => AddElement(new FrameElement { X = 10, Y = 70 });
	[RelayCommand] void AddEllipse() => AddElement(new EllipseElement { X = 45, Y = 70 });
	[RelayCommand] void AddLine() => AddElement(new LineElement { X = 10, Y = 100 });

	[RelayCommand]
	async Task AddImageAsync()
	{
		PickedFile? file;
		try
		{
			file = await _fileDialogs.PickImageAsync("Bild auswählen");
		}
		catch (Exception ex)
		{
			await _alertService.ShowAsync("Fehler", $"Bild konnte nicht ausgewählt werden: {ex.Message}", "OK");
			return;
		}

		if (file is null)
			return;

		AddElement(new ImageElement { X = 10, Y = 10, ImageBase64 = Convert.ToBase64String(file.Bytes) });
	}

	[RelayCommand]
	void DeleteElement()
	{
		if (SelectedElement is null)
			return;

		var element = SelectedElement;
		_template.Elements.Remove(element);
		SelectElement(null);
		ElementRemoved?.Invoke(element);
		RequestZplRefresh();
	}

	// ---------- Drill-downs ----------

	[RelayCommand] Task ManagePlaceholdersAsync() => _navigationService.PushAsync(new PlaceholderManagerPage(_template));
	[RelayCommand] Task ManageMediaAsync() => _navigationService.PushAsync(new MediaManagerPage(_template));
	[RelayCommand] Task EditPropertiesAsync() => _navigationService.PushAsync(new TemplatePropertiesPage(_template));

	// ---------- Neu / Speichern / Laden / Export / Import / Drucken ----------

	[RelayCommand]
	async Task NewAsync()
	{
		bool confirmed = await _alertService.ConfirmAsync("Neue Vorlage", "Aktuelles Design verwerfen und neu beginnen?", "Ja", "Abbrechen");
		if (!confirmed)
			return;

		LoadTemplate(null);
	}

	[RelayCommand]
	async Task SaveAsync()
	{
		string? name = await _alertService.PromptAsync("Vorlage speichern", "Name der Vorlage:", _template.Name);
		if (string.IsNullOrWhiteSpace(name))
			return;

		_template.Name = name.Trim();
		await _store.SaveAsync(_template);
		UpdateTitle();
		await _alertService.ShowAsync("Gespeichert", $"Vorlage \"{_template.Name}\" wurde gespeichert.", "OK");
	}

	[RelayCommand]
	async Task LoadAsync()
	{
		var names = await _store.ListTemplateNamesAsync();
		if (names.Count == 0)
		{
			await _alertService.ShowAsync("Keine Vorlagen", "Es sind noch keine Vorlagen gespeichert.", "OK");
			return;
		}

		string choice = await _alertService.ActionSheetAsync("Vorlage laden", "Abbrechen", null, names.ToArray());
		if (string.IsNullOrEmpty(choice) || choice == "Abbrechen")
			return;

		var loaded = await _store.LoadAsync(choice);
		if (loaded is null)
		{
			await _alertService.ShowAsync("Fehler", "Vorlage konnte nicht geladen werden.", "OK");
			return;
		}

		LoadTemplate(loaded);
	}

	[RelayCommand]
	async Task ExportAsync()
	{
		const string saveAsOption = "Speichern unter...";
		const string shareOption = "Teilen...";

		string choice = await _alertService.ActionSheetAsync("Vorlage exportieren", "Abbrechen", null, saveAsOption, shareOption);
		if (string.IsNullOrEmpty(choice) || choice == "Abbrechen")
			return;

		try
		{
			string json = System.Text.Json.JsonSerializer.Serialize(_template, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
			string fileName = $"{LabelTemplateStore.SanitizeFileName(_template.Name)}.json";

			if (choice == saveAsOption)
			{
				string? path = await _fileDialogs.SaveTextFileAsAsync(fileName, "JSON-Vorlage", ".json", json);
				if (path is not null)
					await _alertService.ShowAsync("Gespeichert", $"Vorlage wurde unter \"{path}\" gespeichert.", "OK");
			}
			else
			{
				await _fileDialogs.ShareTextFileAsync("Vorlage exportieren", fileName, json);
			}
		}
		catch (Exception ex)
		{
			await _alertService.ShowAsync("Fehler", $"Export fehlgeschlagen: {ex.Message}", "OK");
		}
	}

	[RelayCommand]
	async Task ImportAsync()
	{
		try
		{
			var file = await _fileDialogs.PickJsonAsync("Vorlage importieren");
			if (file is null)
				return;

			string json = Encoding.UTF8.GetString(file.Bytes);
			var imported = System.Text.Json.JsonSerializer.Deserialize<LabelTemplate>(json);
			if (imported is null)
			{
				await _alertService.ShowAsync("Fehler", "Datei enthält keine gültige Vorlage.", "OK");
				return;
			}

			LoadTemplate(imported);
			await _alertService.ShowAsync("Importiert", $"Vorlage \"{_template.Name}\" wurde importiert.", "OK");
		}
		catch (Exception ex)
		{
			await _alertService.ShowAsync("Fehler", $"Import fehlgeschlagen: {ex.Message}", "OK");
		}
	}

	[RelayCommand]
	async Task PrintAsync()
	{
		if (_profileStore.GetDefault() is not { } profile)
		{
			await _alertService.ShowAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ ein Druckerprofil anlegen.", "OK");
			return;
		}

		if (_template.Elements.Count == 0)
		{
			await _alertService.ShowAsync("Leeres Label", "Bitte zuerst mindestens ein Element hinzufügen.", "OK");
			return;
		}

		if (_template.Placeholders.Count > 0)
		{
			bool goToTest = await _alertService.ConfirmAsync(
				"Vorlage mit Platzhaltern",
				"Diese Vorlage enthält Platzhalter. Zum Befüllen und Drucken bitte den Test-Modus verwenden.",
				"Zum Test-Modus",
				"Abbrechen");

			if (goToTest)
				await _navigationService.PushAsync(new TemplateTestPage(_template));

			return;
		}

		IsBusy = true;
		string zpl = LabelTemplateRenderer.ToZpl(_template);
		var result = await _printerService.SendZplAsync(profile, zpl);
		IsBusy = false;

		await _alertService.ShowAsync(
			result.Success ? "Gesendet" : "Fehler",
			result.Success ? "Label wurde an den Drucker gesendet." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}

	// ---------- Live-ZPL-Vorschau ----------

	[RelayCommand]
	async Task CopyZplAsync()
	{
		if (_fullZpl.Length == 0)
			return;

		await Clipboard.Default.SetTextAsync(_fullZpl);
		await _alertService.ShowAsync("Kopiert", "Der vollständige ZPL-Code liegt in der Zwischenablage.", "OK");
	}

	/// <summary>
	/// Erzeugt die ZPL-Vorschau entkoppelt (250 ms Debounce, Hintergrund-Thread), damit der Editor
	/// beim Tippen/Ziehen flüssig bleibt – die Bildkonvertierung (^GF) kann teuer sein.
	/// </summary>
	void RequestZplRefresh()
	{
		_zplRefreshCts?.Cancel();
		var cts = _zplRefreshCts = new CancellationTokenSource();

		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(250, cts.Token);
				string zpl = LabelTemplateRenderer.ToZpl(_template);
				if (cts.Token.IsCancellationRequested)
					return;

				string display = FormatZplForDisplay(zpl);
				MainThread.BeginInvokeOnMainThread(() =>
				{
					_fullZpl = zpl;
					ZplPreview = display;
				});
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				// Kann bei halb editierten Werten (z.B. defektem Base64) passieren – Vorschau zeigt den Grund.
				MainThread.BeginInvokeOnMainThread(() => ZplPreview = $"ZPL-Vorschau nicht möglich: {ex.Message}");
			}
		});
	}

	/// <summary>
	/// Bricht den ZPL-Einzeiler für die Anzeige um (ein Feld pro Zeile) und kürzt eingebettete
	/// Bilddaten (^GF-Hexblöcke), die sonst die Vorschau fluten würden. "ZPL kopieren" liefert
	/// immer den vollständigen Code.
	/// </summary>
	internal static string FormatZplForDisplay(string zpl)
	{
		string display = zpl.Replace("^FO", "\n^FO").Replace("^XZ", "\n^XZ");

		display = Regex.Replace(display, @"(\^GF[AB],\d+,\d+,\d+,)(\S{40,})", m =>
			$"{m.Groups[1].Value}… ({m.Groups[2].Value.Length:N0} Zeichen Bilddaten)");

		return display.TrimStart('\n');
	}
}
