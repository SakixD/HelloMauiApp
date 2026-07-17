using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>
/// Architektur-Stub für USB-Drucker: reserviert die Stelle, an der später die plattformspezifische
/// USB-Anbindung entsteht (bewusst ohne #if-Plattformcode). Jeder Verbindungsversuch wirft
/// <see cref="PrinterTransportNotImplementedException"/>; nur <see cref="DisposeAsync"/> ist
/// harmlos, weil der Service Verbindungen immer im finally entsorgt.
/// </summary>
public class UsbPrinterConnection : IPrinterConnection
{
	// Für die spätere Implementierung; aktuell bewusst ungenutzt.
	readonly string _usbDeviceId;

	public UsbPrinterConnection(string usbDeviceId)
	{
		_usbDeviceId = usbDeviceId;
	}

	public Task ConnectAsync(CancellationToken cancellationToken = default)
		=> throw new PrinterTransportNotImplementedException(PrinterTransportKind.Usb);

	public Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
		=> throw new PrinterTransportNotImplementedException(PrinterTransportKind.Usb);

	public Task<int> ReadAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken = default)
		=> throw new PrinterTransportNotImplementedException(PrinterTransportKind.Usb);

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
