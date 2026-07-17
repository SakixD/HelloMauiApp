using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>
/// Architektur-Stub für Bluetooth-Drucker – Gegenstück zu <see cref="UsbPrinterConnection"/>,
/// siehe dort für die Begründung des Musters.
/// </summary>
public class BluetoothPrinterConnection : IPrinterConnection
{
	// Für die spätere Implementierung; aktuell bewusst ungenutzt.
	readonly string _bluetoothAddress;

	public BluetoothPrinterConnection(string bluetoothAddress)
	{
		_bluetoothAddress = bluetoothAddress;
	}

	public Task ConnectAsync(CancellationToken cancellationToken = default)
		=> throw new PrinterTransportNotImplementedException(PrinterTransportKind.Bluetooth);

	public Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
		=> throw new PrinterTransportNotImplementedException(PrinterTransportKind.Bluetooth);

	public Task<int> ReadAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken = default)
		=> throw new PrinterTransportNotImplementedException(PrinterTransportKind.Bluetooth);

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
