namespace LabelPrinting.Services;

/// <summary>Ergebnis der Umwandlung eines Bildes in ein ZPL-Grafikfeld (^GFA).</summary>
public record ZplGraphic(int WidthDots, int HeightDots, string ZplFieldData)
{
	/// <summary>Fertiges ZPL-Snippet inkl. Positionierung, einsetzbar zwischen ^XA und ^XZ.</summary>
	public string ToFieldOrigin(int x, int y) => $"^FO{x},{y}{ZplFieldData}^FS";
}
