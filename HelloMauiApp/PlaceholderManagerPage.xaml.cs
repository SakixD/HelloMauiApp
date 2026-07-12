using LabelPrinting.Models;

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
			PlaceholderListLayout.Children.Add(new Label { Text = "Noch keine Platzhalter angelegt.", TextColor = Colors.Gray });
			return;
		}

		foreach (var placeholder in _template.Placeholders)
		{
			var button = new Button
			{
				Text = $"{placeholder.Key}  ({TypeLabel(placeholder.Type)}, {(placeholder.Required ? "Pflicht" : "optional")})",
				HorizontalOptions = LayoutOptions.Fill,
			};
			var captured = placeholder;
			button.Clicked += (s, e) => EditPlaceholder(captured);
			PlaceholderListLayout.Children.Add(button);
		}
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
