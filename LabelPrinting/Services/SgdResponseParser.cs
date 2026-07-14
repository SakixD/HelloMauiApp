namespace LabelPrinting.Services;

/// <summary>
/// Parst die Antwort einer ZPL SGD "! U1 getvar"-Abfrage: der Wert kommt in Anführungszeichen,
/// ggf. von STX/ETX umschlossen und CRLF-terminiert zurück (z.B. <c>"192.168.1.50"\r\n</c>).
/// Analog zu <see cref="ZplStatusParser"/>, nur für das einfachere Ein-Wert-Antwortformat von getvar.
/// </summary>
public static class SgdResponseParser
{
	const char Stx = (char)0x02;
	const char Etx = (char)0x03;

	public static string Parse(string raw)
	{
		string trimmed = raw.Trim(Stx, Etx, '\r', '\n', ' ');

		if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
			trimmed = trimmed[1..^1];

		return trimmed;
	}
}
