using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloMauiApp.Services;
using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp.ViewModels;

/// <summary>Eine Zeile der Vorlagenübersicht (fertige Anzeige-Strings).</summary>
public record TemplateListItem(string Name, string? Subtitle, bool HasSubtitle);

/// <summary>
/// ViewModel der Vorlagenübersicht (Rail-Ziel "templates"): Vorlagen im Designer öffnen oder
/// löschen. Löschen arbeitet rein dateibasiert (kein Deserialisieren nötig) und funktioniert
/// deshalb auch für Vorlagen, die wegen eines veralteten Formats nicht mehr geladen werden können.
/// </summary>
public partial class TemplateManagerViewModel : ViewModelBase
{
	readonly ILabelTemplateStore _store;
	readonly IAlertService _alertService;

	/// <summary>Von <see cref="AppShell"/> gesetzt: öffnet die Vorlage im (dauerhaften) Designer statt sie zu pushen.</summary>
	public Action<LabelTemplate>? OpenInDesigner { get; set; }

	[ObservableProperty] List<TemplateListItem> templates = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNoTemplates))]
	bool hasTemplates;

	public bool HasNoTemplates => !HasTemplates;

	public TemplateManagerViewModel(ILabelTemplateStore store, IAlertService alertService)
	{
		_store = store;
		_alertService = alertService;
	}

	public Task OnActivatedAsync() => RefreshAsync();

	async Task RefreshAsync()
	{
		var names = await _store.ListTemplateNamesAsync();
		var items = new List<TemplateListItem>();

		foreach (var name in names)
		{
			var template = await _store.LoadAsync(name);
			string? subtitle = BuildSubtitle(template);
			items.Add(new TemplateListItem(name, subtitle, subtitle is not null));
		}

		Templates = items;
		HasTemplates = items.Count > 0;
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

	[RelayCommand]
	async Task OpenAsync(TemplateListItem item)
	{
		LabelTemplate? template = await _store.LoadAsync(item.Name);
		if (template is null)
		{
			bool delete = await _alertService.ConfirmAsync(
				"Kann nicht geöffnet werden",
				$"Vorlage \"{item.Name}\" konnte nicht geladen werden – vermutlich wurde sie mit einer älteren App-Version gespeichert und ist mit dem aktuellen Format nicht mehr kompatibel. Soll sie gelöscht werden?",
				"Löschen",
				"Abbrechen");

			// Bewusst ohne zweite Rückfrage: der Nutzer hat gerade schon "Löschen" gewählt.
			if (delete)
				await DeleteFileAndRefreshAsync(item.Name);

			return;
		}

		OpenInDesigner?.Invoke(template);
	}

	[RelayCommand]
	async Task DeleteAsync(TemplateListItem item)
	{
		bool confirmed = await _alertService.ConfirmAsync("Vorlage löschen", $"Vorlage \"{item.Name}\" wirklich löschen?", "Löschen", "Abbrechen");
		if (!confirmed)
			return;

		await DeleteFileAndRefreshAsync(item.Name);
	}

	async Task DeleteFileAndRefreshAsync(string name)
	{
		await _store.DeleteAsync(name);
		await RefreshAsync();
	}
}
