using System.Net.Sockets;

namespace LabelPrinting.Services;

/// <summary>TCP/IP-Verbindung (RAW-Socket, i.d.R. Port 9100/JetDirect).</summary>
public class TcpPrinterConnection : IPrinterConnection
{
	static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

	readonly string _ipAddress;
	readonly int _port;
	TcpClient? _client;
	NetworkStream? _stream;

	public TcpPrinterConnection(string ipAddress, int port)
	{
		_ipAddress = ipAddress;
		_port = port;
	}

	public async Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		_client = new TcpClient();

		using var timeoutCts = new CancellationTokenSource(ConnectTimeout);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		await _client.ConnectAsync(_ipAddress, _port, linkedCts.Token).ConfigureAwait(false);
		_stream = _client.GetStream();
	}

	public async Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
	{
		var stream = _stream ?? throw new InvalidOperationException("Verbindung wurde noch nicht hergestellt.");
		await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
		await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<int> ReadAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken = default)
	{
		var stream = _stream ?? throw new InvalidOperationException("Verbindung wurde noch nicht hergestellt.");

		using var timeoutCts = new CancellationTokenSource(timeout);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		try
		{
			return await stream.ReadAsync(buffer, linkedCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return 0; // Lesefenster abgelaufen: Antwort gilt als vollständig (oder Gegenstelle antwortet nicht).
		}
	}

	public ValueTask DisposeAsync()
	{
		_stream?.Dispose();
		_client?.Dispose();
		return ValueTask.CompletedTask;
	}
}
