using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>
/// Interne "Vorlage befüllen"-API (Vorstufe der späteren App-API): prüft Pflichtfelder und wendet
/// Default-Werte an. Wird sowohl vom Test-Modus (manuelles Ausfüllen) als auch von zukünftigen
/// automatisierten Aufrufen genutzt – ein einziger Validierungsweg für beide.
/// </summary>
public static class LabelTemplateFillService
{
	public static TemplateFillResult Fill(LabelTemplate template, IReadOnlyDictionary<string, string> data)
	{
		var resolved = new Dictionary<string, string>(data);
		var missing = new List<string>();

		foreach (var placeholder in template.Placeholders)
		{
			bool hasValue = resolved.TryGetValue(placeholder.Key, out var value) && !string.IsNullOrEmpty(value);
			if (hasValue)
				continue;

			if (!string.IsNullOrEmpty(placeholder.DefaultValue))
				resolved[placeholder.Key] = placeholder.DefaultValue;
			else if (placeholder.Required)
				missing.Add(placeholder.Key);
			else
				resolved[placeholder.Key] = string.Empty;
		}

		return missing.Count > 0 ? TemplateFillResult.Missing(missing) : TemplateFillResult.Ok(resolved);
	}
}
