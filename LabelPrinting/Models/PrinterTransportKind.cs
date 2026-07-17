namespace LabelPrinting.Models;

/// <summary>
/// Physische Anbindungsart eines lokalen Druckers. Nur <see cref="Tcp"/> ist implementiert;
/// <see cref="Usb"/> und <see cref="Bluetooth"/> sind architektonisch vorbereitete Stubs
/// (siehe <c>UsbPrinterConnection</c>/<c>BluetoothPrinterConnection</c>).
/// </summary>
public enum PrinterTransportKind
{
	Tcp,
	Usb,
	Bluetooth,
}
