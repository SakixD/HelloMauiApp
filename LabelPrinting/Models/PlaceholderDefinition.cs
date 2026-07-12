namespace LabelPrinting.Models;

public enum PlaceholderType
{
	Text,
	Number,
	Date,
}

/// <summary>Definition eines Platzhalters auf Vorlagen-Ebene (kann von mehreren Elementen referenziert werden).</summary>
public class PlaceholderDefinition
{
	public string Key { get; set; } = string.Empty;
	public PlaceholderType Type { get; set; } = PlaceholderType.Text;
	public bool Required { get; set; } = true;
	public string? DefaultValue { get; set; }
}
