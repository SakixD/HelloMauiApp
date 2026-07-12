using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp;

public partial class TemplateTestPage : ContentPage
{
	readonly LabelTemplateStore _store = new();
	readonly IPrinterService _printerService = new ZplPrinterService();
	readonly PrinterSettingsStore _settingsStore = new();
	readonly Dictionary<string, View> _fieldControls = [];

	LabelTemplate? _template;

	public TemplateTestPage()
	{
		InitializeComponent();
	}

	public TemplateTestPage(LabelTemplate template)
	{
		InitializeComponent();
		_template = template;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (_template is null)
			await ChooseTemplateAsync();
		else
			BuildForm();
	}

	async Task ChooseTemplateAsync()
	{
		var names = await _store.ListTemplateNamesAsync();
		if (names.Count == 0)
		{
			await DisplayAlertAsync("Keine Vorlagen", "Es sind noch keine Vorlagen gespeichert.", "OK");
			if (_template is null && Navigation.NavigationStack.Count > 1)
				await Navigation.PopAsync();
			return;
		}

		string choice = await DisplayActionSheetAsync("Vorlage wählen", "Abbrechen", null, names.ToArray());
		if (string.IsNullOrEmpty(choice) || choice == "Abbrechen")
		{
			if (_template is null && Navigation.NavigationStack.Count > 1)
				await Navigation.PopAsync();
			return;
		}

		var loaded = await _store.LoadAsync(choice);
		if (loaded is null)
		{
			await DisplayAlertAsync("Fehler", "Vorlage konnte nicht geladen werden.", "OK");
			return;
		}

		_template = loaded;
		BuildForm();
	}

	async void OnChooseTemplateClicked(object? sender, EventArgs e) => await ChooseTemplateAsync();

	void BuildForm()
	{
		if (_template is null)
			return;

		TitleLabel.Text = $"Vorlage: {_template.Name}";
		FormLayout.Children.Clear();
		_fieldControls.Clear();
		ValidationErrorLabel.IsVisible = false;
		PreviewCanvas.Children.Clear();

		if (_template.Placeholders.Count == 0)
		{
			FormLayout.Children.Add(new Label { Text = "Diese Vorlage hat keine Platzhalter.", TextColor = Colors.Gray });
			return;
		}

		foreach (var placeholder in _template.Placeholders)
		{
			string label = placeholder.Key + (placeholder.Required ? " *" : " (optional)");
			FormLayout.Children.Add(new Label { Text = label, FontAttributes = FontAttributes.Bold });

			View input = placeholder.Type switch
			{
				PlaceholderType.Date => new DatePicker(),
				PlaceholderType.Number => new Entry { Keyboard = Keyboard.Numeric, Placeholder = placeholder.DefaultValue ?? string.Empty },
				_ => new Entry { Placeholder = placeholder.DefaultValue ?? string.Empty },
			};

			FormLayout.Children.Add(input);
			_fieldControls[placeholder.Key] = input;
		}
	}

	Dictionary<string, string> CollectFormData()
	{
		var data = new Dictionary<string, string>();
		foreach (var (key, control) in _fieldControls)
		{
			data[key] = control switch
			{
				DatePicker datePicker => $"{datePicker.Date:dd.MM.yyyy}",
				Entry entry => entry.Text ?? string.Empty,
				_ => string.Empty,
			};
		}

		return data;
	}

	TemplateFillResult? Validate()
	{
		if (_template is null)
			return null;

		var result = LabelTemplateFillService.Fill(_template, CollectFormData());
		if (!result.Success)
		{
			ValidationErrorLabel.Text = "Fehlende Pflichtfelder: " + string.Join(", ", result.MissingRequiredKeys);
			ValidationErrorLabel.IsVisible = true;
			return null;
		}

		ValidationErrorLabel.IsVisible = false;
		return result;
	}

	void RenderPreview(IReadOnlyDictionary<string, string> data)
	{
		if (_template is null)
			return;

		PreviewCanvas.Children.Clear();
		PreviewCanvas.WidthRequest = Math.Max(10, _template.WidthMm * LabelCanvasRenderer.PixelsPerMm);
		PreviewCanvas.HeightRequest = Math.Max(10, _template.HeightMm * LabelCanvasRenderer.PixelsPerMm);

		foreach (var element in _template.Elements)
		{
			var view = LabelCanvasRenderer.CreateView(element, bv => bv.Resolve(data));
			LabelCanvasRenderer.PositionOnCanvas(view, element);
			PreviewCanvas.Children.Add(view);
		}
	}

	void OnPreviewClicked(object? sender, EventArgs e)
	{
		var result = Validate();
		if (result is null)
			return;

		RenderPreview(result.ResolvedData);
	}

	async void OnTestPrintClicked(object? sender, EventArgs e)
	{
		if (_template is null)
			return;

		var result = Validate();
		if (result is null)
			return;

		var settings = _settingsStore.Load();
		if (string.IsNullOrWhiteSpace(settings.IpAddress))
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Drucker-Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		RenderPreview(result.ResolvedData);

		string zpl = LabelTemplateRenderer.ToZpl(_template, result.ResolvedData);
		var printResult = await _printerService.SendZplAsync(settings.IpAddress, settings.Port, zpl);

		await DisplayAlertAsync(
			printResult.Success ? "Gesendet" : "Fehler",
			printResult.Success ? "Testdruck wurde an den Drucker gesendet." : printResult.ErrorMessage ?? "Unbekannter Fehler",
			"OK");
	}
}
