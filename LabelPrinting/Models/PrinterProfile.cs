namespace LabelPrinting.Models;

/// <summary>
/// Ein konfigurierter Drucker als eigenständiges Profil – ersetzt die frühere globale
/// Einzeldrucker-Konfiguration <see cref="PrinterSettings"/>. Mehrere Profile werden über
/// <see cref="Services.IPrinterProfileStore"/> verwaltet; genau eines ist das Default-Profil
/// (= der app-weit aktive Drucker). Labelgeometrie und DPI gehören bewusst zum Profil,
/// weil verschiedene Drucker unterschiedliche Medien geladen haben.
/// </summary>
public class PrinterProfile
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public string Name { get; set; } = string.Empty;

	public bool IsDefault { get; set; }

	public PrinterConnectionMode ConnectionMode { get; set; } = PrinterConnectionMode.Local;

	public PrinterTransportKind TransportKind { get; set; } = PrinterTransportKind.Tcp;

	// ---------- Tcp ----------

	public string IpAddress { get; set; } = string.Empty;

	public int Port { get; set; } = 9100;

	// ---------- Usb/Bluetooth (Platzhalter, Transporte noch nicht implementiert) ----------

	public string UsbDeviceId { get; set; } = string.Empty;

	public string BluetoothAddress { get; set; } = string.Empty;

	// ---------- Remote (Platzhalter, Server existiert noch nicht) ----------

	public string RemotePrinterId { get; set; } = string.Empty;

	public string RemoteProviderName { get; set; } = string.Empty;

	// ---------- Labelgeometrie (pro Profil statt global) ----------

	public double LabelWidthMm { get; set; } = 100;

	public double LabelHeightMm { get; set; } = 150;

	public int Dpi { get; set; } = 203;

	/// <summary>
	/// Kurzbeschreibung der Verbindung für Listen/Statusanzeigen (z.B. "192.168.1.50:9100",
	/// "USB", "Remote über Server") – zentral hier, damit nicht jede Seite eigene Logik baut.
	/// </summary>
	public string ConnectionSummary => ConnectionMode == PrinterConnectionMode.Remote
		? $"Remote: {RemoteProviderName}".TrimEnd(':', ' ')
		: TransportKind switch
		{
			PrinterTransportKind.Tcp => string.IsNullOrWhiteSpace(IpAddress) ? "TCP (keine IP)" : $"{IpAddress}:{Port}",
			PrinterTransportKind.Usb => "USB",
			PrinterTransportKind.Bluetooth => "Bluetooth",
			_ => TransportKind.ToString(),
		};
}
