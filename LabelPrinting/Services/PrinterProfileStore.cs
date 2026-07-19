using System.Text.Json;
using System.Text.Json.Serialization;
using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>
/// Verwaltet die Druckerprofil-Liste als einen JSON-Blob in <see cref="Preferences"/> (wie die
/// übrigen Stores bewusst MAUI-Preferences – die SDK-Entkopplung ist zurückgestellt).
/// Beim ersten Zugriff werden die Legacy-Einzeldrucker-Werte aus <see cref="IPrinterSettingsStore"/>
/// einmalig in ein Default-Profil migriert (siehe <see cref="MigrateIfNeeded"/>).
/// </summary>
public class PrinterProfileStore : IPrinterProfileStore
{
	const string ProfilesKey = "printer_profiles";
	const string MigratedFlagKey = "printer_profiles_migrated_v1";

	// Enums als Namen statt Zahlen speichern: lesbarer und robust gegen spätere Enum-Umsortierung.
	static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

	// Instanz-Lock reicht: Seit CLEAN-02 gibt es nur noch die eine DI-Singleton-Instanz
	// (einzige Erzeugung: MauiProgram-Factory). Der Lock serialisiert das
	// Lesen-Ändern-Schreiben auf dem einen JSON-Blob in Preferences.
	readonly object _syncRoot = new();

	// Die Migration ist der eine legitime Nutzer der als obsolet markierten Legacy-Typen.
#pragma warning disable CS0618
	readonly IPrinterSettingsStore _legacyStore;

	/// <param name="legacyStore">Quelle der einmaligen Migration (Standard: <see cref="PrinterSettingsStore"/>). Austauschbar für Tests.</param>
	public PrinterProfileStore(IPrinterSettingsStore? legacyStore = null)
	{
		_legacyStore = legacyStore ?? new PrinterSettingsStore();
	}
#pragma warning restore CS0618

	public IReadOnlyList<PrinterProfile> GetAll()
	{
		lock (_syncRoot)
		{
			MigrateIfNeeded();
			return LoadList();
		}
	}

	public PrinterProfile? GetDefault()
	{
		lock (_syncRoot)
		{
			MigrateIfNeeded();
			return LoadList().FirstOrDefault(p => p.IsDefault);
		}
	}

	public PrinterProfile? GetById(Guid id)
	{
		lock (_syncRoot)
		{
			MigrateIfNeeded();
			return LoadList().FirstOrDefault(p => p.Id == id);
		}
	}

	public void Save(PrinterProfile profile)
	{
		lock (_syncRoot)
		{
			MigrateIfNeeded();
			var list = LoadList();

			int index = list.FindIndex(p => p.Id == profile.Id);
			if (index >= 0)
				list[index] = profile;
			else
				list.Add(profile);

			NormalizeDefault(list, preferredDefaultId: profile.IsDefault ? profile.Id : null);
			SaveList(list);
		}
	}

	public void Delete(Guid id)
	{
		lock (_syncRoot)
		{
			MigrateIfNeeded();
			var list = LoadList();
			list.RemoveAll(p => p.Id == id);
			NormalizeDefault(list, preferredDefaultId: null);
			SaveList(list);
		}
	}

	public void SetDefault(Guid id)
	{
		lock (_syncRoot)
		{
			MigrateIfNeeded();
			var list = LoadList();
			if (!list.Any(p => p.Id == id))
				return;

			NormalizeDefault(list, preferredDefaultId: id);
			SaveList(list);
		}
	}

	/// <summary>
	/// Erzwingt die Invariante "höchstens ein Default; genau eines, sobald die Liste nicht leer ist".
	/// Ohne diese Regel gäbe es nach dem Löschen des Default-Profils keinen aktiven Drucker mehr,
	/// obwohl weitere Profile existieren.
	/// </summary>
	static void NormalizeDefault(List<PrinterProfile> list, Guid? preferredDefaultId)
	{
		if (list.Count == 0)
			return;

		var target = (preferredDefaultId is { } id ? list.FirstOrDefault(p => p.Id == id) : null)
			?? list.FirstOrDefault(p => p.IsDefault)
			?? list[0];

		foreach (var profile in list)
			profile.IsDefault = profile.Id == target.Id;
	}

	/// <summary>
	/// Einmalige Übernahme der Legacy-Einzeldrucker-Konfiguration: Ist eine IP hinterlegt, entsteht
	/// daraus genau ein TCP-Default-Profil "Standarddrucker". Das Migrations-Flag wird auch dann
	/// gesetzt, wenn nichts zu migrieren war – sonst würde ein später gelöschtes Profil beim
	/// nächsten Start aus den (absichtlich nicht gelöschten) Legacy-Keys wieder neu entstehen.
	/// </summary>
	void MigrateIfNeeded()
	{
		if (Preferences.Default.Get(MigratedFlagKey, false))
			return;

		var legacy = _legacyStore.Load();
		if (!string.IsNullOrWhiteSpace(legacy.IpAddress))
		{
			var list = LoadList();
			list.Add(new PrinterProfile
			{
				Name = "Standarddrucker",
				IsDefault = true,
				ConnectionMode = PrinterConnectionMode.Local,
				TransportKind = PrinterTransportKind.Tcp,
				IpAddress = legacy.IpAddress,
				Port = legacy.Port,
				LabelWidthMm = legacy.LabelWidthMm,
				LabelHeightMm = legacy.LabelHeightMm,
				Dpi = legacy.Dpi,
			});
			NormalizeDefault(list, preferredDefaultId: null);
			SaveList(list);
		}

		Preferences.Default.Set(MigratedFlagKey, true);
	}

	static List<PrinterProfile> LoadList()
	{
		string json = Preferences.Default.Get(ProfilesKey, string.Empty);
		if (string.IsNullOrWhiteSpace(json))
			return [];

		try
		{
			return JsonSerializer.Deserialize<List<PrinterProfile>>(json, JsonOptions) ?? [];
		}
		catch (JsonException)
		{
			// Ein defekter Blob (z.B. aus einer künftigen App-Version) darf die App nicht crashen –
			// leere Liste heißt: Nutzer legt Profile neu an, Legacy-Keys existieren als Sicherheitsnetz weiter.
			return [];
		}
	}

	static void SaveList(List<PrinterProfile> list)
		=> Preferences.Default.Set(ProfilesKey, JsonSerializer.Serialize(list, JsonOptions));
}
