using LabelPrinting.Models;
using Microsoft.Maui.Controls.Shapes;

namespace HelloMauiApp;

public partial class PlaceholderManagerPage : ContentPage
{
	readonly LabelTemplate _template;
	PlaceholderDefinition? _editing;
	bool _isNew;

	public PlaceholderManagerPage(LabelTemplate template)
	{
		InitializeComponent();
		_template = template;
		RefreshList();
	}

	void RefreshList()
	{
		PlaceholderListLayout.Children.Clear();

		if (_template.Placeholders.Count == 0)
		{
			var empty = new Label { Text = "Noch keine Platzhalter angelegt." };
			empty.SetDynamicResource(Label.TextColorProperty, "ColorText2");
			PlaceholderListLayout.Children.Add(empty);
			return;
		}

		foreach (var placeholder in _template.Placeholders)
			PlaceholderListLayout.Children.Add(CreateRow(placeholder));
	}

	/// <summary>Listenzeile im Stil der Profilliste (Karte mit Tap statt Vollbreiten-Button) – Farben als Theme-Tokens.</summary>
	View CreateRow(PlaceholderDefinition placeholder)
	{
		var nameLabel = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold, Text = placeholder.Key };
		nameLabel.SetDynamicResource(Label.TextColorProperty, "ColorText");

		var detailLabel = new Label { FontSize = 12, Text = $"{TypeLabel(placeholder.Type)} · {(placeholder.Required ? "Pflichtfeld" : "optional")}" };
		detailLabel.SetDynamicResource(Label.TextColorProperty, "ColorText2");

		var editLabel = new Label { FontSize = 12.5, FontAttributes = FontAttributes.Bold, Text = "Bearbeiten", VerticalOptions = LayoutOptions.Center };
		editLabel.SetDynamicResource(Label.TextColorProperty, "AccentColor");

		var textStack = new VerticalStackLayout { Spacing = 2 };
		textStack.Children.Add(nameLabel);
		textStack.Children.Add(detailLabel);

		var grid = new Grid { ColumnSpacing = 10 };
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
		Grid.SetColumn(editLabel, 1);
		grid.Children.Add(textStack);
		grid.Children.Add(editLabel);

		var border = new Border
		{
			Padding = new Thickness(16, 12),
			StrokeThickness = 1,
			StrokeShape = new RoundRectangle { CornerRadius = 10 },
			Content = grid,
		};
		border.SetDynamicResource(Border.StrokeProperty, "ColorStroke");
		border.SetDynamicResource(VisualElement.BackgroundColorProperty, "ColorLayer");

		var captured = placeholder;
		var tap = new TapGestureRecognizer();
		tap.Tapped += (s, e) => EditPlaceholder(captured);
		border.GestureRecognizers.Add(tap);

		return border;
	}

	static string TypeLabel(PlaceholderType type) => type switch
	{
		PlaceholderType.Number => "Zahl",
		PlaceholderType.Date => "Datum",
		_ => "Text",
	};

	void EditPlaceholder(PlaceholderDefinition placeholder)
	{
		_editing = placeholder;
		_isNew = false;

		EditPanel.IsVisible = true;
		KeyEntry.Text = placeholder.Key;
		TypePicker.SelectedIndex = (int)placeholder.Type;
		RequiredCheckBox.IsChecked = placeholder.Required;
		DefaultValueEntry.Text = placeholder.DefaultValue;
	}

	void OnAddPlaceholderClicked(object? sender, EventArgs e)
	{
		_editing = new PlaceholderDefinition();
		_isNew = true;

		EditPanel.IsVisible = true;
		KeyEntry.Text = string.Empty;
		TypePicker.SelectedIndex = 0;
		RequiredCheckBox.IsChecked = true;
		DefaultValueEntry.Text = string.Empty;
	}

	async void OnSavePlaceholderClicked(object? sender, EventArgs e)
	{
		if (_editing is null)
			return;

		string key = KeyEntry.Text?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(key))
		{
			await DisplayAlertAsync("Ungültig", "Bitte einen Key eingeben.", "OK");
			return;
		}

		bool keyTakenByOther = _template.Placeholders.Any(p => !ReferenceEquals(p, _editing) && p.Key == key);
		if (keyTakenByOther)
		{
			await DisplayAlertAsync("Ungültig", $"Der Key \"{key}\" wird bereits verwendet.", "OK");
			return;
		}

		_editing.Key = key;
		_editing.Type = (PlaceholderType)Math.Max(0, TypePicker.SelectedIndex);
		_editing.Required = RequiredCheckBox.IsChecked;
		_editing.DefaultValue = string.IsNullOrWhiteSpace(DefaultValueEntry.Text) ? null : DefaultValueEntry.Text.Trim();

		if (_isNew)
			_template.Placeholders.Add(_editing);

		EditPanel.IsVisible = false;
		_editing = null;
		RefreshList();
	}

	void OnDeletePlaceholderClicked(object? sender, EventArgs e)
	{
		if (_editing is not null && !_isNew)
			_template.Placeholders.Remove(_editing);

		EditPanel.IsVisible = false;
		_editing = null;
		RefreshList();
	}
}
