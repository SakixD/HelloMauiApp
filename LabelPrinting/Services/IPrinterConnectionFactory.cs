using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>
/// Erzeugt aus einem lokalen <see cref="PrinterProfile"/> die zur Anbindungsart passende, noch
/// nicht verbundene <see cref="IPrinterConnection"/>. Das Profil sagt <i>was</i> (Tcp/Usb/Bluetooth
/// samt Adressdaten), die Factory baut <i>wie</i> – der Druckservice bleibt reine ZPL-Fachlogik.
/// Pro Aufruf entsteht bewusst eine frische Verbindung: die Connections sind Einweg-Objekte,
/// die der Service nach jedem Vorgang entsorgt.
/// </summary>
public interface IPrinterConnectionFactory
{
	IPrinterConnection Create(PrinterProfile profile);
}
