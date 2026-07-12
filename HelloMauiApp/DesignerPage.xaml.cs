using LabelPrinting.Models;
using LabelPrinting.Services;
using Microsoft.Maui.Layouts;

namespace HelloMauiApp;

public partial class DesignerPage : ContentPage
{
	const double PixelsPerMm = LabelCanvasRenderer.PixelsPerMm;

	readonly LabelTemplateStore _store = new();
	readonly IPrinterService _printerService = new ZplPrinterService();
	readonly PrinterSettingsStore _settingsStore = new();

	LabelTemplate _template;
	LabelElement? _selectedElement;
	Border? _selectedBorder;
	bool _suppressPropertyChanged;

	public DesignerPage()
	{
		InitializeComponent();

		var settings = _settingsStore.Load();
		_template = new LabelTemplate
		{
			Name = "Neue Vorlage",
			WidthMm = settings.LabelWidthMm,
			HeightMm = settings.LabelHeightMm,
			Dpi = settings.Dpi,
		};

		RenderCanvas();
	}

	public DesignerPage(LabelTemplate template)
	{
		InitializeComponent();
		_template = template;
		RenderCanvas();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		// Nach Rückkehr von "Medium" kann sich die Labelgröße geändert haben.
		UpdateCanvasSize();

		// Nach Rückkehr von "Platzhalter verwalten" könnten sich Keys geändert haben.
		if (_selectedElement is not null && _selectedBorder is not null)
			ShowPropertiesFor(_selectedElement);
	}

	// ---------- Canvas-Aufbau ----------

	void UpdateCanvasSize()
	{
		CanvasLayout.WidthRequest = Math.Max(10, _template.WidthMm * PixelsPerMm);
		CanvasLayout.HeightRequest = Math.Max(10, _template.HeightMm * PixelsPerMm);
		TitleLabel.Text = $"Label-Designer – {_template.Name} ({_template.WidthMm:0.#}×{_template.HeightMm:0.#} mm)";
	}

	void RenderCanvas()
	{
		CanvasLayout.Children.Clear();
		_selectedElement = null;
		_selectedBorder = null;
		PropertiesPanel.IsVisible = false;

		UpdateCanvasSize();

		foreach (var element in _template.Elements)
			CanvasLayout.Children.Add(CreateElementView(element));
	}

	Border CreateElementView(LabelElement element)
	{
		var border = new Border
		{
			Content = CreateInnerView(element),
			Padding = 2,
			StrokeThickness = 2,
			Stroke = Colors.Transparent,
			BackgroundColor = Colors.Transparent,
		};

		AttachGestures(border, element);
		PositionOnCanvas(border, element);
		return border;
	}

	View CreateInnerView(LabelElement element) => LabelCanvasRenderer.CreateView(element, bv => bv.ToString());

	static void PositionOnCanvas(View view, LabelElement element) => LabelCanvasRenderer.PositionOnCanvas(view, element);

	// ---------- Auswahl & Ziehen ----------

	void AttachGestures(Border border, LabelElement element)
	{
		double startXmm = 0, startYmm = 0;

		var pan = new PanGestureRecognizer();
		pan.PanUpdated += (s, e) =>
		{
			if (e.StatusType == GestureStatus.Started)
			{
				startXmm = element.X;
				startYmm = element.Y;
				SelectElement(element, border);
			}
			else if (e.StatusType == GestureStatus.Running)
			{
				double newX = Math.Clamp(startXmm + e.TotalX / PixelsPerMm, 0, Math.Max(0, _template.WidthMm - 2));
				double newY = Math.Clamp(startYmm + e.TotalY / PixelsPerMm, 0, Math.Max(0, _template.HeightMm - 2));
				element.X = newX;
				element.Y = newY;
				PositionOnCanvas(border, element);

				if (ReferenceEquals(_selectedElement, element))
				{
					_suppressPropertyChanged = true;
					XEntry.Text = newX.ToString("0.#");
					YEntry.Text = newY.ToString("0.#");
					_suppressPropertyChanged = false;
				}
			}
		};
		border.GestureRecognizers.Add(pan);

		var tap = new TapGestureRecognizer();
		tap.Tapped += (s, e) => SelectElement(element, border);
		border.GestureRecognizers.Add(tap);
	}

	void SelectElement(LabelElement element, Border border)
	{
		if (_selectedBorder is not null)
			_selectedBorder.Stroke = Colors.Transparent;

		_selectedElement = element;
		_selectedBorder = border;
		border.Stroke = Colors.DodgerBlue;

		ShowPropertiesFor(element);
	}

	static void RefreshPlaceholderPicker(Picker picker, LabelTemplate template, string currentKey)
	{
		var keys = template.Placeholders.Select(p => p.Key).ToList();
		picker.ItemsSource = keys;
		picker.SelectedIndex = keys.IndexOf(currentKey);
	}

	static void SetPlaceholderMode(CheckBox checkBox, Entry literalEntry, Picker picker, bool isPlaceholder)
	{
		checkBox.IsChecked = isPlaceholder;
		literalEntry.IsVisible = !isPlaceholder;
		picker.IsVisible = isPlaceholder;
	}

	void UpdateBarcodeSymbologyGroups(BarcodeSymbology symbology)
	{
		bool is2D = LabelCanvasRenderer.IsSymbology2D(symbology);
		Barcode1DPropertiesGroup.IsVisible = !is2D;
		Barcode2DPropertiesGroup.IsVisible = is2D;
		BarcodeQrEcGroup.IsVisible = symbology == BarcodeSymbology.QrCode;
	}

	void ShowPropertiesFor(LabelElement element)
	{
		_suppressPropertyChanged = true;

		PropertiesPanel.IsVisible = true;
		XEntry.Text = element.X.ToString("0.#");
		YEntry.Text = element.Y.ToString("0.#");

		TextPropertiesGroup.IsVisible = element is TextElement;
		BarcodePropertiesGroup.IsVisible = element is BarcodeElement;
		ImagePropertiesGroup.IsVisible = element is ImageElement;
		FramePropertiesGroup.IsVisible = element is FrameElement;
		LinePropertiesGroup.IsVisible = element is LineElement;

		switch (element)
		{
			case TextElement text:
				SelectedElementLabel.Text = "Text-Element";
				TextValueEntry.Text = text.Text.LiteralValue;
				SetPlaceholderMode(TextIsPlaceholderCheckBox, TextValueEntry, TextPlaceholderPicker, text.Text.IsPlaceholder);
				RefreshPlaceholderPicker(TextPlaceholderPicker, _template, text.Text.PlaceholderKey);
				FontSizeEntry.Text = text.FontSizeMm.ToString("0.#");
				break;

			case BarcodeElement barcode:
				SelectedElementLabel.Text = "Barcode-Element";
				BarcodeSymbologyPicker.SelectedIndex = (int)barcode.Symbology;
				BarcodeDataEntry.Text = barcode.Data.LiteralValue;
				SetPlaceholderMode(BarcodeIsPlaceholderCheckBox, BarcodeDataEntry, BarcodePlaceholderPicker, barcode.Data.IsPlaceholder);
				RefreshPlaceholderPicker(BarcodePlaceholderPicker, _template, barcode.Data.PlaceholderKey);
				BarcodeHeightEntry.Text = barcode.HeightMm.ToString("0.#");
				BarcodeHrCheckBox.IsChecked = barcode.PrintHumanReadable;
				BarcodeMagnificationEntry.Text = barcode.Magnification.ToString();
				QrErrorCorrectionPicker.SelectedIndex = (int)barcode.QrErrorCorrection;
				UpdateBarcodeSymbologyGroups(barcode.Symbology);
				break;

			case ImageElement image:
				SelectedElementLabel.Text = "Bild-Element";
				ImageWidthEntry.Text = image.WidthMm.ToString("0.#");
				ImageHeightEntry.Text = image.HeightMm.ToString("0.#");
				break;

			case FrameElement frame:
				SelectedElementLabel.Text = "Rahmen-Element";
				FrameWidthEntry.Text = frame.WidthMm.ToString("0.#");
				FrameHeightEntry.Text = frame.HeightMm.ToString("0.#");
				FrameThicknessEntry.Text = frame.ThicknessMm.ToString("0.#");
				FrameFilledCheckBox.IsChecked = frame.Filled;
				break;

			case LineElement line:
				SelectedElementLabel.Text = "Linie-Element";
				LineLengthEntry.Text = line.LengthMm.ToString("0.#");
				LineThicknessEntry.Text = line.ThicknessMm.ToString("0.#");
				LineOrientationPicker.SelectedIndex = line.Orientation == LineOrientation.Horizontal ? 0 : 1;
				break;
		}

		_suppressPropertyChanged = false;
	}

	void OnTextIsPlaceholderChanged(object? sender, EventArgs e)
	{
		if (_suppressPropertyChanged || _selectedElement is not TextElement text)
			return;

		bool isPlaceholder = TextIsPlaceholderCheckBox.IsChecked;
		TextValueEntry.IsVisible = !isPlaceholder;
		TextPlaceholderPicker.IsVisible = isPlaceholder;
		if (isPlaceholder)
			RefreshPlaceholderPicker(TextPlaceholderPicker, _template, text.Text.PlaceholderKey);

		OnPropertyChanged(sender, e);
	}

	void OnBarcodeIsPlaceholderChanged(object? sender, EventArgs e)
	{
		if (_suppressPropertyChanged || _selectedElement is not BarcodeElement barcode)
			return;

		bool isPlaceholder = BarcodeIsPlaceholderCheckBox.IsChecked;
		BarcodeDataEntry.IsVisible = !isPlaceholder;
		BarcodePlaceholderPicker.IsVisible = isPlaceholder;
		if (isPlaceholder)
			RefreshPlaceholderPicker(BarcodePlaceholderPicker, _template, barcode.Data.PlaceholderKey);

		OnPropertyChanged(sender, e);
	}

	void OnBarcodeSymbologyChanged(object? sender, EventArgs e)
	{
		if (BarcodeSymbologyPicker.SelectedIndex < 0)
			return;

		UpdateBarcodeSymbologyGroups((BarcodeSymbology)BarcodeSymbologyPicker.SelectedIndex);
		OnPropertyChanged(sender, e);
	}

	void OnPropertyChanged(object? sender, EventArgs e)
	{
		if (_suppressPropertyChanged || _selectedElement is null || _selectedBorder is null)
			return;

		if (double.TryParse(XEntry.Text, out double x))
			_selectedElement.X = x;
		if (double.TryParse(YEntry.Text, out double y))
			_selectedElement.Y = y;

		switch (_selectedElement)
		{
			case TextElement text:
				text.Text = TextIsPlaceholderCheckBox.IsChecked
					? BindableValue.Placeholder(TextPlaceholderPicker.SelectedItem as string ?? string.Empty)
					: BindableValue.Literal(TextValueEntry.Text ?? string.Empty);
				if (double.TryParse(FontSizeEntry.Text, out double fontSize))
					text.FontSizeMm = fontSize;
				break;

			case BarcodeElement barcode:
				if (BarcodeSymbologyPicker.SelectedIndex >= 0)
					barcode.Symbology = (BarcodeSymbology)BarcodeSymbologyPicker.SelectedIndex;
				barcode.Data = BarcodeIsPlaceholderCheckBox.IsChecked
					? BindableValue.Placeholder(BarcodePlaceholderPicker.SelectedItem as string ?? string.Empty)
					: BindableValue.Literal(BarcodeDataEntry.Text ?? string.Empty);
				if (double.TryParse(BarcodeHeightEntry.Text, out double barcodeHeight))
					barcode.HeightMm = barcodeHeight;
				barcode.PrintHumanReadable = BarcodeHrCheckBox.IsChecked;
				if (int.TryParse(BarcodeMagnificationEntry.Text, out int magnification))
					barcode.Magnification = magnification;
				if (QrErrorCorrectionPicker.SelectedIndex >= 0)
					barcode.QrErrorCorrection = (QrErrorCorrection)QrErrorCorrectionPicker.SelectedIndex;
				break;

			case ImageElement image:
				if (double.TryParse(ImageWidthEntry.Text, out double imageWidth))
					image.WidthMm = imageWidth;
				if (double.TryParse(ImageHeightEntry.Text, out double imageHeight))
					image.HeightMm = imageHeight;
				break;

			case FrameElement frame:
				if (double.TryParse(FrameWidthEntry.Text, out double frameWidth))
					frame.WidthMm = frameWidth;
				if (double.TryParse(FrameHeightEntry.Text, out double frameHeight))
					frame.HeightMm = frameHeight;
				if (double.TryParse(FrameThicknessEntry.Text, out double frameThickness))
					frame.ThicknessMm = frameThickness;
				frame.Filled = FrameFilledCheckBox.IsChecked;
				break;

			case LineElement line:
				if (double.TryParse(LineLengthEntry.Text, out double lineLength))
					line.LengthMm = lineLength;
				if (double.TryParse(LineThicknessEntry.Text, out double lineThickness))
					line.ThicknessMm = lineThickness;
				line.Orientation = LineOrientationPicker.SelectedIndex == 1 ? LineOrientation.Vertical : LineOrientation.Horizontal;
				break;
		}

		_selectedBorder.Content = CreateInnerView(_selectedElement);
		PositionOnCanvas(_selectedBorder, _selectedElement);
	}

	// ---------- Elemente hinzufügen/löschen ----------

	void AddElement(LabelElement element)
	{
		_template.Elements.Add(element);
		var border = CreateElementView(element);
		CanvasLayout.Children.Add(border);
		SelectElement(element, border);
	}

	void OnAddTextClicked(object? sender, EventArgs e) => AddElement(new TextElement { X = 10, Y = 10 });

	void OnAddBarcodeClicked(object? sender, EventArgs e) => AddElement(new BarcodeElement { X = 10, Y = 40 });

	void OnAddFrameClicked(object? sender, EventArgs e) => AddElement(new FrameElement { X = 10, Y = 70 });

	void OnAddLineClicked(object? sender, EventArgs e) => AddElement(new LineElement { X = 10, Y = 70 });

	async void OnAddImageClicked(object? sender, EventArgs e)
	{
		FileResult? file;
		try
		{
			file = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "Bild auswählen",
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

		using var stream = await file.OpenReadAsync();
		using var ms = new MemoryStream();
		await stream.CopyToAsync(ms);

		AddElement(new ImageElement { X = 10, Y = 10, ImageBase64 = Convert.ToBase64String(ms.ToArray()) });
	}

	void OnDeleteElementClicked(object? sender, EventArgs e)
	{
		if (_selectedElement is null || _selectedBorder is null)
			return;

		_template.Elements.Remove(_selectedElement);
		CanvasLayout.Children.Remove(_selectedBorder);
		_selectedElement = null;
		_selectedBorder = null;
		PropertiesPanel.IsVisible = false;
	}

	// ---------- Platzhalter verwalten ----------

	async void OnManagePlaceholdersClicked(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new PlaceholderManagerPage(_template));
	}

	// ---------- Medium ----------

	async void OnManageMediaClicked(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new MediaManagerPage(_template));
	}

	async void OnPropertiesClicked(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new TemplatePropertiesPage(_template));
	}

	// ---------- Neu / Speichern / Laden / Exportieren / Importieren / Drucken ----------

	async void OnNewClicked(object? sender, EventArgs e)
	{
		bool confirmed = await DisplayAlertAsync("Neue Vorlage", "Aktuelles Design verwerfen und neu beginnen?", "Ja", "Abbrechen");
		if (!confirmed)
			return;

		var settings = _settingsStore.Load();
		_template = new LabelTemplate
		{
			Name = "Neue Vorlage",
			WidthMm = settings.LabelWidthMm,
			HeightMm = settings.LabelHeightMm,
			Dpi = settings.Dpi,
		};
		RenderCanvas();
	}

	async void OnSaveClicked(object? sender, EventArgs e)
	{
		string? name = await DisplayPromptAsync("Vorlage speichern", "Name der Vorlage:", initialValue: _template.Name);
		if (string.IsNullOrWhiteSpace(name))
			return;

		_template.Name = name.Trim();
		await _store.SaveAsync(_template);
		TitleLabel.Text = $"Label-Designer – {_template.Name} ({_template.WidthMm:0.#}×{_template.HeightMm:0.#} mm)";
		await DisplayAlertAsync("Gespeichert", $"Vorlage \"{_template.Name}\" wurde gespeichert.", "OK");
	}

	async void OnLoadClicked(object? sender, EventArgs e)
	{
		var names = await _store.ListTemplateNamesAsync();
		if (names.Count == 0)
		{
			await DisplayAlertAsync("Keine Vorlagen", "Es sind noch keine Vorlagen gespeichert.", "OK");
			return;
		}

		string choice = await DisplayActionSheetAsync("Vorlage laden", "Abbrechen", null, names.ToArray());
		if (string.IsNullOrEmpty(choice) || choice == "Abbrechen")
			return;

		var loaded = await _store.LoadAsync(choice);
		if (loaded is null)
		{
			await DisplayAlertAsync("Fehler", "Vorlage konnte nicht geladen werden.", "OK");
			return;
		}

		_template = loaded;
		RenderCanvas();
	}

	async void OnExportClicked(object? sender, EventArgs e)
	{
		const string saveAsOption = "Speichern unter...";
		const string shareOption = "Teilen...";

		string choice = await DisplayActionSheetAsync("Vorlage exportieren", "Abbrechen", null, saveAsOption, shareOption);
		if (string.IsNullOrEmpty(choice) || choice == "Abbrechen")
			return;

		try
		{
			string json = System.Text.Json.JsonSerializer.Serialize(_template, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
			string fileName = $"{LabelTemplateStore.SanitizeFileName(_template.Name)}.json";

			if (choice == saveAsOption)
				await ExportSaveAsAsync(fileName, json);
			else
				await ExportShareAsync(fileName, json);
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Fehler", $"Export fehlgeschlagen: {ex.Message}", "OK");
		}
	}

	async Task ExportShareAsync(string fileName, string json)
	{
		string tempPath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
		await File.WriteAllTextAsync(tempPath, json);

		await Share.Default.RequestAsync(new ShareFileRequest
		{
			Title = "Vorlage exportieren",
			File = new ShareFile(tempPath),
		});
	}

	async Task ExportSaveAsAsync(string fileName, string json)
	{
#if WINDOWS
		var nativeWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
		if (nativeWindow is null)
		{
			await ExportShareAsync(fileName, json);
			return;
		}

		var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
		var picker = new Windows.Storage.Pickers.FileSavePicker();
		WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
		picker.SuggestedFileName = Path.GetFileNameWithoutExtension(fileName);
		picker.FileTypeChoices.Add("JSON-Vorlage", new List<string> { ".json" });

		var file = await picker.PickSaveFileAsync();
		if (file is null)
			return;

		await Windows.Storage.FileIO.WriteTextAsync(file, json);
		await DisplayAlertAsync("Gespeichert", $"Vorlage wurde unter \"{file.Path}\" gespeichert.", "OK");
#else
		// Android/iOS/MacCatalyst haben ohne Zusatzpaket (z.B. CommunityToolkit.Maui.Storage, aktuell
		// wegen einer Versionskollision mit Microsoft.Maui.Controls nicht einbindbar) keinen
		// systemweiten "Speichern unter"-Dialog – dort bleibt Teilen der Weg, um die Datei z.B. direkt
		// in "Dateien"/Drive/OneDrive abzulegen.
		await ExportShareAsync(fileName, json);
#endif
	}

	async void OnImportClicked(object? sender, EventArgs e)
	{
		try
		{
			var jsonFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
			{
				{ DevicePlatform.WinUI, new[] { ".json" } },
				{ DevicePlatform.Android, new[] { "application/json" } },
				{ DevicePlatform.iOS, new[] { "public.json" } },
				{ DevicePlatform.MacCatalyst, new[] { "json" } },
			});

			var file = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "Vorlage importieren",
				FileTypes = jsonFileType,
			});

			if (file is null)
				return;

			string json = await File.ReadAllTextAsync(file.FullPath);
			var imported = System.Text.Json.JsonSerializer.Deserialize<LabelTemplate>(json);
			if (imported is null)
			{
				await DisplayAlertAsync("Fehler", "Datei enthält keine gültige Vorlage.", "OK");
				return;
			}

			_template = imported;
			RenderCanvas();
			await DisplayAlertAsync("Importiert", $"Vorlage \"{_template.Name}\" wurde importiert.", "OK");
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Fehler", $"Import fehlgeschlagen: {ex.Message}", "OK");
		}
	}

	async void OnPrintClicked(object? sender, EventArgs e)
	{
		var settings = _settingsStore.Load();
		if (string.IsNullOrWhiteSpace(settings.IpAddress))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Drucker-Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		if (_template.Elements.Count == 0)
		{
			await DisplayAlertAsync("Leeres Label", "Bitte zuerst mindestens ein Element hinzufügen.", "OK");
			return;
		}

		if (_template.Placeholders.Count > 0)
		{
			bool goToTest = await DisplayAlertAsync(
				"Vorlage mit Platzhaltern",
				"Diese Vorlage enthält Platzhalter. Zum Befüllen und Drucken bitte den Test-Modus verwenden.",
				"Zum Test-Modus",
				"Abbrechen");

			if (goToTest)
				await Navigation.PushAsync(new TemplateTestPage(_template));

			return;
		}

		string zpl = LabelTemplateRenderer.ToZpl(_template);
		var result = await _printerService.SendZplAsync(settings.IpAddress, settings.Port, zpl);

		await DisplayAlertAsync(
			result.Success ? "Gesendet" : "Fehler",
			result.Success ? "Label wurde an den Drucker gesendet." : result.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}
}
