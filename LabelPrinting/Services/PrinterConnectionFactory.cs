using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>Standard-Implementierung von <see cref="IPrinterConnectionFactory"/> (siehe dort).</summary>
public class PrinterConnectionFactory : IPrinterConnectionFactory
{
	readonly Func<string, int, IPrinterConnection> _tcpConnectionFactory;

	/// <param name="tcpConnectionFactory">
	/// Baut die TCP-Verbindung (Standard: <see cref="TcpPrinterConnection"/>). Austauschbar, damit
	/// Konsumenten/Tests Profile nutzen können, ohne einen echten Socket zu öffnen.
	/// </param>
	public PrinterConnectionFactory(Func<string, int, IPrinterConnection>? tcpConnectionFactory = null)
	{
		_tcpConnectionFactory = tcpConnectionFactory ?? ((ip, port) => new TcpPrinterConnection(ip, port));
	}

	public IPrinterConnection Create(PrinterProfile profile) => profile.TransportKind switch
	{
		PrinterTransportKind.Tcp => _tcpConnectionFactory(profile.IpAddress, profile.Port),
		PrinterTransportKind.Usb => new UsbPrinterConnection(profile.UsbDeviceId),
		PrinterTransportKind.Bluetooth => new BluetoothPrinterConnection(profile.BluetoothAddress),
		_ => throw new PrinterTransportNotImplementedException(profile.TransportKind),
	};
}
