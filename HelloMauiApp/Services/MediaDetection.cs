using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp.Services;

/// <summary>
/// Gemeinsame Auswertung der Medienerkennung vom Drucker (~HS-Status): baut aus
/// <see cref="PrinterStatus"/> + Profil-DPI den Anzeigetext und — falls eine kalibrierte
/// Etikettenlänge vorliegt — ein vorbefülltes <see cref="PrintMedia"/>. Genutzt von der
/// Medienverwaltung (Rail „Medien") und dem Designer-Drill-down (Designer → Medium),
/// damit die Logik nicht doppelt gepflegt werden muss.
/// </summary>
public static class MediaDetection
{
	/// <summary>Auswertungsergebnis: Anzeigetext + ggf. vorbefülltes neues Medium (sonst null).</summary>
	public record Result(string SummaryText, PrintMedia? DetectedMedia);

	public static Result Interpret(PrinterStatus status, int dpi)
	{
		if (!status.Success)
			return new Result($"Fehler: {status.ErrorMessage}", null);

		var parts = new List<string>();
		if (status.LabelLengthDots is int dots)
			parts.Add($"Etikettenlänge: {ZplLabelBuilder.DotsToMm(dots, dpi):0.#} mm");
		if (status.PaperOut is bool paperOut)
			parts.Add(paperOut ? "Kein Papier!" : "Papier OK");
		if (status.RibbonOut is bool ribbonOut)
			parts.Add(ribbonOut ? "Kein Farbband!" : "Farbband OK");
		if (status.HeadOpen is bool headOpen)
			parts.Add(headOpen ? "Druckkopf offen!" : "Druckkopf geschlossen");

		string summary = parts.Count > 0
			? string.Join("  •  ", parts) + "  (Breite bitte manuell eintragen.)"
			: "Keine auswertbaren Felder in der Statusantwort.";

		PrintMedia? detected = status.LabelLengthDots is int lengthDots
			? new PrintMedia { HeightMm = Math.Round(ZplLabelBuilder.DotsToMm(lengthDots, dpi), 1) }
			: null;

		return new Result(summary, detected);
	}

	/// <summary>Deutsche Kurzbezeichnung des Sensortyps für Listen und Editoren.</summary>
	public static string SensorLabel(MediaSensorType type) => type switch
	{
		MediaSensorType.BlackMark => "Schwarzmarke",
		MediaSensorType.Continuous => "Endlos",
		_ => "Lücke",
	};
}
