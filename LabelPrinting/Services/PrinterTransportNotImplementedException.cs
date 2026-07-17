using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>
/// Signalisiert, dass eine Anbindungsart (USB/Bluetooth) zwar architektonisch vorgesehen, aber noch
/// nicht implementiert ist. <see cref="ZplPrinterService"/> fängt sie ab und wandelt sie in ein
/// reguläres Fail-Ergebnis um – Aufrufer (UI/API) müssen wie überall sonst keine Exceptions behandeln.
/// </summary>
public class PrinterTransportNotImplementedException : Exception
{
	public PrinterTransportKind Kind { get; }

	public PrinterTransportNotImplementedException(PrinterTransportKind kind)
		: base($"Die Anbindungsart \"{kind}\" ist noch nicht implementiert.")
	{
		Kind = kind;
	}
}
