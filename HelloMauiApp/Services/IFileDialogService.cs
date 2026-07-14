namespace HelloMauiApp.Services;

/// <summary>Eine vom Nutzer ausgewählte Datei (Name + kompletter Inhalt).</summary>
public record PickedFile(string FileName, byte[] Bytes);

/// <summary>
/// Abstrahiert Datei-Dialoge (Öffnen/Speichern unter/Teilen), damit ViewModels sie ohne
/// Plattform-Interop (FilePicker, WinRT FileSavePicker, Share) nutzen können – analog zu
/// <see cref="IAlertService"/>.
/// </summary>
public interface IFileDialogService
{
	/// <summary>Bild auswählen (Galerie/Dateisystem). null = abgebrochen.</summary>
	Task<PickedFile?> PickImageAsync(string title);

	/// <summary>JSON-Datei auswählen. null = abgebrochen.</summary>
	Task<PickedFile?> PickJsonAsync(string title);

	/// <summary>
	/// Text als Datei speichern. Unter Windows über den System-"Speichern unter"-Dialog
	/// (Rückgabe: gewählter Pfad), auf anderen Plattformen als Teilen-Fallback (Rückgabe: null,
	/// ebenso bei Abbruch).
	/// </summary>
	Task<string?> SaveTextFileAsAsync(string suggestedFileName, string fileTypeDescription, string fileExtension, string content);

	/// <summary>Text als Datei über das System-Teilen-Blatt weitergeben.</summary>
	Task ShareTextFileAsync(string title, string fileName, string content);
}
