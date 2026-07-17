namespace LabelPrinting.Models;

/// <summary>Persistierte Druckerkonfiguration (IP, Port, Labelgröße, DPI). Reine Daten – Laden/Speichern übernimmt <see cref="Services.PrinterSettingsStore"/>.</summary>
[Obsolete("Ersetzt durch PrinterProfile/IPrinterProfileStore; wird nur noch für die einmalige Migration gelesen.")]
public class PrinterSettings
{
	public string IpAddress { get; set; } = string.Empty;
	public int Port { get; set; } = 9100;
	public double LabelWidthMm { get; set; } = 100;
	public double LabelHeightMm { get; set; } = 150;
	public int Dpi { get; set; } = 203;
}
