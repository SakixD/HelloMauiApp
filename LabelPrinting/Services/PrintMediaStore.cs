using System.Text.Json;
using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>
/// Speichert/lädt Druckmedien-Presets als JSON-Dateien im App-Datenverzeichnis, benannt nach der
/// stabilen <see cref="PrintMedia.Id"/> (nicht nach dem änderbaren Namen), damit Vorlagen ein Medium
/// dauerhaft per Id referenzieren können.
/// </summary>
public class PrintMediaStore : IPrintMediaStore
{
	static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	static string MediaDirectory
	{
		get
		{
			string dir = Path.Combine(FileSystem.Current.AppDataDirectory, "PrintMedia");
			Directory.CreateDirectory(dir);
			return dir;
		}
	}

	static string PathFor(string id) => Path.Combine(MediaDirectory, id + ".json");

	/// <summary>Lädt alle gespeicherten Medien, sortiert nach Name. Beschädigte Dateien werden übersprungen.</summary>
	public async Task<List<PrintMedia>> ListAsync()
	{
		var result = new List<PrintMedia>();

		foreach (var file in Directory.EnumerateFiles(MediaDirectory, "*.json"))
		{
			var media = await LoadFileAsync(file).ConfigureAwait(false);
			if (media is not null)
				result.Add(media);
		}

		return result.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
	}

	public Task<PrintMedia?> LoadAsync(string id) => LoadFileAsync(PathFor(id));

	static async Task<PrintMedia?> LoadFileAsync(string path)
	{
		if (!File.Exists(path))
			return null;

		try
		{
			await using var stream = File.OpenRead(path);
			return await JsonSerializer.DeserializeAsync<PrintMedia>(stream, JsonOptions).ConfigureAwait(false);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public async Task SaveAsync(PrintMedia media)
	{
		await using var stream = File.Create(PathFor(media.Id));
		await JsonSerializer.SerializeAsync(stream, media, JsonOptions).ConfigureAwait(false);
	}

	public Task DeleteAsync(string id)
	{
		string path = PathFor(id);
		if (File.Exists(path))
			File.Delete(path);

		return Task.CompletedTask;
	}
}
