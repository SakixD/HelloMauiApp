using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloMauiApp.Services;
using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp.ViewModels;

/// <summary>Ein Eintrag der Medienliste (fertige Anzeige-Strings + Referenz aufs Modell).</summary>
public record MediaListItem(string Name, string Details, PrintMedia Media);

/// <summary>
/// ViewModel der eigenständigen Medienverwaltung (Rail-Ziel "media"): CRUD über
/// <see cref="IPrintMediaStore"/> plus Medienerkennung vom Drucker (~HS-Status). Ersetzt die
/// frühere ComingSoon-Seite; das vorlagen-bezogene Zuweisen eines Mediums bleibt im Drill-down
/// MediaManagerPage (Designer → Medium).
/// </summary>
public partial class MediaLibraryViewModel : ViewModelBase
{
	readonly IPrintMediaStore _mediaStore;
	readonly IPrinterSettingsStore _settingsStore;
	readonly IPrinterService _printerService;
	readonly IAlertService _alertService;

	PrintMedia? _editing;
	bool _isNew;

	[ObservableProperty] string countText = string.Empty;
	[ObservableProperty] List<MediaListItem> mediaItems = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNoMedia))]
	bool hasMedia;

	public bool HasNoMedia => !HasMedia;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsNotEditing))]
	bool isEditing;

	public bool IsNotEditing => !IsEditing;

	[ObservableProperty] string editorTitle = string.Empty;
	[ObservableProperty] string editName = string.Empty;
	[ObservableProperty] string editWidthText = string.Empty;
	[ObservableProperty] string editHeightText = string.Empty;
	[ObservableProperty] string editGapText = string.Empty;
	[ObservableProperty] int sensorIndex;
	[ObservableProperty] string editMaterial = string.Empty;
	[ObservableProperty] bool canDelete;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasDetectResult))]
	string detectResult = string.Empty;

	public bool HasDetectResult => DetectResult.Length > 0;

	public MediaLibraryViewModel(
		IPrintMediaStore mediaStore,
		IPrinterSettingsStore settingsStore,
		IPrinterService printerService,
		IAlertService alertService)
	{
		_mediaStore = mediaStore;
		_settingsStore = settingsStore;
		_printerService = printerService;
		_alertService = alertService;
	}

	public async Task RefreshAsync()
	{
		var mediaList = await _mediaStore.ListAsync();

		MediaItems = mediaList
			.Select(m => new MediaListItem(
				m.Name,
				$"{m.WidthMm:0.#} × {m.HeightMm:0.#} mm · {SensorLabel(m.SensorType)}"
					+ (m.SensorType == MediaSensorType.Continuous ? string.Empty : $" · Lücke {m.GapMm:0.#} mm")
					+ (string.IsNullOrWhiteSpace(m.Material) ? string.Empty : $" · {m.Material}"),
				m))
			.ToList();

		HasMedia = MediaItems.Count > 0;
		CountText = MediaItems.Count == 1 ? "1 Medium gespeichert" : $"{MediaItems.Count} Medien gespeichert";
	}

	static string SensorLabel(MediaSensorType type) => type switch
	{
		MediaSensorType.BlackMark => "Schwarzmarke",
		MediaSensorType.Continuous => "Endlos",
		_ => "Lücke",
	};

	// ---------- Editor ----------

	[RelayCommand]
	void AddNew() => StartEditing(new PrintMedia(), isNew: true, title: "Neues Medium");

	[RelayCommand]
	void Edit(MediaListItem item) => StartEditing(item.Media, isNew: false, title: "Medium bearbeiten");

	void StartEditing(PrintMedia media, bool isNew, string title)
	{
		_editing = media;
		_isNew = isNew;

		EditorTitle = title;
		EditName = media.Name;
		EditWidthText = media.WidthMm.ToString("0.#");
		EditHeightText = media.HeightMm.ToString("0.#");
		EditGapText = media.GapMm.ToString("0.#");
		SensorIndex = (int)media.SensorType;
		EditMaterial = media.Material;
		CanDelete = !isNew;
		IsEditing = true;
	}

	[RelayCommand]
	async Task SaveEditAsync()
	{
		if (_editing is null)
			return;

		if (string.IsNullOrWhiteSpace(EditName))
		{
			await _alertService.ShowAsync("Ungültig", "Bitte einen Namen eingeben.", "OK");
			return;
		}

		_editing.Name = EditName.Trim();
		if (double.TryParse(EditWidthText, out double width))
			_editing.WidthMm = width;
		if (double.TryParse(EditHeightText, out double height))
			_editing.HeightMm = height;
		if (double.TryParse(EditGapText, out double gap))
			_editing.GapMm = gap;
		_editing.SensorType = (MediaSensorType)Math.Max(0, SensorIndex);
		_editing.Material = EditMaterial?.Trim() ?? string.Empty;

		await _mediaStore.SaveAsync(_editing);

		CloseEditor();
		await RefreshAsync();
	}

	[RelayCommand]
	async Task DeleteAsync()
	{
		if (_editing is null || _isNew)
		{
			CloseEditor();
			return;
		}

		bool confirmed = await _alertService.ConfirmAsync("Medium löschen", $"Medium \"{_editing.Name}\" wirklich löschen?", "Löschen", "Abbrechen");
		if (!confirmed)
			return;

		await _mediaStore.DeleteAsync(_editing.Id);
		CloseEditor();
		await RefreshAsync();
	}

	[RelayCommand]
	void CancelEdit() => CloseEditor();

	void CloseEditor()
	{
		IsEditing = false;
		_editing = null;
	}

	// ---------- Medienerkennung vom Drucker ----------

	[RelayCommand]
	async Task DetectAsync()
	{
		var settings = _settingsStore.Load();
		if (string.IsNullOrWhiteSpace(settings.IpAddress))
		{
			await _alertService.ShowAsync("Kein Drucker", "Bitte zuerst unter „Drucker-Einstellungen“ die IP-Adresse eintragen.", "OK");
			return;
		}

		IsBusy = true;
		var status = await _printerService.GetDetailedStatusAsync(settings.IpAddress, settings.Port);
		IsBusy = false;

		if (!status.Success)
		{
			DetectResult = $"Fehler: {status.ErrorMessage}";
			return;
		}

		var parts = new List<string>();
		if (status.LabelLengthDots is int dots)
			parts.Add($"Etikettenlänge: {ZplLabelBuilder.DotsToMm(dots, settings.Dpi):0.#} mm");
		if (status.PaperOut is bool paperOut)
			parts.Add(paperOut ? "Kein Papier!" : "Papier OK");
		if (status.RibbonOut is bool ribbonOut)
			parts.Add(ribbonOut ? "Kein Farbband!" : "Farbband OK");
		if (status.HeadOpen is bool headOpen)
			parts.Add(headOpen ? "Druckkopf offen!" : "Druckkopf geschlossen");

		DetectResult = parts.Count > 0
			? string.Join("  •  ", parts) + "  (Breite bitte manuell eintragen.)"
			: "Keine auswertbaren Felder in der Statusantwort.";

		if (status.LabelLengthDots is int lengthDots)
		{
			StartEditing(
				new PrintMedia { HeightMm = Math.Round(ZplLabelBuilder.DotsToMm(lengthDots, settings.Dpi), 1) },
				isNew: true,
				title: "Erkanntes Medium");
		}
	}
}
