using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>Persistenz für die Druckerkonfiguration (IP, Port, Labelgröße, DPI). Als Interface, damit Konsumenten/Tests eine eigene Implementierung (z.B. In-Memory) einsetzen können.</summary>
[Obsolete("Ersetzt durch PrinterProfile/IPrinterProfileStore; wird nur noch für die einmalige Migration gelesen.")]
public interface IPrinterSettingsStore
{
	PrinterSettings Load();

	void Save(PrinterSettings settings);
}
