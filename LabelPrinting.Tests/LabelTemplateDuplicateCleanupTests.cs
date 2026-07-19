using System.Text.Json;
using LabelPrinting.Models;
using LabelPrinting.Services;
using Xunit;

namespace LabelPrinting.Tests;

/// <summary>
/// BUG-02: „Speichern unter neuem Namen" darf keine Alt-Datei mit derselben Id hinterlassen.
/// Getestet wird die pure Bereinigungslogik gegen ein temporäres Verzeichnis; der MAUI-Store
/// selbst ruft sie nach jedem Speichern auf.
/// </summary>
public class LabelTemplateDuplicateCleanupTests : IDisposable
{
	readonly string _dir = Path.Combine(Path.GetTempPath(), "LabelTemplateDuplicateCleanupTests_" + Guid.NewGuid().ToString("N"));

	public LabelTemplateDuplicateCleanupTests() => Directory.CreateDirectory(_dir);

	public void Dispose()
	{
		try { Directory.Delete(_dir, recursive: true); } catch { /* Temp-Aufräumen darf nie einen Test brechen */ }
	}

	string WriteTemplateFile(string fileName, LabelTemplate template)
	{
		string path = Path.Combine(_dir, fileName);
		File.WriteAllText(path, JsonSerializer.Serialize(template));
		return path;
	}

	[Fact]
	public void LoeschtAltDateiMitGleicherId()
	{
		var template = new LabelTemplate { Name = "Neu" };
		string oldPath = WriteTemplateFile("Alt.json", template);
		string newPath = WriteTemplateFile("Neu.json", template);

		var deleted = LabelTemplateDuplicateCleanup.DeleteOtherFilesWithSameId(_dir, newPath, template.Id);

		Assert.Equal([oldPath], deleted);
		Assert.False(File.Exists(oldPath));
		Assert.True(File.Exists(newPath));
	}

	[Fact]
	public void BehaeltDateienMitAndererId()
	{
		var saved = new LabelTemplate { Name = "A" };
		var other = new LabelTemplate { Name = "B" };
		string savedPath = WriteTemplateFile("A.json", saved);
		string otherPath = WriteTemplateFile("B.json", other);

		var deleted = LabelTemplateDuplicateCleanup.DeleteOtherFilesWithSameId(_dir, savedPath, saved.Id);

		Assert.Empty(deleted);
		Assert.True(File.Exists(otherPath));
	}

	[Fact]
	public void UeberspringtKaputteDateienUndSolcheOhneId()
	{
		var template = new LabelTemplate();
		string keepPath = WriteTemplateFile("Keep.json", template);
		string invalidPath = Path.Combine(_dir, "Kaputt.json");
		File.WriteAllText(invalidPath, "{ kein gueltiges json");
		string noIdPath = Path.Combine(_dir, "OhneId.json");
		File.WriteAllText(noIdPath, """{"Name":"Alt"}""");

		var deleted = LabelTemplateDuplicateCleanup.DeleteOtherFilesWithSameId(_dir, keepPath, template.Id);

		Assert.Empty(deleted);
		Assert.True(File.Exists(invalidPath));
		Assert.True(File.Exists(noIdPath));
	}

	[Fact]
	public void LeereIdLoeschtNichts()
	{
		var template = new LabelTemplate();
		string keepPath = WriteTemplateFile("Keep.json", template);
		string otherPath = WriteTemplateFile("Anderer.json", new LabelTemplate());

		var deleted = LabelTemplateDuplicateCleanup.DeleteOtherFilesWithSameId(_dir, keepPath, string.Empty);

		Assert.Empty(deleted);
		Assert.True(File.Exists(otherPath));
	}
}
