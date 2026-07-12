using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp;

/// <summary>
/// Übersicht aller gespeicherten Vorlagen mit Öffnen- und Löschfunktion. Löschen arbeitet rein
/// dateibasiert (kein Deserialisieren nötig) und funktioniert deshalb auch für Vorlagen, die
/// wegen eines veralteten Formats nicht mehr geladen werden können.
/// </summary>
public partial class TemplateManagerPage : ContentPage
{
	readonly LabelTemplateStore _store = new();

	public TemplateManagerPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshList();
	}

	async Task RefreshList()
	{
		ListLayout.Children.Clear();

		var names = await _store.ListTemplateNamesAsync();
		if (names.Count == 0)
		{
			ListLayout.Children.Add(new Label { Text = "Noch keine Vorlagen gespeichert.", TextColor = Colors.Gray });
			return;
		}

		foreach (var name in names)
			ListLayout.Children.Add(CreateRow(name));
	}

	View CreateRow(string name)
	{
		var grid = new Grid { ColumnSpacing = 8 };
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

		var nameLabel = new Label { Text = name, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
		var openButton = new Button { Text = "Öffnen" };
		var deleteButton = new Button { Text = "Löschen", BackgroundColor = Colors.IndianRed, TextColor = Colors.White };

		openButton.Clicked += async (s, e) => await OpenTemplateAsync(name);
		deleteButton.Clicked += async (s, e) => await DeleteTemplateAsync(name);

		Grid.SetColumn(nameLabel, 0);
		Grid.SetColumn(openButton, 1);
		Grid.SetColumn(deleteButton, 2);

		grid.Children.Add(nameLabel);
		grid.Children.Add(openButton);
		grid.Children.Add(deleteButton);

		return new Border
		{
			Padding = 10,
			StrokeThickness = 1,
			Stroke = Colors.LightGray,
			Content = grid,
		};
	}

	async Task OpenTemplateAsync(string name)
	{
		LabelTemplate? template = await _store.LoadAsync(name);
		if (template is null)
		{
			bool delete = await DisplayAlertAsync(
				"Kann nicht geöffnet werden",
				$"Vorlage \"{name}\" konnte nicht geladen werden – vermutlich wurde sie mit einer älteren App-Version gespeichert und ist mit dem aktuellen Format nicht mehr kompatibel. Soll sie gelöscht werden?",
				"Löschen",
				"Abbrechen");

			if (delete)
				await DeleteTemplateAsync(name);

			return;
		}

		await Navigation.PushAsync(new DesignerPage(template));
	}

	async Task DeleteTemplateAsync(string name)
	{
		bool confirmed = await DisplayAlertAsync("Vorlage löschen", $"Vorlage \"{name}\" wirklich löschen?", "Löschen", "Abbrechen");
		if (!confirmed)
			return;

		await _store.DeleteAsync(name);
		await RefreshList();
	}
}
