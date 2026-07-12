namespace LabelPrinting.Services;

public class TemplateFillResult
{
	public bool Success { get; init; }
	public IReadOnlyList<string> MissingRequiredKeys { get; init; } = [];
	public IReadOnlyDictionary<string, string> ResolvedData { get; init; } = new Dictionary<string, string>();

	public static TemplateFillResult Ok(IReadOnlyDictionary<string, string> data) => new() { Success = true, ResolvedData = data };

	public static TemplateFillResult Missing(IReadOnlyList<string> keys) => new() { Success = false, MissingRequiredKeys = keys };
}
