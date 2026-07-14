using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloMauiApp.Services;
using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp.ViewModels;

/// <summary>Eine Zeile der Platzhalterübersicht (fertige Anzeige-Strings).</summary>
public record PlaceholderRow(string Key, string Details);

/// <summary>Alle Platzhalter einer gespeicherten Vorlage.</summary>
public record TemplatePlaceholderGroup(string TemplateName, string Subtitle, List<PlaceholderRow> Rows, bool HasRows, LabelTemplate Template);

/// <summary>
/// ViewModel der eigenständigen Platzhalterübersicht (Rail-Ziel "placeholders"). Platzhalter
/// sind im SDK-Modell bewusst Teil der Vorlage (<see cref="LabelTemplate.Placeholders"/>) – diese
/// Seite ist deshalb eine Übersicht über alle gespeicherten Vorlagen mit direktem Absprung in den
/// Platzhalter-Editor (Drill-down, Änderungen werden beim Zurückkehren automatisch gespeichert)
/// oder in den Designer.
/// </summary>
public partial class PlaceholderLibraryViewModel : ViewModelBase
{
	readonly ILabelTemplateStore _templateStore;
	readonly INavigationService _navigationService;

	/// <summary>Von <see cref="AppShell"/> gesetzt (Rail-Navigation ist Shell-Orchestrierung, wie bei TemplateManagerPage).</summary>
	public Action<LabelTemplate>? OpenInDesigner { get; set; }

	/// <summary>Vorlage, deren Platzhalter gerade im Drill-down bearbeitet werden – wird beim Reaktivieren gespeichert.</summary>
	LabelTemplate? _pendingSave;

	[ObservableProperty] string countText = string.Empty;
	[ObservableProperty] List<TemplatePlaceholderGroup> groups = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNoGroups))]
	bool hasGroups;

	public bool HasNoGroups => !HasGroups;

	public PlaceholderLibraryViewModel(ILabelTemplateStore templateStore, INavigationService navigationService)
	{
		_templateStore = templateStore;
		_navigationService = navigationService;
	}

	public async Task OnActivatedAsync()
	{
		if (_pendingSave is not null)
		{
			await _templateStore.SaveAsync(_pendingSave);
			_pendingSave = null;
		}

		await RefreshAsync();
	}

	async Task RefreshAsync()
	{
		var names = await _templateStore.ListTemplateNamesAsync();
		var groups = new List<TemplatePlaceholderGroup>();
		int total = 0;

		foreach (var name in names)
		{
			var template = await _templateStore.LoadAsync(name);
			if (template is null)
				continue;

			var rows = template.Placeholders
				.Select(p => new PlaceholderRow(p.Key, PlaceholderDetails(p)))
				.ToList();

			total += rows.Count;
			string subtitle = rows.Count switch
			{
				0 => "Keine Platzhalter",
				1 => "1 Platzhalter",
				_ => $"{rows.Count} Platzhalter",
			};

			groups.Add(new TemplatePlaceholderGroup(template.Name, subtitle, rows, rows.Count > 0, template));
		}

		Groups = groups;
		HasGroups = groups.Count > 0;
		CountText = $"{total} Platzhalter in {groups.Count} Vorlagen";
	}

	static string PlaceholderDetails(PlaceholderDefinition placeholder)
	{
		string type = placeholder.Type switch
		{
			PlaceholderType.Number => "Zahl",
			PlaceholderType.Date => "Datum",
			_ => "Text",
		};

		string details = $"{type} · {(placeholder.Required ? "Pflicht" : "optional")}";
		if (!string.IsNullOrWhiteSpace(placeholder.DefaultValue))
			details += $" · Standard: {placeholder.DefaultValue}";
		return details;
	}

	[RelayCommand]
	async Task EditAsync(TemplatePlaceholderGroup group)
	{
		// Der Drill-down bearbeitet die Vorlage im Speicher; gespeichert wird beim Zurückkehren
		// (OnActivatedAsync), damit die Änderung nicht nur im RAM lebt.
		_pendingSave = group.Template;
		await _navigationService.PushAsync(new PlaceholderManagerPage(group.Template));
	}

	[RelayCommand]
	void OpenDesigner(TemplatePlaceholderGroup group) => OpenInDesigner?.Invoke(group.Template);
}
