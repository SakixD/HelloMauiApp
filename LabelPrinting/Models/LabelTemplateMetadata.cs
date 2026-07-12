namespace LabelPrinting.Models;

/// <summary>Freie Zusatzinfos zur Organisation/Suche von Vorlagen, ohne Einfluss auf den Druck.</summary>
public class LabelTemplateMetadata
{
	public string Description { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public List<string> Tags { get; set; } = [];
}
