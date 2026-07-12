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

	/// <summary>
	/// Fragt den Druckerstatus ab (ZPL "~HS"). Wrapper um <see cref="QueryAsync"/>, damit Aufrufer
	/// (insbesondere das UI) den rohen Statusbefehl nicht selbst kennen müssen.
	/// </summary>
	Task<PrinterQueryResult> GetStatusAsync(string ipAddress, int port, CancellationToken cancellationToken = default);

	/// <summary>
	/// Startet die Medienkalibrierung (ZPL "~JC"): Der Drucker zieht mehrere Etiketten durch, um
	/// Etikettenlänge sowie Lücken-/Schwarzmarkenposition automatisch zu erkennen.
	/// </summary>
	Task<PrinterResult> CalibrateMediaAsync(string ipAddress, int port, CancellationToken cancellationToken = default);

	/// <summary>
	/// Fragt den Druckerstatus ab (wie <see cref="GetStatusAsync"/>) und wertet die Antwort zusätzlich
	/// in strukturierte Felder aus (Papier/Band/Kopf, zuletzt kalibrierte Etikettenlänge) – u.a. die
	/// Grundlage für die automatische Medienerkennung.
	/// </summary>
	Task<PrinterStatus> GetDetailedStatusAsync(string ipAddress, int port, CancellationToken cancellationToken = default);

	// ---------- Transportunabhängige Überladungen (TCP/IP, seriell, künftig USB/Bluetooth) ----------

	/// <summary>Wie <see cref="SendZplAsync(string, int, string, CancellationToken)"/>, aber über eine beliebige, bereits konfigurierte <see cref="IPrinterConnection"/>.</summary>
	Task<PrinterResult> SendZplAsync(IPrinterConnection connection, string zpl, CancellationToken cancellationToken = default);

	/// <summary>Wie <see cref="SendRawAsync(string, int, byte[], CancellationToken)"/>, aber über eine beliebige, bereits konfigurierte <see cref="IPrinterConnection"/>.</summary>
	Task<PrinterResult> SendRawAsync(IPrinterConnection connection, byte[] data, CancellationToken cancellationToken = default);

	/// <summary>Wie <see cref="QueryAsync(string, int, string, CancellationToken)"/>, aber über eine beliebige, bereits konfigurierte <see cref="IPrinterConnection"/>.</summary>
	Task<PrinterQueryResult> QueryAsync(IPrinterConnection connection, string command, CancellationToken cancellationToken = default);
}
