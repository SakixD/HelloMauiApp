namespace LabelPrinting.Models;

/// <summary>
/// Validierung und Normalisierung von Rollen-Kennungen im Format <c>Bereich.Rolle</c>
/// (z.B. "Versand.PaketLabel", "Produktion.Produktetikett"). Bewusster Vorgriff auf die
/// DeviceRole-Schicht aus ROADMAP Phase 3a: Hier lebt nur die strukturierte Kennung
/// (Leitplanke aus PROJECT.md — Rolle ist kein loser String), keine Auflösungslogik.
/// Die spätere Schicht übernimmt die so validierten Daten unverändert.
/// </summary>
public static class DeviceRoleName
{
	/// <summary>
	/// Prüft und normalisiert eine einzelne Rollen-Kennung: getrimmt, genau ein Punkt,
	/// beide Teile nicht leer und ohne Leerraum. Liefert false bei ungültigem Format.
	/// </summary>
	public static bool TryNormalize(string? input, out string normalized)
	{
		normalized = string.Empty;
		if (string.IsNullOrWhiteSpace(input))
			return false;

		string candidate = input.Trim();
		string[] parts = candidate.Split('.');
		if (parts.Length != 2 || parts.Any(p => p.Length == 0 || p.Any(char.IsWhiteSpace)))
			return false;

		normalized = candidate;
		return true;
	}

	/// <summary>
	/// Parst eine komma-/semikolon-/zeilengetrennte Eingabe (z.B. aus einem Editor-Feld) zu
	/// einer Rollenliste. Duplikate werden entfernt (Groß-/Kleinschreibung ignoriert, erste
	/// Schreibweise gewinnt), die Eingabereihenfolge bleibt erhalten. Ungültige Einträge
	/// landen unverändert in <paramref name="invalid"/>, damit der Aufrufer sie melden kann.
	/// </summary>
	public static List<string> ParseList(string? input, out List<string> invalid)
	{
		var roles = new List<string>();
		invalid = [];
		if (string.IsNullOrWhiteSpace(input))
			return roles;

		foreach (string raw in input.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!TryNormalize(raw, out string role))
				invalid.Add(raw);
			else if (!roles.Contains(role, StringComparer.OrdinalIgnoreCase))
				roles.Add(role);
		}

		return roles;
	}
}
