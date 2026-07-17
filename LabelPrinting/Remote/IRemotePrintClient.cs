using LabelPrinting.Services;

namespace LabelPrinting.Remote;

/// <summary>
/// Sicht des <b>anfragenden</b> Clients: Druckaufträge über den zentralen Server an fremde Drucker
/// schicken. Bewusst nur der Vertrag – die Implementierung (SignalR-Client) entsteht erst mit dem
/// Server. <c>ZplPrinterService</c> nutzt diese Schnittstelle optional für Profile mit
/// <c>PrinterConnectionMode.Remote</c>.
/// </summary>
public interface IRemotePrintClient
{
	Task<PrintJobResult> SubmitJobAsync(PrintJobRequest request, CancellationToken cancellationToken = default);

	/// <summary>Erreichbarkeitsprüfung eines Remote-Druckers, analog zu <c>TestConnectionAsync</c> lokal.</summary>
	Task<PrinterResult> TestRemotePrinterAsync(string remotePrinterId, CancellationToken cancellationToken = default);
}
