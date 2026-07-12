using System.Net.Sockets;
using System.Text;

namespace LabelPrinting.Services;

/// <summary>
/// Sendet ZPL an Etikettendrucker (Zebra sowie Honeywell-Drucker im Zebra-Emulationsmodus) über eine
/// austauschbare <see cref="IPrinterConnection"/> – Standardweg ist TCP/IP (RAW-Socket, i.d.R. Port
/// 9100/JetDirect), wofür die IPrinterService-Methoden mit ipAddress/port-Parametern intern eine
/// <see cref="TcpPrinterConnection"/> aufbauen. Für andere Transportarten (z.B. seriell) können die
/// <see cref="IPrinterConnection"/>-Überladungen direkt mit einer eigenen Verbindung genutzt werden.
/// </summary>
public class ZplPrinterService : IPrinterService
{
	static readonly TimeSpan InitialResponseTimeout = TimeSpan.FromSeconds(3);
	static readonly TimeSpan FollowupResponseTimeout = TimeSpan.FromMilliseconds(400);

	// ---------- IP/Port-Überladungen (TCP/IP, der bisherige Standardweg) ----------

	public Task<PrinterResult> TestConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(ipAddress))
			return Task.FromResult(PrinterResult.Fail("Keine Drucker-IP konfiguriert."));

		return RunAsync(
			new TcpPrinterConnection(ipAddress, port),
			static (_, _) => Task.FromResult(PrinterResult.Ok()),
			PrinterResult.Fail,
			$"Zeitüberschreitung: Drucker unter {ipAddress}:{port} nicht erreichbar.",
			cancellationToken);
	}

	public async Task<PrinterResult> SendZplAsync(string ipAddress, int port, string zpl, CancellationToken cancellationToken = default)
	{
		return await SendRawAsync(ipAddress, port, ToZplPayloadBytes(zpl), cancellationToken).ConfigureAwait(false);
	}

	public Task<PrinterResult> SendRawAsync(string ipAddress, int port, byte[] data, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(ipAddress))
			return Task.FromResult(PrinterResult.Fail("Keine Drucker-IP konfiguriert."));

		return RunAsync(
			new TcpPrinterConnection(ipAddress, port),
			(connection, ct) => WriteAsync(connection, data, ct),
			PrinterResult.Fail,
			$"Zeitüberschreitung: Drucker unter {ipAddress}:{port} nicht erreichbar.",
			cancellationToken);
	}

	public Task<PrinterQueryResult> QueryAsync(string ipAddress, int port, string command, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(ipAddress))
			return Task.FromResult(PrinterQueryResult.Fail("Keine Drucker-IP konfiguriert."));

		return RunAsync(
			new TcpPrinterConnection(ipAddress, port),
			(connection, ct) => QueryCoreAsync(connection, command, ct),
			PrinterQueryResult.Fail,
			$"Zeitüberschreitung: Drucker unter {ipAddress}:{port} nicht erreichbar.",
			cancellationToken);
	}

	public Task<PrinterQueryResult> GetStatusAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
		=> QueryAsync(ipAddress, port, "~HS", cancellationToken);

	public Task<PrinterResult> CalibrateMediaAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
		=> SendZplAsync(ipAddress, port, "~JC", cancellationToken);

	public async Task<PrinterStatus> GetDetailedStatusAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
	{
		var result = await GetStatusAsync(ipAddress, port, cancellationToken).ConfigureAwait(false);
		return result.Success
			? ZplStatusParser.Parse(result.ResponseText)
			: PrinterStatus.Fail(result.ErrorMessage ?? "Unbekannter Fehler");
	}

	// ---------- IPrinterConnection-Überladungen (transportunabhängig, z.B. seriell) ----------

	public Task<PrinterResult> SendZplAsync(IPrinterConnection connection, string zpl, CancellationToken cancellationToken = default)
		=> SendRawAsync(connection, ToZplPayloadBytes(zpl), cancellationToken);

	public Task<PrinterResult> SendRawAsync(IPrinterConnection connection, byte[] data, CancellationToken cancellationToken = default)
	{
		return RunAsync(
			connection,
			(conn, ct) => WriteAsync(conn, data, ct),
			PrinterResult.Fail,
			"Zeitüberschreitung: Drucker nicht erreichbar.",
			cancellationToken);
	}

	public Task<PrinterQueryResult> QueryAsync(IPrinterConnection connection, string command, CancellationToken cancellationToken = default)
	{
		return RunAsync(
			connection,
			(conn, ct) => QueryCoreAsync(conn, command, ct),
			PrinterQueryResult.Fail,
			"Zeitüberschreitung: Drucker nicht erreichbar.",
			cancellationToken);
	}

	static byte[] ToZplPayloadBytes(string zpl)
	{
		// ^CI28 schaltet die ZPL-Zeichenkodierung auf UTF-8, damit Umlaute etc. korrekt gedruckt werden.
		var payload = zpl.Contains("^CI28", StringComparison.Ordinal)
			? zpl
			: zpl.Replace("^XA", "^XA^CI28", StringComparison.Ordinal);

		return Encoding.UTF8.GetBytes(payload);
	}

	static async Task<PrinterResult> WriteAsync(IPrinterConnection connection, byte[] data, CancellationToken cancellationToken)
	{
		await connection.WriteAsync(data, cancellationToken).ConfigureAwait(false);
		return PrinterResult.Ok();
	}

	static async Task<PrinterQueryResult> QueryCoreAsync(IPrinterConnection connection, string command, CancellationToken cancellationToken)
	{
		await connection.WriteAsync(Encoding.UTF8.GetBytes(command), cancellationToken).ConfigureAwait(false);
		string raw = await ReadResponseAsync(connection, cancellationToken).ConfigureAwait(false);
		return PrinterQueryResult.Ok(raw);
	}

	/// <summary>
	/// Baut die Verbindung auf und führt anschließend <paramref name="action"/> aus. Bündelt den für
	/// Verbindungsaufbau, Test, Senden und Abfragen identischen Ablauf sowie das
	/// Exception→Result-Mapping an einer einzigen Stelle, transportunabhängig.
	/// </summary>
	static async Task<TResult> RunAsync<TResult>(
		IPrinterConnection connection,
		Func<IPrinterConnection, CancellationToken, Task<TResult>> action,
		Func<string, TResult> fail,
		string unreachableMessage,
		CancellationToken cancellationToken)
	{
		try
		{
			await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);
			return await action(connection, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			return fail(unreachableMessage);
		}
		catch (SocketException ex)
		{
			return fail($"Verbindung fehlgeschlagen: {ex.Message}");
		}
		catch (PlatformNotSupportedException ex)
		{
			return fail($"Diese Verbindungsart wird auf dieser Plattform nicht unterstützt: {ex.Message}");
		}
		catch (Exception ex)
		{
			return fail($"Unerwarteter Fehler: {ex.Message}");
		}
		finally
		{
			await connection.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Der Drucker antwortet (falls überhaupt) direkt über dieselbe Verbindung. Da die genutzten
	/// Protokolle (TCP, seriell) kein "Ende der Antwort" signalisieren, lesen wir zunächst mit
	/// großzügigem Timeout und danach nur noch kurz weiter, bis keine weiteren Daten mehr eintreffen.
	/// </summary>
	static async Task<string> ReadResponseAsync(IPrinterConnection connection, CancellationToken cancellationToken)
	{
		using var responseBuffer = new MemoryStream();
		var buffer = new byte[4096];
		bool receivedAny = false;

		while (true)
		{
			var readWindow = receivedAny ? FollowupResponseTimeout : InitialResponseTimeout;
			int read = await connection.ReadAsync(buffer, readWindow, cancellationToken).ConfigureAwait(false);

			if (read <= 0)
				break; // Lesefenster abgelaufen oder Verbindung geschlossen: Antwort gilt als vollständig.

			responseBuffer.Write(buffer, 0, read);
			receivedAny = true;
		}

		return Encoding.Latin1.GetString(responseBuffer.ToArray());
	}
}
