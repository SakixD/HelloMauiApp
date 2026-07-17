using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>
/// Persistenz für die Liste der Druckerprofile. Ersetzt <see cref="IPrinterSettingsStore"/>
/// (Einzeldrucker) als führende Konfigurationsquelle; die Legacy-Werte werden einmalig migriert.
/// Es gibt höchstens ein Default-Profil – das ist der app-weit aktive Drucker.
/// </summary>
public interface IPrinterProfileStore
{
	IReadOnlyList<PrinterProfile> GetAll();

	/// <summary>Das aktive Profil, oder null wenn noch keines angelegt wurde.</summary>
	PrinterProfile? GetDefault();

	PrinterProfile? GetById(Guid id);

	/// <summary>Fügt ein Profil hinzu oder aktualisiert es (Zuordnung über <see cref="PrinterProfile.Id"/>).</summary>
	void Save(PrinterProfile profile);

	void Delete(Guid id);

	/// <summary>Macht genau dieses Profil zum Default (alle anderen verlieren das Flag).</summary>
	void SetDefault(Guid id);
}
