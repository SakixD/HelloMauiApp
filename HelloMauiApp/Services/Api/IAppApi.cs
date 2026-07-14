using System.Text.Json.Nodes;

namespace HelloMauiApp.Services.Api;

/// <summary>Beschreibung eines API-Kommandos für die Discovery (GET /api).</summary>
public record ApiCommandInfo(string Name, string Description, string? PayloadHint);

/// <summary>Einheitliches Ergebnis aller API-Kommandos (wird als JSON serialisiert).</summary>
public record ApiResult(bool Success, object? Data, string? Error)
{
	public static ApiResult Ok(object? data = null) => new(true, data, null);

	public static ApiResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Zentraler Kommando-Dispatcher der App: Modul-zu-Modul-Kommunikation über benannte Kommandos
/// ("templates.list", "print.template", ...) mit JSON-Payload statt direkter Service-Referenzen.
/// Der lokale HTTP-Server (<see cref="LocalApiServer"/>) ist nur EIN Transport davor – In-App-Module
/// können <see cref="ExecuteAsync"/> genauso direkt aufrufen.
/// Lebt bewusst in der App (nicht im SDK): Das SDK bleibt die Service-Schicht; welche Kommandos
/// eine App daraus zusammensetzt, ist Konsumenten-Sache. Wandert die Funktionsliste später in eine
/// eigene Verteilung, kann der Dispatcher mitziehen, ohne dass sich Kommandos ändern.
/// </summary>
public interface IAppApi
{
	IReadOnlyList<ApiCommandInfo> Commands { get; }

	Task<ApiResult> ExecuteAsync(string command, JsonObject? payload, CancellationToken cancellationToken = default);
}
