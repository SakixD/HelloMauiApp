using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>Persistenz für Druckmedien-Presets. Als Interface, damit Konsumenten/Tests eine eigene Implementierung (z.B. In-Memory) einsetzen können.</summary>
public interface IPrintMediaStore
{
	Task<List<PrintMedia>> ListAsync();

	Task<PrintMedia?> LoadAsync(string id);

	Task SaveAsync(PrintMedia media);

	Task DeleteAsync(string id);
}
