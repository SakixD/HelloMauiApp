using System.Text.Json;

namespace LabelPrinting.Services;

/// <summary>
/// Bereinigt Vorlagen-Duplikate nach „Speichern unter neuem Namen": Der Store speichert
/// dateibasiert pro Name, die <c>Id</c> einer Vorlage ist laut Modell aber eindeutig. Ohne
/// Bereinigung trüge die Datei des alten Namens dieselbe Id weiter (stilles Kopieren statt
/// Umbenennen). Bewusst ohne MAUI-Abhängigkeit, damit die Logik im Testprojekt per
/// Compile-Include testbar bleibt (siehe LabelPrinting.Tests.csproj).
/// </summary>
public static class LabelTemplateDuplicateCleanup
{
	/// <summary>
	/// Löscht alle *.json-Dateien im Verzeichnis, die dieselbe Vorlagen-Id wie
	/// <paramref name="id"/> tragen — außer <paramref name="keepFilePath"/> (der gerade
	/// gespeicherten Datei). Nicht lesbare oder ungültige Dateien werden übersprungen,
	/// damit ein einzelnes kaputtes Vorlagen-File nie das Speichern stört.
	/// Liefert die gelöschten Dateipfade zurück.
	/// </summary>
	public static List<string> DeleteOtherFilesWithSameId(string directory, string keepFilePath, string id)
	{
		var deleted = new List<string>();
		if (string.IsNullOrEmpty(id) || !Directory.Exists(directory))
			return deleted;

		string keepFull = Path.GetFullPath(keepFilePath);
		foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
		{
			if (string.Equals(Path.GetFullPath(file), keepFull, StringComparison.OrdinalIgnoreCase))
				continue;

			if (TryReadTemplateId(file) == id)
			{
				File.Delete(file);
				deleted.Add(file);
			}
		}

		return deleted;
	}

	/// <summary>Liest nur die „Id"-Eigenschaft einer Vorlagen-Datei; null bei fehlender/ungültiger Datei.</summary>
	static string? TryReadTemplateId(string filePath)
	{
		try
		{
			using var stream = File.OpenRead(filePath);
			using var doc = JsonDocument.Parse(stream);
			return doc.RootElement.ValueKind == JsonValueKind.Object
				&& doc.RootElement.TryGetProperty("Id", out var idProperty)
				&& idProperty.ValueKind == JsonValueKind.String
				? idProperty.GetString()
				: null;
		}
		catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
		{
			return null;
		}
	}
}
