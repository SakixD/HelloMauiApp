namespace LabelPrinting.Services;

public interface IPrinterService
{
	/// <summary>Öffnet kurz eine TCP-Verbindung zum Drucker, um Erreichbarkeit zu prüfen.</summary>
	Task<PrinterResult> TestConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken = default);

	/// <summary>Sendet rohe Daten (z.B. ZPL) unverändert an den Drucker (Port 9100 / RAW-Druck).</summary>
	Task<PrinterResult> SendRawAsync(string ipAddress, int port, byte[] data, CancellationToken cancellationToken = default);

	/// <summary>Sendet ZPL-Text (UTF-8) an den Drucker.</summary>
	Task<PrinterResult> SendZplAsync(string ipAddress, int port, string zpl, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sendet einen Befehl (z.B. ~HS Statusabfrage oder ein SGD "! U1 getvar ..."-Kommando) und liest die
	/// Antwort, die der Drucker über dieselbe TCP-Verbindung zurückschickt.
	/// </summary>
	Task<PrinterQueryResult> QueryAsync(string ipAddress, int port, string command, CancellationToken cancellationToken = default);
}
