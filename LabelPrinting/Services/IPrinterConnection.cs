namespace LabelPrinting.Services;

/// <summary>
/// Transportunabhängige Verbindung zu einem Drucker. <see cref="ZplPrinterService"/> baut auf dieser
/// Abstraktion auf, damit dieselbe ZPL-Sende-/Empfangslogik über TCP/IP, seriell und künftig auch
/// USB/Bluetooth funktioniert, ohne dass Aufrufer den jeweiligen Transport kennen müssen.
/// </summary>
public interface IPrinterConnection : IAsyncDisposable
{
	Task ConnectAsync(CancellationToken cancellationToken = default);

	Task WriteAsync(byte[] data, CancellationToken cancellationToken = default);

	/// <summary>
	/// Liest verfügbare Daten in <paramref name="buffer"/>. Gibt 0 zurück, wenn im gegebenen
	/// Zeitfenster keine (weiteren) Daten ankommen – das ist kein Fehler, sondern das normale
	/// Signal "Antwort ist vollständig" bei Protokollen ohne explizites Ende-Zeichen.
	/// </summary>
	Task<int> ReadAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken = default);
}
