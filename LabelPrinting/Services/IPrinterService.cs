using LabelPrinting.Models;

namespace LabelPrinting.Services;

public interface IPrinterService
{
	// ---------- PrinterProfile-Überladungen (Standardweg: Profil sagt, wie der Drucker erreichbar ist) ----------

	/// <summary>Prüft die Erreichbarkeit des Druckers über die im Profil hinterlegte Anbindung.</summary>
	Task<PrinterResult> TestConnectionAsync(PrinterProfile profile, CancellationToken cancellationToken = default);

	/// <summary>Sendet rohe Daten (z.B. ZPL) unverändert an den Drucker des Profils.</summary>
	Task<PrinterResult> SendRawAsync(PrinterProfile profile, byte[] data, CancellationToken cancellationToken = default);

	/// <summary>Sendet ZPL-Text (UTF-8) an den Drucker des Profils.</summary>
	Task<PrinterResult> SendZplAsync(PrinterProfile profile, string zpl, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sendet einen Befehl (z.B. ~HS Statusabfrage oder ein SGD "! U1 getvar ..."-Kommando) und liest die
	/// Antwort, die der Drucker über dieselbe Verbindung zurückschickt.
	/// </summary>
	Task<PrinterQueryResult> QueryAsync(PrinterProfile profile, string command, CancellationToken cancellationToken = default);

	/// <summary>
	/// Fragt den Druckerstatus ab (ZPL "~HS"). Wrapper um <see cref="QueryAsync(PrinterProfile, string, CancellationToken)"/>,
	/// damit Aufrufer (insbesondere das UI) den rohen Statusbefehl nicht selbst kennen müssen.
	/// </summary>
	Task<PrinterQueryResult> GetStatusAsync(PrinterProfile profile, CancellationToken cancellationToken = default);

	/// <summary>
	/// Startet die Medienkalibrierung (ZPL "~JC"): Der Drucker zieht mehrere Etiketten durch, um
	/// Etikettenlänge sowie Lücken-/Schwarzmarkenposition automatisch zu erkennen.
	/// </summary>
	Task<PrinterResult> CalibrateMediaAsync(PrinterProfile profile, CancellationToken cancellationToken = default);

	/// <summary>
	/// Fragt den Druckerstatus ab (wie <see cref="GetStatusAsync(PrinterProfile, CancellationToken)"/>) und wertet
	/// die Antwort zusätzlich in strukturierte Felder aus (Papier/Band/Kopf, zuletzt kalibrierte Etikettenlänge).
	/// </summary>
	Task<PrinterStatus> GetDetailedStatusAsync(PrinterProfile profile, CancellationToken cancellationToken = default);

	/// <summary>
	/// Liest eine SGD-Variable (ZPL "! U1 getvar", z.B. "device.friendly_name" oder "ip.addr") vom
	/// Drucker des Profils – die rohe Antwort wird bereits über <see cref="SgdResponseParser"/> entpackt.
	/// </summary>
	Task<PrinterQueryResult> GetVariableAsync(PrinterProfile profile, string variableName, CancellationToken cancellationToken = default);

	/// <summary>Setzt eine SGD-Variable (ZPL "! U1 setvar") am Drucker des Profils, z.B. um Name/IP/Netzwerk zu ändern.</summary>
	Task<PrinterResult> SetVariableAsync(PrinterProfile profile, string variableName, string value, CancellationToken cancellationToken = default);

	/// <summary>
	/// Startet den Drucker neu (ZPL SGD "! U1 do device.reset"). Viele Netzwerkänderungen (IP, DHCP)
	/// werden erst nach einem Neustart wirksam.
	/// </summary>
	Task<PrinterResult> RestartAsync(PrinterProfile profile, CancellationToken cancellationToken = default);
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

	/// <summary>
	/// Liest eine SGD-Variable (ZPL "! U1 getvar", z.B. "device.friendly_name" oder "ip.addr") vom
	/// Drucker – Grundlage für Geräteeinstellungen wie Name/Netzwerkkonfiguration. Wrapper um
	/// <see cref="QueryAsync(string, int, string, CancellationToken)"/>, der die rohe Antwort bereits
	/// über <see cref="SgdResponseParser"/> entpackt.
	/// </summary>
	Task<PrinterQueryResult> GetVariableAsync(string ipAddress, int port, string variableName, CancellationToken cancellationToken = default);

	/// <summary>Setzt eine SGD-Variable (ZPL "! U1 setvar") am Drucker, z.B. um Name/IP/Netzwerk zu ändern.</summary>
	Task<PrinterResult> SetVariableAsync(string ipAddress, int port, string variableName, string value, CancellationToken cancellationToken = default);

	/// <summary>
	/// Startet den Drucker neu (ZPL SGD "! U1 do device.reset"). Viele Netzwerkänderungen (IP, DHCP)
	/// werden erst nach einem Neustart wirksam.
	/// </summary>
	Task<PrinterResult> RestartAsync(string ipAddress, int port, CancellationToken cancellationToken = default);

	// ---------- Transportunabhängige Überladungen (TCP/IP, seriell, künftig USB/Bluetooth) ----------

	/// <summary>Wie <see cref="SendZplAsync(string, int, string, CancellationToken)"/>, aber über eine beliebige, bereits konfigurierte <see cref="IPrinterConnection"/>.</summary>
	Task<PrinterResult> SendZplAsync(IPrinterConnection connection, string zpl, CancellationToken cancellationToken = default);

	/// <summary>Wie <see cref="SendRawAsync(string, int, byte[], CancellationToken)"/>, aber über eine beliebige, bereits konfigurierte <see cref="IPrinterConnection"/>.</summary>
	Task<PrinterResult> SendRawAsync(IPrinterConnection connection, byte[] data, CancellationToken cancellationToken = default);

	/// <summary>Wie <see cref="QueryAsync(string, int, string, CancellationToken)"/>, aber über eine beliebige, bereits konfigurierte <see cref="IPrinterConnection"/>.</summary>
	Task<PrinterQueryResult> QueryAsync(IPrinterConnection connection, string command, CancellationToken cancellationToken = default);

	/// <summary>Wie <see cref="GetVariableAsync(string, int, string, CancellationToken)"/>, aber über eine beliebige, bereits konfigurierte <see cref="IPrinterConnection"/>.</summary>
	Task<PrinterQueryResult> GetVariableAsync(IPrinterConnection connection, string variableName, CancellationToken cancellationToken = default);

	/// <summary>Wie <see cref="SetVariableAsync(string, int, string, string, CancellationToken)"/>, aber über eine beliebige, bereits konfigurierte <see cref="IPrinterConnection"/>.</summary>
	Task<PrinterResult> SetVariableAsync(IPrinterConnection connection, string variableName, string value, CancellationToken cancellationToken = default);

	/// <summary>Wie <see cref="RestartAsync(string, int, CancellationToken)"/>, aber über eine beliebige, bereits konfigurierte <see cref="IPrinterConnection"/>.</summary>
	Task<PrinterResult> RestartAsync(IPrinterConnection connection, CancellationToken cancellationToken = default);
}
