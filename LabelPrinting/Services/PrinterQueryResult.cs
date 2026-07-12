namespace LabelPrinting.Services;

public class PrinterQueryResult
{
	public bool Success { get; init; }
	public string? ErrorMessage { get; init; }
	public string ResponseText { get; init; } = string.Empty;

	public static PrinterQueryResult Ok(string response) => new() { Success = true, ResponseText = response };
	public static PrinterQueryResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
