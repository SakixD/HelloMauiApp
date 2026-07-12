namespace LabelPrinting.Models;

/// <summary>
/// Ein Feldwert, der entweder fest verdrahtet ist (LiteralValue) oder zur Druckzeit aus den
/// mitgegebenen Daten über einen Platzhalter-Key aufgelöst wird.
/// </summary>
public class BindableValue
{
	public bool IsPlaceholder { get; set; }
	public string LiteralValue { get; set; } = string.Empty;
	public string PlaceholderKey { get; set; } = string.Empty;

	public static BindableValue Literal(string value) => new() { LiteralValue = value };

	public static BindableValue Placeholder(string key) => new() { IsPlaceholder = true, PlaceholderKey = key };

	public string Resolve(IReadOnlyDictionary<string, string> data)
	{
		if (!IsPlaceholder)
			return LiteralValue;

		return data.TryGetValue(PlaceholderKey, out var value) ? value : string.Empty;
	}

	public override string ToString() => IsPlaceholder ? $"{{{PlaceholderKey}}}" : LiteralValue;
}
