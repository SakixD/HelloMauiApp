using LabelPrinting.Models;

namespace LabelPrinting.Remote;

/// <summary>Ein einzelner Drucker, den ein Client anderen als Druckdienst anbietet.</summary>
public class PrintProviderPrinterInfo
{
	/// <summary>Server-weite, stabile Kennung – darauf verweisen fremde Clients in ihren Profilen (<see cref="PrinterProfile.RemotePrinterId"/>).</summary>
	public string RemotePrinterId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	/// <summary>Wie der anbietende Client den Drucker lokal erreicht (für Anzeige/Diagnose beim Anfragenden).</summary>
	public PrinterTransportKind TransportKind { get; set; } = PrinterTransportKind.Tcp;

	public double LabelWidthMm { get; set; }

	public double LabelHeightMm { get; set; }

	public int Dpi { get; set; }
}
