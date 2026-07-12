namespace LabelPrinting.Models;

/// <summary>Eine gespeicherte Label-Vorlage: Maße plus die frei positionierten Elemente darauf.</summary>
public class LabelTemplate
{
	public string Name { get; set; } = "Neue Vorlage";
	public double WidthMm { get; set; } = 100;
	public double HeightMm { get; set; } = 150;
	public int Dpi { get; set; } = 203;
	public List<LabelElement> Elements { get; set; } = [];

	/// <summary>Platzhalter, die von Elementen dieser Vorlage referenziert werden können (Key/Typ/Pflicht/Default).</summary>
	public List<PlaceholderDefinition> Placeholders { get; set; } = [];
}
