namespace LabelPrinting.Models;

/// <summary>Wie der Drucker das Ende eines Etiketts erkennt.</summary>
public enum MediaSensorType
{
	/// <summary>Lücke zwischen den Etiketten.</summary>
	Gap,
	/// <summary>Schwarzmarke auf der Rückseite des Trägermaterials.</summary>
	BlackMark,
	/// <summary>Endlosmaterial ohne Lücke/Marke (Länge wird per ^LL vorgegeben).</summary>
	Continuous,
}

/// <summary>
/// Ein Druckmedium (Etikettenrolle/Material), unabhängig von einer einzelnen Vorlage gespeichert,
/// damit es im Designer wiederverwendet oder vom Drucker übernommen werden kann.
/// </summary>
public class PrintMedia
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string Name { get; set; } = "Neues Medium";
	public double WidthMm { get; set; } = 100;
	public double HeightMm { get; set; } = 150;
	public double GapMm { get; set; } = 2;
	public MediaSensorType SensorType { get; set; } = MediaSensorType.Gap;
	public string Material { get; set; } = string.Empty;
}
