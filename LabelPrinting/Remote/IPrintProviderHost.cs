namespace LabelPrinting.Remote;

/// <summary>
/// Sicht des <b>druckbereitstellenden</b> Clients: eigene Drucker am Server anmelden und eingehende
/// Aufträge lokal ausführen. Bewusst nur der Vertrag – die Implementierung (SignalR-Verbindung,
/// Job-Ausführung über den lokalen Druckservice) entsteht erst mit dem Server.
/// </summary>
public interface IPrintProviderHost
{
	Task RegisterAsync(PrintProviderRegistration registration, CancellationToken cancellationToken = default);

	Task UnregisterAsync(CancellationToken cancellationToken = default);

	/// <summary>Wird ausgelöst, wenn der Server einen Druckauftrag für einen der angemeldeten Drucker zustellt.</summary>
	event Func<PrintJobRequest, Task<PrintJobResult>>? JobReceived;
}
