namespace LabelPrinting.Remote;

/// <summary>Ergebnis eines Remote-Druckauftrags (Rückmeldung des druckbereitstellenden Clients).</summary>
public class PrintJobResult
{
	public Guid JobId { get; set; }

	public bool Success { get; set; }

	public string? ErrorMessage { get; set; }

	public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;

	public static PrintJobResult Ok(Guid jobId) => new() { JobId = jobId, Success = true };

	public static PrintJobResult Fail(Guid jobId, string errorMessage)
		=> new() { JobId = jobId, Success = false, ErrorMessage = errorMessage };
}
