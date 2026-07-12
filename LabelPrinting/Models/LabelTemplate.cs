namespace LabelPrinting.Models;

/// <summary>Eine gespeicherte Label-Vorlage: Maße plus die frei positionierten Elemente darauf.</summary>
public class LabelTemplate
{
	/// <summary>
	/// Stabile, unveränderliche Kennung – im Gegensatz zu <see cref="Name"/> umbenennbar-sicher, damit
	/// andere Module/eine künftige API eine Vorlage dauerhaft referenzieren können. Vorlagen aus
	/// älteren App-Versionen ohne Id bekommen beim ersten Laden automatisch eine zugewiesen
	/// (siehe <see cref="Services.LabelTemplateStore"/>).
	/// </summary>
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	public string Name { get; set; } = "Neue Vorlage";
	public double WidthMm { get; set; } = 100;
	public double HeightMm { get; set; } = 150;
	public int Dpi { get; set; } = 203;
	public List<LabelElement> Elements { get; set; } = [];

	/// <summary>Platzhalter, die von Elementen dieser Vorlage referenziert werden können (Key/Typ/Pflicht/Default).</summary>
	public List<PlaceholderDefinition> Placeholders { get; set; } = [];

	/// <summary>Freie Zusatzinfos zur Organisation/Suche (Beschreibung, Kategorie, Tags).</summary>
	public LabelTemplateMetadata Metadata { get; set; } = new();

	/// <summary>Druckeinstellungen, die beim Rendern dieser Vorlage automatisch angewendet werden.</summary>
	public LabelPrintParameters PrintParameters { get; set; } = new();
}
