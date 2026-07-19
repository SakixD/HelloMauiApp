using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloMauiApp.Services;
using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp.ViewModels;

/// <summary>Zeile der Profilliste (Anzeige-Daten vorformatiert, damit die View nur bindet).</summary>
public record PrinterProfileListItem(PrinterProfile Profile, string Name, string Details, string TransportLabel, bool IsDefault)
{
	/// <summary>Für die "Als Standard"-Schaltfläche (XAML hat keinen Invert-Konverter im Projekt).</summary>
	public bool IsNotDefault => !IsDefault;
}

/// <summary>
/// ViewModel der Druckerprofil-Verwaltung (Drill-down aus den Einstellungen). Liste + Editor auf
/// einer Seite (Muster der Medien-Bibliothek). Ersetzt die frühere PrinterSettingsPage mit ihrem
/// einen globalen Drucker: Profile sind beliebig viele, das Default-Profil ist der app-weit aktive
/// Drucker. USB/Bluetooth sind wählbar, aber als "noch nicht implementiert" gekennzeichnet
/// (Architektur-Stubs im SDK); Remote wird erst mit dem Server wählbar.
/// </summary>
public partial class PrinterProfilesViewModel : ViewModelBase
{
	readonly IPrinterProfileStore _profileStore;
	readonly IPrinterService _printerService;
	readonly IPrintMediaStore _mediaStore;
	readonly IAlertService _alertService;

	PrinterProfile? _editing;
	List<PrintMedia> _mediaList = [];
	bool _suppressMediaSelection;

	[ObservableProperty] string countText = string.Empty;
	[ObservableProperty] List<PrinterProfileListItem> profileItems = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNoProfiles))]
	bool hasProfiles;

	public bool HasNoProfiles => !HasProfiles;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsNotEditing))]
	bool isEditing;

	public bool IsNotEditing => !IsEditing;

	[ObservableProperty] string editorTitle = string.Empty;
	[ObservableProperty] string editName = string.Empty;
	[ObservableProperty] int transportIndex;
	[ObservableProperty] bool isTcp = true;
	[ObservableProperty] bool isTransportStub;
	[ObservableProperty] string editIp = string.Empty;
	[ObservableProperty] string editPortText = "9100";
	[ObservableProperty] string editWidthText = string.Empty;
	[ObservableProperty] string editHeightText = string.Empty;
	[ObservableProperty] string editDpiText = string.Empty;
	[ObservableProperty] bool canDelete;
	[ObservableProperty] string testResult = string.Empty;
	[ObservableProperty] List<string> mediaOptions = [];
	[ObservableProperty] int selectedMediaIndex = -1;

	public PrinterProfilesViewModel(
		IPrinterProfileStore profileStore,
		IPrinterService printerService,
		IPrintMediaStore mediaStore,
		IAlertService alertService)
	{
		_profileStore = profileStore;
		_printerService = printerService;
		_mediaStore = mediaStore;
		_alertService = alertService;
	}

	public async Task RefreshAsync()
	{
		var profiles = _profileStore.GetAll();

		ProfileItems = profiles.Select(p => new PrinterProfileListItem(
			p,
			p.Name,
			$"{p.ConnectionSummary} · {p.LabelWidthMm:0.#}×{p.LabelHeightMm:0.#} mm · {p.Dpi} dpi",
			p.TransportKind switch
			{
				PrinterTransportKind.Tcp => "Netzwerk",
				PrinterTransportKind.Usb => "USB",
				PrinterTransportKind.Bluetooth => "Bluetooth",
				_ => p.TransportKind.ToString(),
			},
			p.IsDefault)).ToList();

		HasProfiles = ProfileItems.Count > 0;
		CountText = ProfileItems.Count == 1 ? "1 Profil" : $"{ProfileItems.Count} Profile";

		_mediaList = await _mediaStore.ListAsync();
		_suppressMediaSelection = true;
		MediaOptions = _mediaList.Select(m => $"{m.Name} ({m.WidthMm:0.#}×{m.HeightMm:0.#} mm)").ToList();
		SelectedMediaIndex = -1;
		_suppressMediaSelection = false;
	}

	partial void OnTransportIndexChanged(int value)
	{
		IsTcp = value == 0;
		IsTransportStub = value != 0;
	}

	partial void OnSelectedMediaIndexChanged(int value)
	{
		if (_suppressMediaSelection || value < 0 || value >= _mediaList.Count)
			return;

		EditWidthText = _mediaList[value].WidthMm.ToString("0.##");
		EditHeightText = _mediaList[value].HeightMm.ToString("0.##");
	}

	[RelayCommand]
	void AddNew()
	{
		_editing = null;
		var defaults = new PrinterProfile();
		EditorTitle = "Neues Druckerprofil";
		EditName = string.Empty;
		TransportIndex = 0;
		EditIp = string.Empty;
		EditPortText = defaults.Port.ToString();
		EditWidthText = defaults.LabelWidthMm.ToString("0.##");
		EditHeightText = defaults.LabelHeightMm.ToString("0.##");
		EditDpiText = defaults.Dpi.ToString();
		CanDelete = false;
		TestResult = string.Empty;
		IsEditing = true;
	}

	[RelayCommand]
	void Edit(PrinterProfileListItem item)
	{
		var profile = item.Profile;
		_editing = profile;
		EditorTitle = $"Profil: {profile.Name}";
		EditName = profile.Name;
		TransportIndex = profile.TransportKind switch
		{
			PrinterTransportKind.Usb => 1,
			PrinterTransportKind.Bluetooth => 2,
			_ => 0,
		};
		EditIp = profile.IpAddress;
		EditPortText = profile.Port.ToString();
		EditWidthText = profile.LabelWidthMm.ToString("0.##");
		EditHeightText = profile.LabelHeightMm.ToString("0.##");
		EditDpiText = profile.Dpi.ToString();
		CanDelete = true;
		TestResult = string.Empty;
		IsEditing = true;
	}

	[RelayCommand]
	async Task SetDefaultAsync(PrinterProfileListItem item)
	{
		_profileStore.SetDefault(item.Profile.Id);
		await RefreshAsync();
	}

	[RelayCommand]
	async Task SaveEditAsync()
	{
		if (TryReadEditor(out var profile, out string? error))
		{
			_profileStore.Save(profile);
			CloseEditor();
			await RefreshAsync();
		}
		else
		{
			await _alertService.ShowAsync("Ungültige Eingabe", error!, "OK");
		}
	}

	[RelayCommand]
	async Task DeleteAsync()
	{
		if (_editing is not { } profile)
			return;

		bool confirmed = await _alertService.ConfirmAsync(
			"Profil löschen",
			$"Druckerprofil „{profile.Name}“ wirklich löschen?",
			"Ja, löschen",
			"Abbrechen");
		if (!confirmed)
			return;

		_profileStore.Delete(profile.Id);
		CloseEditor();
		await RefreshAsync();
	}

	[RelayCommand]
	void CancelEdit() => CloseEditor();

	/// <summary>Testet die Erreichbarkeit mit den aktuellen Formularwerten (ohne zu speichern).</summary>
	[RelayCommand]
	async Task TestConnectionAsync()
	{
		if (!TryReadEditor(out var profile, out string? error))
		{
			await _alertService.ShowAsync("Ungültige Eingabe", error!, "OK");
			return;
		}

		IsBusy = true;
		TestResult = "Teste Verbindung...";
		var result = await _printerService.TestConnectionAsync(profile);
		IsBusy = false;

		TestResult = result.Success
			? $"Verbindung zu {profile.ConnectionSummary} erfolgreich."
			: result.ErrorMessage ?? "Unbekannter Fehler";
	}

	void CloseEditor()
	{
		IsEditing = false;
		_editing = null;
	}

	bool TryReadEditor(out PrinterProfile profile, out string? error)
	{
		// Bestehendes Profil klonen statt neu aufbauen: Felder, die dieses Formular nicht kennt
		// (UsbDeviceId, BluetoothAddress, Remote*, künftige), gingen sonst beim Speichern verloren —
		// z.B. die IP, wenn der Transport kurz auf USB stand (BUG-03). Id/Default bleiben mit erhalten.
		profile = _editing?.Clone() ?? new PrinterProfile { ConnectionMode = PrinterConnectionMode.Local };
		profile.TransportKind = TransportIndex switch
		{
			1 => PrinterTransportKind.Usb,
			2 => PrinterTransportKind.Bluetooth,
			_ => PrinterTransportKind.Tcp,
		};
		error = null;

		if (string.IsNullOrWhiteSpace(EditName))
		{
			error = "Bitte einen Profilnamen eingeben.";
			return false;
		}
		profile.Name = EditName.Trim();

		if (profile.TransportKind == PrinterTransportKind.Tcp)
		{
			if (string.IsNullOrWhiteSpace(EditIp))
			{
				error = "Bitte eine IP-Adresse eingeben.";
				return false;
			}
			if (!int.TryParse(EditPortText, out int port) || port is <= 0 or > 65535)
			{
				error = "Bitte einen gültigen Port (1-65535) eingeben.";
				return false;
			}
			profile.IpAddress = EditIp.Trim();
			profile.Port = port;
		}

		if (!double.TryParse(EditWidthText, out double width) || width <= 0)
		{
			error = "Bitte eine gültige Labelbreite eingeben.";
			return false;
		}
		if (!double.TryParse(EditHeightText, out double height) || height <= 0)
		{
			error = "Bitte eine gültige Labelhöhe eingeben.";
			return false;
		}
		if (!int.TryParse(EditDpiText, out int dpi) || dpi <= 0)
		{
			error = "Bitte eine gültige Auflösung (DPI) eingeben.";
			return false;
		}

		profile.LabelWidthMm = width;
		profile.LabelHeightMm = height;
		profile.Dpi = dpi;
		return true;
	}
}
