using HelloMauiApp.Services;
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
	readonly IPrintMediaStore _store;
	readonly IPrinterProfileStore _profileStore;
	readonly IPrinterService _printerService;

	PrintMedia? _editing;
	bool _isNew;

	public MediaManagerPage(IPrintMediaStore store, IPrinterProfileStore profileStore, IPrinterService printerService, LabelTemplate template)
	{
		InitializeComponent();
		_store = store;
		_profileStore = profileStore;
		_printerService = printerService;
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
			var empty = new Label { Text = "Noch keine Medien gespeichert." };
			empty.SetDynamicResource(Label.TextColorProperty, "ColorText2");
			MediaListLayout.Children.Add(empty);
			return;
		}

		foreach (var media in mediaList)
			MediaListLayout.Children.Add(CreateRow(media));
	}

	/// <summary>Listenzeile im Stil der Profilliste – Farben/Styles als Theme-Tokens statt hartcodiert.</summary>
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
			Text = $"{media.Name}  ({media.WidthMm:0.#}×{media.HeightMm:0.#} mm, {MediaDetection.SensorLabel(media.SensorType)}){suffix}",
			VerticalOptions = LayoutOptions.Center,
			LineBreakMode = LineBreakMode.TailTruncation,
			FontSize = 13,
		};
		nameLabel.SetDynamicResource(Label.TextColorProperty, "ColorText");

		var applyButton = new Button { Text = "Übernehmen", VerticalOptions = LayoutOptions.Center };
		applyButton.SetDynamicResource(VisualElement.StyleProperty, "ChipButton");
		var editButton = new Button { Text = "Bearbeiten", VerticalOptions = LayoutOptions.Center };
		editButton.SetDynamicResource(VisualElement.StyleProperty, "ChipButton");

		applyButton.Clicked += (s, e) => ApplyMedia(media);
		editButton.Clicked += (s, e) => EditMedia(media);

		Grid.SetColumn(nameLabel, 0);
		Grid.SetColumn(applyButton, 1);
		Grid.SetColumn(editButton, 2);
		grid.Children.Add(nameLabel);
		grid.Children.Add(applyButton);
		grid.Children.Add(editButton);

		var border = new Border
		{
			Padding = new Thickness(16, 12),
			StrokeThickness = 1,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
			Content = grid,
		};
		border.SetDynamicResource(Border.StrokeProperty, "ColorStroke");
		border.SetDynamicResource(VisualElement.BackgroundColorProperty, "ColorLayer");
		return border;
	}

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

		var detection = MediaDetection.Interpret(status, profile.Dpi);
		DetectResultLabel.Text = detection.SummaryText;
		DetectResultLabel.IsVisible = true;

		if (detection.DetectedMedia is { } detected)
		{
			_editing = detected;
			_isNew = true;
			ShowEditPanel(detected);
		}
	}
}
