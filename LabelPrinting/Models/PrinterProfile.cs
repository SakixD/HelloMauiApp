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

	// ---------- Fachliche Zuordnung (Vorgriff auf ROADMAP Phase 3a, siehe DeviceRoleName) ----------

	/// <summary>Freier Kommentar für Nutzer (Standort, Besonderheiten) — reine Anzeige, keine Logik.</summary>
	public string Comment { get; set; } = string.Empty;

	/// <summary>
	/// Fachliche Rollen dieses Druckers im Format "Bereich.Rolle" (z.B. "Versand.PaketLabel"),
	/// validiert über <see cref="DeviceRoleName"/>. Bewusster Vorgriff auf die DeviceRole-Schicht
	/// aus ROADMAP Phase 3a: hier nur Daten, keine Auflösungslogik — die spätere Schicht
	/// übernimmt sie unverändert.
	/// </summary>
	public List<string> Roles { get; set; } = [];

	// ---------- Labelgeometrie (pro Profil statt global) ----------

	public double LabelWidthMm { get; set; } = 100;

	public double LabelHeightMm { get; set; } = 150;

	public int Dpi { get; set; } = 203;

	/// <summary>
	/// Kopie aller Werte. Für Editoren, die ein Profil erst beim Speichern zurückschreiben, ohne
	/// Felder zu verlieren, die ihr Formular nicht kennt — per MemberwiseClone, damit künftige
	/// Felder automatisch mitkopiert werden; Listen werden danach tief kopiert.
	/// </summary>
	public PrinterProfile Clone()
	{
		var clone = (PrinterProfile)MemberwiseClone();
		clone.Roles = [.. Roles]; // MemberwiseClone teilt sonst die Listenreferenz mit dem Original
		return clone;
	}

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
