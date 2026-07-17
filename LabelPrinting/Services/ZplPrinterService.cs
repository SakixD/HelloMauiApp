using System.Net.Sockets;
using System.Text;
using LabelPrinting.Models;
using LabelPrinting.Remote;

namespace LabelPrinting.Services;

/// <summary>
/// Sendet ZPL an Etikettendrucker (Zebra sowie Honeywell-Drucker im Zebra-Emulationsmodus) über eine
/// austauschbare <see cref="IPrinterConnection"/>. Standardweg sind die <see cref="PrinterProfile"/>-
/// Überladungen: Das Profil beschreibt die Anbindung, die <see cref="IPrinterConnectionFactory"/>
/// baut daraus die Verbindung. Profile mit <see cref="PrinterConnectionMode.Remote"/> werden an den
/// optionalen <see cref="IRemotePrintClient"/> delegiert (ohne diesen: sauberes Fail-Ergebnis).
/// Für Spezialfälle (z.B. seriell) können die <see cref="IPrinterConnection"/>-Überladungen direkt
/// mit einer eigenen Verbindung genutzt werden.
/// </summary>
public class ZplPrinterService : IPrinterService
{
	static readonly TimeSpan InitialResponseTimeout = TimeSpan.FromSeconds(3);
	static readonly TimeSpan FollowupResponseTimeout = TimeSpan.FromMilliseconds(400);

	const string RemoteUnavailableMessage = "Remote-Druck ist in dieser Version noch nicht verfügbar.";
	const string RemoteQueryUnavailableMessage = "Statusabfragen sind für Remote-Drucker noch nicht verfügbar.";

	readonly Func<string, int, IPrinterConnection> _tcpConnectionFactory;
	readonly IPrinterConnectionFactory _connectionFactory;
	readonly IRemotePrintClient? _remoteClient;

	/// <param name="connectionFactory">Baut die Verbindung für die Profil-Überladungen (Standard: <see cref="PrinterConnectionFactory"/>).</param>
	/// <param name="remoteClient">
	/// Optionaler Client für Profile mit <see cref="PrinterConnectionMode.Remote"/>. Solange es keinen
	/// Server gibt, bleibt er null – Remote-Aufrufe liefern dann ein Fail-Ergebnis statt zu drucken.
	/// </param>
	/// <param name="tcpConnectionFactory">
	/// Baut die Verbindung für die ip/port-Überladungen (Standard: <see cref="TcpPrinterConnection"/>).
	/// Austauschbar, damit Konsumenten/Tests die ip/port-API nutzen können, ohne einen echten
	/// TCP-Socket zu öffnen (z.B. eine Fake-<see cref="IPrinterConnection"/> für Unit-Tests).
	/// </param>
	public ZplPrinterService(
		IPrinterConnectionFactory? connectionFactory = null,
		IRemotePrintClient? remoteClient = null,
		Func<string, int, IPrinterConnection>? tcpConnectionFactory = null)
	{
		_tcpConnectionFactory = tcpConnectionFactory ?? ((ip, port) => new TcpPrinterConnection(ip, port));
		_connectionFactory = connectionFactory ?? new PrinterConnectionFactory(tcpConnectionFactory);
		_remoteClient = remoteClient;
	}

	// ---------- PrinterProfile-Überladungen (Standardweg der App) ----------

	public Task<PrinterResult> TestConnectionAsync(PrinterProfile profile, CancellationToken cancellationToken = default)
	{
		if (profile.ConnectionMode == PrinterConnectionMode.Remote)
		{
			return _remoteClient is null
				? Task.FromResult(PrinterResult.Fail(RemoteUnavailableMessage))
				: _remoteClient.TestRemotePrinterAsync(profile.RemotePrinterId, cancellationToken);
		}

		if (!TryCreateConnection(profile, out var connection, out string? error))
			return Task.FromResult(PrinterResult.Fail(error));

		return RunAsync(
			connection,
			static (_, _) => Task.FromResult(PrinterResult.Ok()),
			PrinterResult.Fail,
			UnreachableMessage(profile),
			cancellationToken);
	}

	public Task<PrinterResult> SendZplAsync(PrinterProfile profile, string zpl, CancellationToken cancellationToken = default)
		=> SendPayloadAsync(profile, ToZplPayloadBytes(zpl), PrintPayloadKind.Zpl, cancellationToken);

	public Task<PrinterResult> SendRawAsync(PrinterProfile profile, byte[] data, CancellationToken cancellationToken = default)
		=> SendPayloadAsync(profile, data, PrintPayloadKind.Raw, cancellationToken);

	public Task<PrinterQueryResult> QueryAsync(PrinterProfile profile, string command, CancellationToken cancellationToken = default)
	{
		// Die Remote-Verträge kennen bisher nur Druckaufträge und Erreichbarkeitstests – bidirektionale
		// Abfragen über den Server folgen erst mit dem Backend.
		if (profile.ConnectionMode == PrinterConnectionMode.Remote)
			return Task.FromResult(PrinterQueryResult.Fail(RemoteQueryUnavailableMessage));

		if (!TryCreateConnection(profile, out var connection, out string? error))
			return Task.FromResult(PrinterQueryResult.Fail(error));

		return RunAsync(
			connection,
			(conn, ct) => QueryCoreAsync(conn, command, ct),
			PrinterQueryResult.Fail,
			UnreachableMessage(profile),
			cancellationToken);
	}

	public Task<PrinterQueryResult> GetStatusAsync(PrinterProfile profile, CancellationToken cancellationToken = default)
		=> QueryAsync(profile, "~HS", cancellationToken);

	public Task<PrinterResult> CalibrateMediaAsync(PrinterProfile profile, CancellationToken cancellationToken = default)
		=> SendZplAsync(profile, "~JC", cancellationToken);

	public async Task<PrinterStatus> GetDetailedStatusAsync(PrinterProfile profile, CancellationToken cancellationToken = default)
	{
		var result = await GetStatusAsync(profile, cancellationToken).ConfigureAwait(false);
		return result.Success
			? ZplStatusParser.Parse(result.ResponseText)
			: PrinterStatus.Fail(result.ErrorMessage ?? "Unbekannter Fehler");
	}

	public async Task<PrinterQueryResult> GetVariableAsync(PrinterProfile profile, string variableName, CancellationToken cancellationToken = default)
	{
		var result = await QueryAsync(profile, BuildGetVarCommand(variableName), cancellationToken).ConfigureAwait(false);
		return result.Success ? PrinterQueryResult.Ok(SgdResponseParser.Parse(result.ResponseText)) : result;
	}

	public Task<PrinterResult> SetVariableAsync(PrinterProfile profile, string variableName, string value, CancellationToken cancellationToken = default)
		=> SendZplAsync(profile, BuildSetVarCommand(variableName, value), cancellationToken);

	public Task<PrinterResult> RestartAsync(PrinterProfile profile, CancellationToken cancellationToken = default)
		=> SendZplAsync(profile, RestartCommand, cancellationToken);

	/// <summary>Gemeinsamer Sendeweg der Profil-Überladungen: lokal über die Factory-Verbindung, remote als Druckauftrag.</summary>
	async Task<PrinterResult> SendPayloadAsync(PrinterProfile profile, byte[] payload, PrintPayloadKind payloadKind, CancellationToken cancellationToken)
	{
		if (profile.ConnectionMode == PrinterConnectionMode.Remote)
		{
			if (_remoteClient is null)
				return PrinterResult.Fail(RemoteUnavailableMessage);

			var jobResult = await _remoteClient.SubmitJobAsync(new PrintJobRequest
			{
				RemotePrinterId = profile.RemotePrinterId,
				PayloadKind = payloadKind,
				Payload = payload,
			}, cancellationToken).ConfigureAwait(false);

			return jobResult.Success
				? PrinterResult.Ok()
				: PrinterResult.Fail(jobResult.ErrorMessage ?? "Remote-Druck fehlgeschlagen.");
		}

		if (!TryCreateConnection(profile, out var connection, out string? error))
			return PrinterResult.Fail(error);

		return await RunAsync(
			connection,
			(conn, ct) => WriteAsync(conn, payload, ct),
			PrinterResult.Fail,
			UnreachableMessage(profile),
			cancellationToken).ConfigureAwait(false);
	}

	bool TryCreateConnection(PrinterProfile profile, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IPrinterConnection? connection, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
	{
		if (profile.TransportKind == PrinterTransportKind.Tcp && string.IsNullOrWhiteSpace(profile.IpAddress))
		{
			connection = null;
			error = "Keine Drucker-IP konfiguriert.";
			return false;
		}

		connection = _connectionFactory.Create(profile);
		error = null;
		return true;
	}

	static string UnreachableMessage(PrinterProfile profile)
		=> $"Zeitüberschreitung: Drucker \"{profile.Name}\" ({profile.ConnectionSummary}) nicht erreichbar.";

	// ---------- IP/Port-Überladungen (TCP/IP, der bisherige Standardweg) ----------

	public Task<PrinterResult> TestConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(ipAddress))
			return Task.FromResult(PrinterResult.Fail("Keine Drucker-IP konfiguriert."));

		return RunAsync(
			_tcpConnectionFactory(ipAddress, port),
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
			_tcpConnectionFactory(ipAddress, port),
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
			_tcpConnectionFactory(ipAddress, port),
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

	public async Task<PrinterQueryResult> GetVariableAsync(string ipAddress, int port, string variableName, CancellationToken cancellationToken = default)
	{
		var result = await QueryAsync(ipAddress, port, BuildGetVarCommand(variableName), cancellationToken).ConfigureAwait(false);
		return result.Success ? PrinterQueryResult.Ok(SgdResponseParser.Parse(result.ResponseText)) : result;
	}

	public Task<PrinterResult> SetVariableAsync(string ipAddress, int port, string variableName, string value, CancellationToken cancellationToken = default)
		=> SendZplAsync(ipAddress, port, BuildSetVarCommand(variableName, value), cancellationToken);

	public Task<PrinterResult> RestartAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
		=> SendZplAsync(ipAddress, port, RestartCommand, cancellationToken);

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

	public async Task<PrinterQueryResult> GetVariableAsync(IPrinterConnection connection, string variableName, CancellationToken cancellationToken = default)
	{
		var result = await QueryAsync(connection, BuildGetVarCommand(variableName), cancellationToken).ConfigureAwait(false);
		return result.Success ? PrinterQueryResult.Ok(SgdResponseParser.Parse(result.ResponseText)) : result;
	}

	public Task<PrinterResult> SetVariableAsync(IPrinterConnection connection, string variableName, string value, CancellationToken cancellationToken = default)
		=> SendZplAsync(connection, BuildSetVarCommand(variableName, value), cancellationToken);

	public Task<PrinterResult> RestartAsync(IPrinterConnection connection, CancellationToken cancellationToken = default)
		=> SendZplAsync(connection, RestartCommand, cancellationToken);

	const string RestartCommand = "! U1 do \"device.reset\" \"\"\r\n";

	static string BuildGetVarCommand(string variableName) => $"! U1 getvar \"{SanitizeSgdToken(variableName)}\"\r\n";

	static string BuildSetVarCommand(string variableName, string value) => $"! U1 setvar \"{SanitizeSgdToken(variableName)}\" \"{SanitizeSgdToken(value)}\"\r\n";

	static string SanitizeSgdToken(string token) => token.Replace("\"", string.Empty);

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
		catch (PrinterTransportNotImplementedException ex)
		{
			// USB/Bluetooth-Stubs: sauberes Fehlerergebnis statt Absturz, exakt wie alle anderen Fehlerwege.
			return fail(ex.Message);
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
