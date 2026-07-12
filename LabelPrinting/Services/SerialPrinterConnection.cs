using System.IO.Ports;

namespace LabelPrinting.Services;

/// <summary>
/// Serielle Verbindung (RS232 oder ein USB-CDC/virtueller COM-Port, wie ihn viele Etikettendrucker
/// zusätzlich zu TCP/IP anbieten). Nutzt <see cref="System.IO.Ports.SerialPort"/>, das laut
/// Microsoft-Dokumentation nur unter Windows und Linux tatsächlich funktioniert – auf anderen
/// Plattformen (Android/iOS/MacCatalyst) wirft das Öffnen des Ports eine
/// <see cref="PlatformNotSupportedException"/>, die hier wie ein normaler Verbindungsfehler
/// behandelt wird, statt die App abstürzen zu lassen.
/// </summary>
public class SerialPrinterConnection : IPrinterConnection
{
	readonly string _portName;
	readonly int _baudRate;
	SerialPort? _port;

	public SerialPrinterConnection(string portName, int baudRate = 9600)
	{
		_portName = portName;
		_baudRate = baudRate;
	}

	public Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		var port = new SerialPort(_portName, _baudRate)
		{
			WriteTimeout = 5000,
			ReadTimeout = 3000,
		};
		port.Open();
		_port = port;
		return Task.CompletedTask;
	}

	public async Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
	{
		var port = _port ?? throw new InvalidOperationException("Verbindung wurde noch nicht hergestellt.");
		await port.BaseStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
		await port.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<int> ReadAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken = default)
	{
		var port = _port ?? throw new InvalidOperationException("Verbindung wurde noch nicht hergestellt.");

		using var timeoutCts = new CancellationTokenSource(timeout);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		try
		{
			return await port.BaseStream.ReadAsync(buffer, linkedCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return 0;
		}
	}

	public ValueTask DisposeAsync()
	{
		_port?.Dispose();
		return ValueTask.CompletedTask;
	}
}
