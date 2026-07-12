using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp;

/// <summary>
/// Zeigt/bearbeitet die Id (nur Anzeige), Metadaten (Beschreibung/Kategorie/Tags) und Druckparameter
/// (bevorzugtes Medium/Geschwindigkeit/Darkness) einer Vorlage. Das bevorzugte Medium wird über den
/// Designer-Button „Medium“ gesetzt, hier nur angezeigt.
/// </summary>
public partial class TemplatePropertiesPage : ContentPage
{
	readonly LabelTemplate _template;
	readonly PrintMediaStore _mediaStore = new();

	public TemplatePropertiesPage(LabelTemplate template)
	{
		InitializeComponent();
		_template = template;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		IdLabel.Text = _template.Id;
		DescriptionEditor.Text = _template.Metadata.Description;
		CategoryEntry.Text = _template.Metadata.Category;
		TagsEntry.Text = string.Join(", ", _template.Metadata.Tags);
		SpeedEntry.Text = _template.PrintParameters.PrintSpeed?.ToString() ?? string.Empty;
		DarknessEntry.Text = _template.PrintParameters.Darkness?.ToString() ?? string.Empty;

		await RefreshPreferredMediaLabelAsync();
	}

	async Task RefreshPreferredMediaLabelAsync()
	{
		string? mediaId = _template.PrintParameters.PreferredMediaId;
		if (string.IsNullOrEmpty(mediaId))
		{
			PreferredMediaLabel.Text = "Keines ausgewählt.";
			return;
		}

		var media = await _mediaStore.LoadAsync(mediaId);
		PreferredMediaLabel.Text = media is not null
			? $"{media.Name} ({media.WidthMm:0.#}×{media.HeightMm:0.#} mm)"
			: "Referenziertes Medium wurde gelöscht.";
	}

	async void OnApplyClicked(object? sender, EventArgs e)
	{
		_template.Metadata.Description = DescriptionEditor.Text ?? string.Empty;
		_template.Metadata.Category = CategoryEntry.Text?.Trim() ?? string.Empty;
		_template.Metadata.Tags = (TagsEntry.Text ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList();

		_template.PrintParameters.PrintSpeed = int.TryParse(SpeedEntry.Text, out int speed) ? speed : null;
		_template.PrintParameters.Darkness = int.TryParse(DarknessEntry.Text, out int darkness) ? darkness : null;

		await Navigation.PopAsync();
	}
}
