using HelloMauiApp.Services;
using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp;

/// <summary>
/// Übersicht aller gespeicherten Vorlagen mit Öffnen- und Löschfunktion. Löschen arbeitet rein
/// dateibasiert (kein Deserialisieren nötig) und funktioniert deshalb auch für Vorlagen, die
/// wegen eines veralteten Formats nicht mehr geladen werden können.
/// </summary>
public partial class TemplateManagerPage : ContentView, IShellSectionView
{
	readonly ILabelTemplateStore _store;
	readonly IAlertService _alertService;

	/// <summary>Von <see cref="AppShell"/> verdrahtet: öffnet die übergebene Vorlage im (dauerhaften) Designer statt sie zu pushen.</summary>
	public Action<LabelTemplate>? OpenInDesigner { get; set; }

	public TemplateManagerPage(ILabelTemplateStore store, IAlertService alertService)
	{
		InitializeComponent();
		_store = store;
		_alertService = alertService;
	}

	public async Task OnActivatedAsync() => await RefreshList();

	async Task RefreshList()
	{
		ListLayout.Children.Clear();

		var names = await _store.ListTemplateNamesAsync();
		if (names.Count == 0)
		{
			var empty = new Label { Text = "Noch keine Vorlagen gespeichert." };
			empty.SetDynamicResource(Label.TextColorProperty, "ColorText2");
			ListLayout.Children.Add(empty);
			return;
		}

		foreach (var name in names)
		{
			var template = await _store.LoadAsync(name);
			ListLayout.Children.Add(CreateRow(name, template));
		}
	}

	/// <summary>Listenzeile im Stil der Profilliste – Farben/Styles als Theme-Tokens statt hartcodiert.</summary>
	View CreateRow(string name, LabelTemplate? template)
	{
		var grid = new Grid { ColumnSpacing = 8 };
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

		var textStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 2 };
		var nameLabel = new Label { Text = name, FontSize = 14, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation };
		nameLabel.SetDynamicResource(Label.TextColorProperty, "ColorText");
		textStack.Children.Add(nameLabel);

		string? subtitle = BuildSubtitle(template);
		if (subtitle is not null)
		{
			var subtitleLabel = new Label { Text = subtitle, FontSize = 12, LineBreakMode = LineBreakMode.TailTruncation };
			subtitleLabel.SetDynamicResource(Label.TextColorProperty, "ColorText2");
			textStack.Children.Add(subtitleLabel);
		}

		var openButton = new Button { Text = "Öffnen", VerticalOptions = LayoutOptions.Center };
		openButton.SetDynamicResource(VisualElement.StyleProperty, "ChipButton");
		var deleteButton = new Button { Text = "Löschen", VerticalOptions = LayoutOptions.Center };
		deleteButton.SetDynamicResource(VisualElement.StyleProperty, "DangerButton");

		openButton.Clicked += async (s, e) => await OpenTemplateAsync(name);
		deleteButton.Clicked += async (s, e) => await DeleteTemplateAsync(name);

		Grid.SetColumn(textStack, 0);
		Grid.SetColumn(openButton, 1);
		Grid.SetColumn(deleteButton, 2);

		grid.Children.Add(textStack);
		grid.Children.Add(openButton);
		grid.Children.Add(deleteButton);

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

	static string? BuildSubtitle(LabelTemplate? template)
	{
		if (template is null)
			return null;

		var parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(template.Metadata.Category))
			parts.Add(template.Metadata.Category);
		if (!string.IsNullOrWhiteSpace(template.Metadata.Description))
			parts.Add(template.Metadata.Description);
		if (template.Metadata.Tags.Count > 0)
			parts.Add(string.Join(", ", template.Metadata.Tags));

		return parts.Count > 0 ? string.Join("  •  ", parts) : null;
	}

	async Task OpenTemplateAsync(string name)
	{
		LabelTemplate? template = await _store.LoadAsync(name);
		if (template is null)
		{
			bool delete = await _alertService.ConfirmAsync(
				"Kann nicht geöffnet werden",
				$"Vorlage \"{name}\" konnte nicht geladen werden – vermutlich wurde sie mit einer älteren App-Version gespeichert und ist mit dem aktuellen Format nicht mehr kompatibel. Soll sie gelöscht werden?",
				"Löschen",
				"Abbrechen");

			if (delete)
				await DeleteTemplateAsync(name);

			return;
		}

		OpenInDesigner?.Invoke(template);
	}

	async Task DeleteTemplateAsync(string name)
	{
		bool confirmed = await _alertService.ConfirmAsync("Vorlage löschen", $"Vorlage \"{name}\" wirklich löschen?", "Löschen", "Abbrechen");
		if (!confirmed)
			return;

		await _store.DeleteAsync(name);
		await RefreshList();
	}
}
