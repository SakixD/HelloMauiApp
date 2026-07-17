using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp;

/// <summary>
/// Druckmedien-Presets verwalten (anlegen/bearbeiten/löschen), eines auf die übergebene Vorlage
/// anwenden (setzt Breite/Höhe + Medien-Referenz) oder die zuletzt kalibrierte Etikettenlänge direkt
/// vom Drucker erkennen lassen.
/// </summary>
public partial class MediaManagerPage : ContentPage
{
	readonly LabelTemplate _template;
	readonly PrintMediaStore _store = new();
	readonly PrinterProfileStore _profileStore = new();
	readonly IPrinterService _printerService = new ZplPrinterService();

	PrintMedia? _editing;
	bool _isNew;

	public MediaManagerPage(LabelTemplate template)
	{
		InitializeComponent();
		_template = template;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshListAsync();
	}

	async Task RefreshListAsync()
	{
		MediaListLayout.Children.Clear();

		var mediaList = await _store.ListAsync();
		if (mediaList.Count == 0)
		{
			MediaListLayout.Children.Add(new Label { Text = "Noch keine Medien gespeichert.", TextColor = Colors.Gray });
			return;
		}

		foreach (var media in mediaList)
			MediaListLayout.Children.Add(CreateRow(media));
	}

	View CreateRow(PrintMedia media)
	{
		var grid = new Grid { ColumnSpacing = 8 };
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

		bool isActive = _template.PrintParameters.PreferredMediaId == media.Id;
		string suffix = isActive ? "  ✓ aktiv" : string.Empty;
		var nameLabel = new Label
		{
			Text = $"{media.Name}  ({media.WidthMm:0.#}×{media.HeightMm:0.#} mm, {SensorLabel(media.SensorType)}){suffix}",
			VerticalOptions = LayoutOptions.Center,
			LineBreakMode = LineBreakMode.TailTruncation,
		};
		var applyButton = new Button { Text = "Übernehmen" };
		var editButton = new Button { Text = "Bearbeiten" };

		applyButton.Clicked += (s, e) => ApplyMedia(media);
		editButton.Clicked += (s, e) => EditMedia(media);

		Grid.SetColumn(nameLabel, 0);
		Grid.SetColumn(applyButton, 1);
		Grid.SetColumn(editButton, 2);
		grid.Children.Add(nameLabel);
		grid.Children.Add(applyButton);
		grid.Children.Add(editButton);

		return new Border { Padding = 10, StrokeThickness = 1, Stroke = Colors.LightGray, Content = grid };
	}

	static string SensorLabel(MediaSensorType type) => type switch
	{
		MediaSensorType.BlackMark => "Schwarzmarke",
		MediaSensorType.Continuous => "Endlos",
		_ => "Lücke",
	};

	void ApplyToTemplate(PrintMedia media)
	{
		_template.WidthMm = media.WidthMm;
		_template.HeightMm = media.HeightMm;
		_template.PrintParameters.PreferredMediaId = media.Id;
	}

	async void ApplyMedia(PrintMedia media)
	{
		ApplyToTemplate(media);
		await Navigation.PopAsync();
	}

	void EditMedia(PrintMedia media)
	{
		_editing = media;
		_isNew = false;
		ShowEditPanel(media);
	}

	void OnAddMediaClicked(object? sender, EventArgs e)
	{
		_editing = new PrintMedia();
		_isNew = true;
		ShowEditPanel(_editing);
	}

	void ShowEditPanel(PrintMedia media)
	{
		EditPanel.IsVisible = true;
		NameEntry.Text = media.Name;
		WidthEntry.Text = media.WidthMm.ToString("0.#");
		HeightEntry.Text = media.HeightMm.ToString("0.#");
		GapEntry.Text = media.GapMm.ToString("0.#");
		SensorPicker.SelectedIndex = (int)media.SensorType;
		MaterialEntry.Text = media.Material;
	}

	async void OnSaveMediaClicked(object? sender, EventArgs e)
	{
		if (_editing is null)
			return;

		if (string.IsNullOrWhiteSpace(NameEntry.Text))
		{
			await DisplayAlertAsync("Ungültig", "Bitte einen Namen eingeben.", "OK");
			return;
		}

		_editing.Name = NameEntry.Text.Trim();
		if (double.TryParse(WidthEntry.Text, out double width))
			_editing.WidthMm = width;
		if (double.TryParse(HeightEntry.Text, out double height))
			_editing.HeightMm = height;
		if (double.TryParse(GapEntry.Text, out double gap))
			_editing.GapMm = gap;
		_editing.SensorType = (MediaSensorType)Math.Max(0, SensorPicker.SelectedIndex);
		_editing.Material = MaterialEntry.Text?.Trim() ?? string.Empty;

		await _store.SaveAsync(_editing);

		EditPanel.IsVisible = false;
		_editing = null;
		await RefreshListAsync();
	}

	async void OnDeleteMediaClicked(object? sender, EventArgs e)
	{
		if (_editing is null || _isNew)
		{
			EditPanel.IsVisible = false;
			_editing = null;
			return;
		}

		bool confirmed = await DisplayAlertAsync("Medium löschen", $"Medium \"{_editing.Name}\" wirklich löschen?", "Löschen", "Abbrechen");
		if (!confirmed)
			return;

		await _store.DeleteAsync(_editing.Id);
		EditPanel.IsVisible = false;
		_editing = null;
		await RefreshListAsync();
	}

	void OnCancelEditClicked(object? sender, EventArgs e)
	{
		EditPanel.IsVisible = false;
		_editing = null;
	}

	async void OnDetectClicked(object? sender, EventArgs e)
	{
		if (_profileStore.GetDefault() is not { } profile)
		{
			await DisplayAlertAsync("Kein Drucker", "Bitte zuerst unter „Einstellungen“ ein Druckerprofil anlegen.", "OK");
			return;
		}

		DetectBtn.IsEnabled = false;
		var status = await _printerService.GetDetailedStatusAsync(profile);
		DetectBtn.IsEnabled = true;

		if (!status.Success)
		{
			DetectResultLabel.Text = $"Fehler: {status.ErrorMessage}";
			DetectResultLabel.IsVisible = true;
			return;
		}

		var parts = new List<string>();
		if (status.LabelLengthDots is int dots)
			parts.Add($"Etikettenlänge: {ZplLabelBuilder.DotsToMm(dots, profile.Dpi):0.#} mm");
		if (status.PaperOut is bool paperOut)
			parts.Add(paperOut ? "Kein Papier!" : "Papier OK");
		if (status.RibbonOut is bool ribbonOut)
			parts.Add(ribbonOut ? "Kein Farbband!" : "Farbband OK");
		if (status.HeadOpen is bool headOpen)
			parts.Add(headOpen ? "Druckkopf offen!" : "Druckkopf geschlossen");

		DetectResultLabel.Text = parts.Count > 0
			? string.Join("  •  ", parts) + "  (Breite bitte manuell eintragen.)"
			: "Keine auswertbaren Felder in der Statusantwort.";
		DetectResultLabel.IsVisible = true;

		if (status.LabelLengthDots is int lengthDots)
		{
			_editing = new PrintMedia { HeightMm = Math.Round(ZplLabelBuilder.DotsToMm(lengthDots, profile.Dpi), 1) };
			_isNew = true;
			ShowEditPanel(_editing);
		}
	}
}
