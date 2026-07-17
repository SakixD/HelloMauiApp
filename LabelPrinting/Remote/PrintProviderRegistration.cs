namespace LabelPrinting.Remote;

/// <summary>"Was bietet dieser Client an" – Anmeldung eines druckbereitstellenden Clients am Server.</summary>
public class PrintProviderRegistration
{
	/// <summary>Stabile Kennung dieses Clients (überlebt Neustarts, damit Remote-Profile gültig bleiben).</summary>
	public string ClientId { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public List<PrintProviderPrinterInfo> Printers { get; set; } = [];
}
