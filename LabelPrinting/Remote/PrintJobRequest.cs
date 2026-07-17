namespace LabelPrinting.Remote;

/// <summary>
/// Ein Druckauftrag, wie ihn ein anfragender Client später über den zentralen Server an einen
/// druckbereitstellenden Client schickt. Reiner Datenvertrag – Transport (SignalR) folgt später.
/// </summary>
public class PrintJobRequest
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>Server-weite Kennung des Zieldruckers (aus <see cref="PrintProviderPrinterInfo.RemotePrinterId"/>).</summary>
	public string RemotePrinterId { get; set; } = string.Empty;

	public PrintPayloadKind PayloadKind { get; set; } = PrintPayloadKind.Zpl;

	public byte[] Payload { get; set; } = [];

	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
