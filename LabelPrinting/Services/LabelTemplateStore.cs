using System.Text.Json;
using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>Speichert/lädt Label-Vorlagen als JSON-Dateien im App-Datenverzeichnis.</summary>
public class LabelTemplateStore
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

	static string SanitizeFileName(string name)
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
			await using var stream = File.OpenRead(path);
			return await JsonSerializer.DeserializeAsync<LabelTemplate>(stream, JsonOptions).ConfigureAwait(false);
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
