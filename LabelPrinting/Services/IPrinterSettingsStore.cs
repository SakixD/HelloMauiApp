using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>Persistenz für die Druckerkonfiguration (IP, Port, Labelgröße, DPI). Als Interface, damit Konsumenten/Tests eine eigene Implementierung (z.B. In-Memory) einsetzen können.</summary>
public interface IPrinterSettingsStore
{
	PrinterSettings Load();

	void Save(PrinterSettings settings);
}
