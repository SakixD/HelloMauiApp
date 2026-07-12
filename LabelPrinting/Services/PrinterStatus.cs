namespace LabelPrinting.Services;

/// <summary>
/// Ausgewertete Felder der ZPL "~HS"-Statusabfrage (Host Status Return). Es werden nur die über
/// verschiedene Zebra-Firmwarestände hinweg stabilen Felder geparst; <see cref="RawResponse"/>
/// enthält immer die volle Original-Antwort, falls weitere Felder ausgewertet werden müssen.
/// </summary>
public class PrinterStatus
{
	public bool Success { get; init; }
	public string? ErrorMessage { get; init; }
	public string RawResponse { get; init; } = string.Empty;

	public bool? PaperOut { get; init; }
	public bool? Paused { get; init; }

	/// <summary>Zuletzt kalibrierte Etikettenlänge in Dots (mit der konfigurierten DPI in mm umrechenbar).</summary>
	public int? LabelLengthDots { get; init; }

	public bool? HeadOpen { get; init; }
	public bool? RibbonOut { get; init; }

	public static PrinterStatus Fail(string message) => new() { Success = false, ErrorMessage = message };
}
