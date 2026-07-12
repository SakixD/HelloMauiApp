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

	public Task<PrinterResult> TestConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
	{
		return RunAsync(ipAddress, port, static (_, _) => Task.FromResult(PrinterResult.Ok()), PrinterResult.Fail, cancellationToken);
	}

	public async Task<PrinterResult> SendZplAsync(string ipAddress, int port, string zpl, CancellationToken cancellationToken = default)
	{
		// ^CI28 schaltet die ZPL-Zeichenkodierung auf UTF-8, damit Umlaute etc. korrekt gedruckt werden.
		var payload = zpl.Contains("^CI28", StringComparison.Ordinal)
			? zpl
			: zpl.Replace("^XA", "^XA^CI28", StringComparison.Ordinal);

		return await SendRawAsync(ipAddress, port, Encoding.UTF8.GetBytes(payload), cancellationToken).ConfigureAwait(false);
	}

	public Task<PrinterResult> SendRawAsync(string ipAddress, int port, byte[] data, CancellationToken cancellationToken = default)
	{
		return RunAsync(ipAddress, port, async (stream, ct) =>
		{
			await stream.WriteAsync(data, ct).ConfigureAwait(false);
			await stream.FlushAsync(ct).ConfigureAwait(false);
			return PrinterResult.Ok();
		}, PrinterResult.Fail, cancellationToken);
	}

	public Task<PrinterQueryResult> QueryAsync(string ipAddress, int port, string command, CancellationToken cancellationToken = default)
	{
		return RunAsync(ipAddress, port, async (stream, ct) =>
		{
			await stream.WriteAsync(Encoding.UTF8.GetBytes(command), ct).ConfigureAwait(false);
			await stream.FlushAsync(ct).ConfigureAwait(false);

			string raw = await ReadResponseAsync(stream, ct).ConfigureAwait(false);
			return PrinterQueryResult.Ok(raw);
		}, PrinterQueryResult.Fail, cancellationToken);
	}

	public Task<PrinterQueryResult> GetStatusAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
		=> QueryAsync(ipAddress, port, "~HS", cancellationToken);

	public Task<PrinterResult> CalibrateMediaAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
		=> SendZplAsync(ipAddress, port, "~JC", cancellationToken);

	/// <summary>
	/// Baut die TCP-Verbindung auf (mit Timeout) und führt anschließend <paramref name="action"/> auf dem
	/// offenen Stream aus. Bündelt den für Verbindungsaufbau, Test, Senden und Abfragen identischen
	/// Verbindungsaufbau sowie das Exception→Result-Mapping an einer einzigen Stelle.
	/// </summary>
	static async Task<TResult> RunAsync<TResult>(
		string ipAddress,
		int port,
		Func<NetworkStream, CancellationToken, Task<TResult>> action,
		Func<string, TResult> fail,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(ipAddress))
			return fail("Keine Drucker-IP konfiguriert.");

		try
		{
			using var client = new TcpClient();
			using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

			await client.ConnectAsync(ipAddress, port, linkedCts.Token).ConfigureAwait(false);

			using var stream = client.GetStream();
			return await action(stream, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			return fail($"Zeitüberschreitung: Drucker unter {ipAddress}:{port} nicht erreichbar.");
		}
		catch (SocketException ex)
		{
			return fail($"Verbindung fehlgeschlagen: {ex.Message}");
		}
		catch (Exception ex)
		{
			return fail($"Unerwarteter Fehler: {ex.Message}");
		}
	}

	/// <summary>
	/// Der Drucker antwortet (falls überhaupt) direkt über dieselbe Verbindung. Da TCP kein "Ende der
	/// Antwort" signalisiert, lesen wir zunächst mit großzügigem Timeout und danach nur noch kurz weiter,
	/// bis keine weiteren Daten mehr eintreffen.
	/// </summary>
	static async Task<string> ReadResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
	{
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

		return Encoding.Latin1.GetString(responseBuffer.ToArray());
	}
}
