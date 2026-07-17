namespace LabelPrinting.Remote;

/// <summary>Art der Nutzdaten eines Remote-Druckauftrags.</summary>
public enum PrintPayloadKind
{
	/// <summary>ZPL-Text (UTF-8-kodierte Bytes).</summary>
	Zpl,

	/// <summary>Rohbytes, unverändert an den Drucker durchzureichen.</summary>
	Raw,
}
