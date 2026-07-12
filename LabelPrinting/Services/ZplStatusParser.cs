namespace LabelPrinting.Services;

/// <summary>
/// Parst die Antwort der ZPL "~HS"-Statusabfrage (drei durch STX/ETX/CRLF getrennte Zeilen mit
/// kommaseparierten Feldern). Feldpositionen folgen dem Zebra ZPL II Programming Guide; da manche
/// Firmwares/Emulationen leicht abweichen können, werden bewusst nur die am breitesten dokumentierten
/// Felder ausgewertet (siehe auch die Einschränkung dazu in der README).
/// </summary>
public static class ZplStatusParser
{
	const char Stx = (char)0x02;
	const char Etx = (char)0x03;

	public static PrinterStatus Parse(string raw)
	{
		var lines = raw
			.Split(['\r', '\n'])
			.Select(l => l.Trim(Stx, Etx, ' '))
			.Where(l => l.Length > 0)
			.ToList();

		if (lines.Count == 0)
			return new PrinterStatus { Success = false, ErrorMessage = "Keine Statusantwort erhalten.", RawResponse = raw };

		var line1 = lines[0].Split(',');
		var line2 = lines.Count > 1 ? lines[1].Split(',') : [];

		return new PrinterStatus
		{
			Success = true,
			RawResponse = raw,
			PaperOut = ReadBool(line1, 1),
			Paused = ReadBool(line1, 2),
			LabelLengthDots = ReadInt(line1, 3),
			HeadOpen = ReadBool(line2, 2),
			RibbonOut = ReadBool(line2, 3),
		};
	}

	static bool? ReadBool(string[] fields, int index) =>
		index < fields.Length && int.TryParse(fields[index], out int value) ? value != 0 : null;

	static int? ReadInt(string[] fields, int index) =>
		index < fields.Length && int.TryParse(fields[index], out int value) ? value : null;
}
