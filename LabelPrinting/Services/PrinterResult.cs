namespace LabelPrinting.Services;

public class PrinterResult
{
	public bool Success { get; init; }
	public string? ErrorMessage { get; init; }

	public static PrinterResult Ok() => new() { Success = true };
	public static PrinterResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
