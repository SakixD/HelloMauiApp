using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>Persistenz für Label-Vorlagen. Als Interface, damit Konsumenten/Tests eine eigene Implementierung (z.B. In-Memory) einsetzen können.</summary>
public interface ILabelTemplateStore
{
	Task<List<string>> ListTemplateNamesAsync();

	Task<LabelTemplate?> LoadAsync(string name);

	Task SaveAsync(LabelTemplate template);

	Task DeleteAsync(string name);
}
