using System.Net.Sockets;
using System.Text;

namespace LabelPrinting.Services;

/// <summary>
/// Druckt per RAW-Socket (üblicherweise Port 9100 / JetDirect) auf Netzwerk-Etikettendruckern,
/// die ZPL verstehen (Zebra sowie Honeywell-Drucker im Zebra-Emulationsmodus).
/// </summary>
public class ZplPrinterService : IPrinterService
{
	static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
	static readonly TimeSpan InitialResponseTimeout = TimeSpan.FromSeconds(3);
	static readonly TimeSpan FollowupResponseTimeout = TimeSpan.FromMilliseconds(400);

	public async Task<PrinterResult> TestConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
	{
		try
		{
			using var client = new TcpClient();
			using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			await client.ConnectAsync(ipAddress, port, linkedCts.Token).ConfigureAwait(false);
			return PrinterResult.Ok();
		}
		catch (OperationCanceledException)
		{
			return PrinterResult.Fail($"Zeitüberschreitung: Drucker unter {ipAddress}:{port} nicht erreichbar.");
		}
		catch (SocketException ex)
		{
			return PrinterResult.Fail($"Verbindung fehlgeschlagen: {ex.Message}");
		}
		catch (Exception ex)
		{
			return PrinterResult.Fail($"Unerwarteter Fehler: {ex.Message}");
		}
	}

	public async Task<PrinterResult> SendZplAsync(string ipAddress, int port, string zpl, CancellationToken cancellationToken = default)
	{
		// ^CI28 schaltet die ZPL-Zeichenkodierung auf UTF-8, damit Umlaute etc. korrekt gedruckt werden.
		var payload = zpl.Contains("^CI28", StringComparison.Ordinal)
			? zpl
			: zpl.Replace("^XA", "^XA^CI28", StringComparison.Ordinal);

		return await SendRawAsync(ipAddress, port, Encoding.UTF8.GetBytes(payload), cancellationToken).ConfigureAwait(false);
	}

	public async Task<PrinterResult> SendRawAsync(string ipAddress, int port, byte[] data, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(ipAddress))
			return PrinterResult.Fail("Keine Drucker-IP konfiguriert.");

		try
		{
			using var client = new TcpClient();
			using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			await client.ConnectAsync(ipAddress, port, linkedCts.Token).ConfigureAwait(false);

			using var stream = client.GetStream();
			await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
			await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

			return PrinterResult.Ok();
		}
		catch (OperationCanceledException)
		{
			return PrinterResult.Fail($"Zeitüberschreitung: Drucker unter {ipAddress}:{port} nicht erreichbar.");
		}
		catch (SocketException ex)
		{
			return PrinterResult.Fail($"Verbindung fehlgeschlagen: {ex.Message}");
		}
		catch (Exception ex)
		{
			return PrinterResult.Fail($"Unerwarteter Fehler: {ex.Message}");
		}
	}

	public async Task<PrinterQueryResult> QueryAsync(string ipAddress, int port, string command, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(ipAddress))
			return PrinterQueryResult.Fail("Keine Drucker-IP konfiguriert.");

		try
		{
			using var client = new TcpClient();
			using var connectTimeoutCts = new CancellationTokenSource(DefaultTimeout);
			using var connectLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectTimeoutCts.Token);

			await client.ConnectAsync(ipAddress, port, connectLinkedCts.Token).ConfigureAwait(false);

			using var stream = client.GetStream();
			await stream.WriteAsync(Encoding.UTF8.GetBytes(command), cancellationToken).ConfigureAwait(false);
			await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

			// Der Drucker antwortet (falls überhaupt) direkt über dieselbe Verbindung. Da TCP kein
			// "Ende der Antwort" signalisiert, lesen wir zunächst mit großzügigem Timeout und danach
			// nur noch kurz weiter, bis keine weiteren Daten mehr eintreffen.
			using var responseBuffer = new MemoryStream();
			var buffer = new byte[4096];
			bool receivedAny = false;

			while (true)
			{
				var readWindow = receivedAny ? FollowupResponseTimeout : InitialResponseTimeout;
				using var readTimeoutCts = new CancellationTokenSource(readWindow);
				using var readLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, readTimeoutCts.Token);

				int read;
				try
				{
					read = await stream.ReadAsync(buffer, readLinkedCts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
					break; // Lesefenster abgelaufen: Antwort gilt als vollständig (oder Drucker antwortet nicht).
				}

				if (read <= 0)
					break; // Verbindung vom Drucker geschlossen.

				responseBuffer.Write(buffer, 0, read);
				receivedAny = true;
			}

			string raw = Encoding.Latin1.GetString(responseBuffer.ToArray());
			return PrinterQueryResult.Ok(raw);
		}
		catch (OperationCanceledException)
		{
			return PrinterQueryResult.Fail($"Zeitüberschreitung: Drucker unter {ipAddress}:{port} nicht erreichbar.");
		}
		catch (SocketException ex)
		{
			return PrinterQueryResult.Fail($"Verbindung fehlgeschlagen: {ex.Message}");
		}
		catch (Exception ex)
		{
			return PrinterQueryResult.Fail($"Unerwarteter Fehler: {ex.Message}");
		}
	}
}
