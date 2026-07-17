namespace LabelPrinting.Models;

/// <summary>
/// Ob ein Druckerprofil ein lokal erreichbares Gerät beschreibt (eigene TCP-/USB-/Bluetooth-Verbindung)
/// oder einen Drucker, den ein anderer Client später über den zentralen Server als Druckdienst
/// bereitstellt (siehe <c>LabelPrinting.Remote</c> – dort liegen die Verträge, der Server existiert noch nicht).
/// </summary>
public enum PrinterConnectionMode
{
	Local,
	Remote,
}
