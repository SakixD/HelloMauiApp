using System.Text.Json;
using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>Speichert/lädt Label-Vorlagen als JSON-Dateien im App-Datenverzeichnis.</summary>
public class LabelTemplateStore : ILabelTemplateStore
{
	static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	static string TemplatesDirectory
	{
		get
		{
			string dir = Path.Combine(FileSystem.Current.AppDataDirectory, "LabelTemplates");
			Directory.CreateDirectory(dir);
			return dir;
		}
	}

	static string PathFor(string name) => Path.Combine(TemplatesDirectory, SanitizeFileName(name) + ".json");

	/// <summary>
	/// Macht einen Vorlagennamen dateisystemtauglich (ersetzt ungültige Zeichen durch "_").
	/// Öffentlich, damit Aufrufer (z.B. Export-Funktionen im UI) denselben Dateinamen erzeugen
	/// wie der Store selbst, ohne die Logik zu duplizieren.
	/// </summary>
	public static string SanitizeFileName(string name)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
		string result = new string(chars).Trim();
		return string.IsNullOrEmpty(result) ? "Vorlage" : result;
	}

	public Task<List<string>> ListTemplateNamesAsync()
	{
		var names = Directory.EnumerateFiles(TemplatesDirectory, "*.json")
			.Select(Path.GetFileNameWithoutExtension)
			.Where(n => !string.IsNullOrEmpty(n))
			.Select(n => n!)
			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
			.ToList();

		return Task.FromResult(names);
	}

	/// <summary>
	/// Lädt eine Vorlage. Gibt null zurück, wenn die Datei fehlt ODER ihr JSON nicht mehr zum
	/// aktuellen Format passt (z.B. aus einer älteren App-Version) – wirft dafür bewusst keine
	/// Exception, damit ein einzelnes veraltetes Vorlagen-File nie die App zum Absturz bringt.
	/// </summary>
	public async Task<LabelTemplate?> LoadAsync(string name)
	{
		string path = PathFor(name);
		if (!File.Exists(path))
			return null;

		try
		{
			string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
			var template = JsonSerializer.Deserialize<LabelTemplate>(json, JsonOptions);
			if (template is null)
				return null;

			// Vorlagen aus einer Zeit vor Einführung der Id hätten sonst bei jedem Laden eine neue,
			// instabile Id (Default-Wert des Konstruktors) – stattdessen einmalig zuweisen und sofort
			// zurückschreiben, damit sie ab jetzt stabil bleibt.
			if (!json.Contains("\"Id\"", StringComparison.Ordinal))
				await SaveAsync(template).ConfigureAwait(false);

			return template;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public async Task SaveAsync(LabelTemplate template)
	{
		await using var stream = File.Create(PathFor(template.Name));
		await JsonSerializer.SerializeAsync(stream, template, JsonOptions).ConfigureAwait(false);
	}

	public Task DeleteAsync(string name)
	{
		string path = PathFor(name);
		if (File.Exists(path))
			File.Delete(path);

		return Task.CompletedTask;
	}
}
