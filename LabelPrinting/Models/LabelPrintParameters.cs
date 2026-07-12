namespace LabelPrinting.Models;

/// <summary>
/// Pro Vorlage hinterlegte Druckeinstellungen, die beim Rendern automatisch als ZPL-Befehle
/// angewendet werden. Alle Felder sind optional (null = Druckerstandard/zuletzt eingestellter Wert
/// verwenden) – eine Vorlage ohne Parameter verhält sich exakt wie zuvor.
/// </summary>
public class LabelPrintParameters
{
	/// <summary>Id des bevorzugten Mediums (<see cref="PrintMedia.Id"/>), sofern eines zugeordnet ist.</summary>
	public string? PreferredMediaId { get; set; }

	/// <summary>Druckgeschwindigkeit in ips (Zoll/Sekunde). Null = Druckerstandard verwenden.</summary>
	public int? PrintSpeed { get; set; }

	/// <summary>Druckdichte/Darkness, gültiger Bereich 0-30. Null = Druckerstandard verwenden.</summary>
	public int? Darkness { get; set; }
}
